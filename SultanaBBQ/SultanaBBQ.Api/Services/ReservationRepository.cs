using Npgsql;
using NpgsqlTypes;
using SultanaBBQ.Shared;

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
            select id, name, email, phone, guests, reservation_date, reservation_time, notes, status, created_at
            from reservations
            order by created_at desc
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

    public async Task<(OwnerReservation? Reservation, bool ConfirmedNow)> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var command = DataSource.CreateCommand("""
            update reservations
            set status = 'confirmed'
            where id = @id and status <> 'confirmed'
            returning id, name, email, phone, guests, reservation_date, reservation_time, notes, status, created_at;
            """);

        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return (ReadReservation(reader), true);
        }

        await reader.DisposeAsync();
        return (await GetByIdAsync(id, cancellationToken), false);
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

                create index if not exists ix_reservations_date_time
                    on reservations (reservation_date, reservation_time);
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
            select id, name, email, phone, guests, reservation_date, reservation_time, notes, status, created_at
            from reservations
            where id = @id;
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
            reader.GetFieldValue<DateTimeOffset>(9));
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
