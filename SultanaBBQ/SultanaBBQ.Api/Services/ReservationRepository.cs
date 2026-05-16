using Npgsql;
using NpgsqlTypes;
using SultanaBBQ.Shared;
using System.Data;

namespace SultanaBBQ.Api.Services;

public sealed class ReservationRepository(IConfiguration configuration) : IAsyncDisposable
{
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private NpgsqlDataSource? _dataSource;
    private bool _schemaReady;

    public async Task<Guid> CreateAsync(ReservationRequest request, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        var id = Guid.NewGuid();
        var time = TimeOnly.Parse(request.Time);

        await using var command = DataSource.CreateCommand("""
            insert into reservations
                (id, name, email, phone, guests, reservation_date, reservation_time, notes, status, created_at)
            values
                (@id, @name, @email, @phone, @guests, @reservation_date, @reservation_time, @notes, 'pending', now());
            """);

        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, request.Name.Trim());
        command.Parameters.AddWithValue("email", NpgsqlDbType.Varchar, request.Email.Trim());
        command.Parameters.AddWithValue("phone", NpgsqlDbType.Varchar, request.Phone.Trim());
        command.Parameters.AddWithValue("guests", NpgsqlDbType.Integer, request.Guests);
        command.Parameters.AddWithValue("reservation_date", NpgsqlDbType.Date, request.Date);
        command.Parameters.AddWithValue("reservation_time", NpgsqlDbType.Time, time);
        command.Parameters.AddWithValue("notes", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(request.Notes) ? DBNull.Value : request.Notes.Trim());

        await command.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    public async Task<IReadOnlyList<OwnerReservation>> GetAllAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var command = DataSource.CreateCommand("""
            select r.id, r.name, r.email, r.phone, r.guests, r.reservation_date, r.reservation_time,
                   r.notes, r.status, r.created_at, r.table_id, t.name as table_name
            from reservations r
            left join dining_tables t on t.id = r.table_id
            order by r.created_at desc
            limit 200;
            """);

        var reservations = new List<OwnerReservation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            reservations.Add(ReadReservation(reader));
        }

        return reservations;
    }

    public async Task<(OwnerReservation? Reservation, bool ConfirmedNow, string? Error)> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        await using var reservationCommand = connection.CreateCommand();
        reservationCommand.Transaction = transaction;
        reservationCommand.CommandText = """
            select r.id, r.name, r.email, r.phone, r.guests, r.reservation_date, r.reservation_time,
                   r.notes, r.status, r.created_at, r.table_id, t.name as table_name
            from reservations r
            left join dining_tables t on t.id = r.table_id
            where r.id = @id
            for update of r;
            """;
        reservationCommand.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);

        await using var reader = await reservationCommand.ExecuteReaderAsync(cancellationToken);
        OwnerReservation? reservation = null;
        if (await reader.ReadAsync(cancellationToken))
        {
            reservation = ReadReservation(reader);
        }

        await reader.DisposeAsync();

        if (reservation is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (null, false, null);
        }

        if (reservation.Status == "confirmed")
        {
            await transaction.CommitAsync(cancellationToken);
            return (reservation, false, null);
        }

        await using var tableCommand = connection.CreateCommand();
        tableCommand.Transaction = transaction;
        tableCommand.CommandText = """
            select t.id
            from dining_tables t
            where t.is_active = true
              and t.capacity >= @guests
              and not exists (
                  select 1
                  from reservations other
                  where other.table_id = t.id
                    and other.id <> @reservation_id
                    and other.status = 'confirmed'
                    and other.reservation_date = @reservation_date
                    and tsrange(
                        other.reservation_date::timestamp + other.reservation_time,
                        other.reservation_date::timestamp + other.reservation_time + interval '2 hours',
                        '[)'
                    ) && tsrange(
                        @reservation_date + @reservation_time,
                        @reservation_date + @reservation_time + interval '2 hours',
                        '[)'
                    )
              )
            order by t.capacity asc, t.name asc
            limit 1;
            """;
        tableCommand.Parameters.AddWithValue("guests", NpgsqlDbType.Integer, reservation.Guests);
        tableCommand.Parameters.AddWithValue("reservation_id", NpgsqlDbType.Uuid, reservation.Id);
        tableCommand.Parameters.AddWithValue("reservation_date", NpgsqlDbType.Date, reservation.Date);
        tableCommand.Parameters.AddWithValue("reservation_time", NpgsqlDbType.Time, TimeOnly.Parse(reservation.Time));

        var tableResult = await tableCommand.ExecuteScalarAsync(cancellationToken);
        if (tableResult is not Guid tableId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (reservation, false, "Geen passende vrije tafel beschikbaar voor dit tijdstip.");
        }

        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            update reservations
            set status = 'confirmed',
                table_id = @table_id
            where id = @id
            returning id, name, email, phone, guests, reservation_date, reservation_time, notes, status, created_at,
                      table_id, (select name from dining_tables where id = @table_id) as table_name;
            """;
        updateCommand.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);
        updateCommand.Parameters.AddWithValue("table_id", NpgsqlDbType.Uuid, tableId);

        await using var updatedReader = await updateCommand.ExecuteReaderAsync(cancellationToken);
        OwnerReservation? updatedReservation = null;
        if (await updatedReader.ReadAsync(cancellationToken))
        {
            updatedReservation = ReadReservation(updatedReader);
        }

        await transaction.CommitAsync(cancellationToken);
        return (updatedReservation, true, null);
    }

    public async ValueTask DisposeAsync()
    {
        _schemaLock.Dispose();
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }

        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            await using var command = DataSource.CreateCommand("""
                create table if not exists dining_tables (
                    id uuid primary key,
                    name varchar(80) not null,
                    capacity integer not null check (capacity between 1 and 40),
                    is_active boolean not null default true,
                    created_at timestamptz not null default now()
                );

                create unique index if not exists ux_dining_tables_name
                    on dining_tables (lower(name));

                insert into dining_tables (id, name, capacity, is_active, created_at)
                select gen_random_uuid(), 'Tafel ' || n, 4, true, now()
                from generate_series(1, 12) n
                where not exists (select 1 from dining_tables);

                create table if not exists reservations (
                    id uuid primary key,
                    name varchar(120) not null,
                    email varchar(254) not null,
                    phone varchar(50) not null,
                    guests integer not null check (guests between 1 and 20),
                    reservation_date date not null,
                    reservation_time time not null,
                    notes text null,
                    status varchar(30) not null default 'pending',
                    created_at timestamptz not null default now()
                );

                alter table reservations
                    add column if not exists table_id uuid null references dining_tables(id);

                create index if not exists ix_reservations_date_time
                    on reservations (reservation_date, reservation_time);

                create index if not exists ix_reservations_table_date_time
                    on reservations (table_id, reservation_date, reservation_time);
                """);

            await command.ExecuteNonQueryAsync(cancellationToken);
            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private NpgsqlDataSource DataSource =>
        _dataSource ??= NpgsqlDataSource.Create(GetConnectionString(configuration));

    private async Task<OwnerReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = DataSource.CreateCommand("""
            select r.id, r.name, r.email, r.phone, r.guests, r.reservation_date, r.reservation_time,
                   r.notes, r.status, r.created_at, r.table_id, t.name as table_name
            from reservations r
            left join dining_tables t on t.id = r.table_id
            where r.id = @id;
            """);

        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadReservation(reader) : null;
    }

    private static OwnerReservation ReadReservation(NpgsqlDataReader reader)
    {
        var time = reader.GetFieldValue<TimeOnly>(6).ToString("HH:mm");

        return new OwnerReservation(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetFieldValue<DateOnly>(5),
            time,
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetString(8),
            reader.GetFieldValue<DateTimeOffset>(9),
            reader.IsDBNull(10) ? null : reader.GetGuid(10),
            reader.IsDBNull(11) ? null : reader.GetString(11));
    }

    private static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Postgres") ??
            configuration["DATABASE_URL"] ??
            configuration["POSTGRES_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Postgres is nog niet geconfigureerd. Vul ConnectionStrings:Postgres of DATABASE_URL in.");
        }

        return connectionString;
    }
}
