using Npgsql;
using NpgsqlTypes;
using SultanaBBQ.Shared;

namespace SultanaBBQ.Api.Services;

public sealed class StaffRepository(IConfiguration configuration) : IAsyncDisposable
{
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private NpgsqlDataSource? _dataSource;
    private bool _schemaReady;

    public async Task<IReadOnlyList<DiningTableDto>> GetTablesAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var command = DataSource.CreateCommand("""
            select id, name, capacity, is_active, created_at
            from dining_tables
            order by name;
            """);

        var tables = new List<DiningTableDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(ReadTable(reader));
        }

        return tables;
    }

    public async Task<DiningTableDto> CreateTableAsync(DiningTableRequest request, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        ValidateTable(request);

        await using var command = DataSource.CreateCommand("""
            insert into dining_tables (id, name, capacity, is_active, created_at)
            values (@id, @name, @capacity, @is_active, now())
            returning id, name, capacity, is_active, created_at;
            """);

        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, Guid.NewGuid());
        AddTableParameters(command, request);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Tafel kon niet worden aangemaakt.");
        }

        return ReadTable(reader);
    }

    public async Task<DiningTableDto?> UpdateTableAsync(Guid id, DiningTableRequest request, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        ValidateTable(request);

        await using var command = DataSource.CreateCommand("""
            update dining_tables
            set name = @name,
                capacity = @capacity,
                is_active = @is_active
            where id = @id
            returning id, name, capacity, is_active, created_at;
            """);

        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);
        AddTableParameters(command, request);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTable(reader) : null;
    }

    public async Task<IReadOnlyList<TableStatusDto>> GetTableStatusesAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var command = DataSource.CreateCommand("""
            select t.id, t.name, t.capacity, t.is_active,
                   o.id as open_order_id,
                   coalesce(o.total, 0) as open_order_total,
                   r.id as reservation_id,
                   r.name as reservation_name,
                   r.reservation_time
            from dining_tables t
            left join lateral (
                select id, total
                from table_orders
                where table_id = t.id and status = 'open'
                order by created_at desc
                limit 1
            ) o on true
            left join lateral (
                select id, name, reservation_time
                from reservations
                where table_id = t.id
                  and status = 'confirmed'
                  and reservation_date = current_date
                  and reservation_time >= (current_time - interval '2 hours')::time
                order by reservation_time
                limit 1
            ) r on true
            order by t.name;
            """);

        var statuses = new List<TableStatusDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var isActive = reader.GetBoolean(3);
            Guid? openOrderId = reader.IsDBNull(4) ? null : reader.GetGuid(4);
            Guid? reservationId = reader.IsDBNull(6) ? null : reader.GetGuid(6);
            var status = !isActive
                ? "inactive"
                : openOrderId is not null
                    ? "occupied"
                    : reservationId is not null ? "reserved" : "free";

            statuses.Add(new TableStatusDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                isActive,
                status,
                openOrderId,
                reservationId,
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<TimeOnly>(8).ToString("HH:mm"),
                reader.GetDecimal(5)));
        }

        return statuses;
    }

    public async Task<TableOrderDto?> GetOpenOrderForTableAsync(Guid tableId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var command = DataSource.CreateCommand("""
            select id
            from table_orders
            where table_id = @table_id and status = 'open'
            order by created_at desc
            limit 1;
            """);
        command.Parameters.AddWithValue("table_id", NpgsqlDbType.Uuid, tableId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid orderId ? await GetOrderByIdAsync(orderId, cancellationToken) : null;
    }

    public async Task<TableOrderDto?> AddItemToTableOrderAsync(Guid tableId, AddOrderItemRequest request, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        ValidateItem(request);

        var orderId = await GetOrCreateOpenOrderIdAsync(tableId, cancellationToken);
        if (orderId is null)
        {
            return null;
        }

        await using var command = DataSource.CreateCommand("""
            insert into table_order_items (id, order_id, category, name, unit_price, quantity, note, created_at)
            values (@id, @order_id, @category, @name, @unit_price, @quantity, @note, now());
            """);

        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, Guid.NewGuid());
        command.Parameters.AddWithValue("order_id", NpgsqlDbType.Uuid, orderId.Value);
        AddItemParameters(command, request);

        await command.ExecuteNonQueryAsync(cancellationToken);
        await RefreshTotalAsync(orderId.Value, cancellationToken);
        return await GetOrderByIdAsync(orderId.Value, cancellationToken);
    }

    public async Task<TableOrderDto?> SubmitCustomerTableOrderAsync(CustomerTableOrderRequest request, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        ValidateCustomerOrder(request);

        var tableId = await FindActiveTableIdAsync(request.TableNumber, cancellationToken);
        if (tableId is null)
        {
            return null;
        }

        var orderId = await GetOrCreateOpenOrderIdAsync(tableId.Value, cancellationToken);
        if (orderId is null)
        {
            return null;
        }

        await using var orderCommand = DataSource.CreateCommand("""
            update table_orders
            set customer_name = coalesce(nullif(@customer_name, ''), customer_name),
                customer_phone = coalesce(nullif(@customer_phone, ''), customer_phone),
                order_note = coalesce(nullif(@order_note, ''), order_note)
            where id = @order_id and status = 'open';
            """);
        orderCommand.Parameters.AddWithValue("order_id", NpgsqlDbType.Uuid, orderId.Value);
        orderCommand.Parameters.AddWithValue("customer_name", NpgsqlDbType.Varchar, request.CustomerName?.Trim() ?? "");
        orderCommand.Parameters.AddWithValue("customer_phone", NpgsqlDbType.Varchar, request.CustomerPhone?.Trim() ?? "");
        orderCommand.Parameters.AddWithValue("order_note", NpgsqlDbType.Text, request.Note?.Trim() ?? "");
        await orderCommand.ExecuteNonQueryAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            await using var itemCommand = DataSource.CreateCommand("""
                insert into table_order_items (id, order_id, category, name, unit_price, quantity, note, created_at)
                values (@id, @order_id, @category, @name, @unit_price, @quantity, null, now());
                """);

            itemCommand.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, Guid.NewGuid());
            itemCommand.Parameters.AddWithValue("order_id", NpgsqlDbType.Uuid, orderId.Value);
            itemCommand.Parameters.AddWithValue("category", NpgsqlDbType.Varchar, item.Category.Trim());
            itemCommand.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, item.Name.Trim());
            itemCommand.Parameters.AddWithValue("unit_price", NpgsqlDbType.Numeric, item.UnitPrice);
            itemCommand.Parameters.AddWithValue("quantity", NpgsqlDbType.Integer, item.Quantity);
            await itemCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await RefreshTotalAsync(orderId.Value, cancellationToken);
        return await GetOrderByIdAsync(orderId.Value, cancellationToken);
    }

    public async Task<TableOrderDto?> UpdateOrderItemAsync(Guid orderId, Guid itemId, UpdateOrderItemRequest request, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        if (request.Quantity <= 0)
        {
            await using var deleteCommand = DataSource.CreateCommand("""
                delete from table_order_items
                where id = @item_id
                  and order_id = @order_id
                  and exists (select 1 from table_orders where id = @order_id and status = 'open');
                """);
            deleteCommand.Parameters.AddWithValue("item_id", NpgsqlDbType.Uuid, itemId);
            deleteCommand.Parameters.AddWithValue("order_id", NpgsqlDbType.Uuid, orderId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var updateCommand = DataSource.CreateCommand("""
                update table_order_items
                set quantity = @quantity,
                    note = @note
                where id = @item_id
                  and order_id = @order_id
                  and exists (select 1 from table_orders where id = @order_id and status = 'open');
                """);
            updateCommand.Parameters.AddWithValue("item_id", NpgsqlDbType.Uuid, itemId);
            updateCommand.Parameters.AddWithValue("order_id", NpgsqlDbType.Uuid, orderId);
            updateCommand.Parameters.AddWithValue("quantity", NpgsqlDbType.Integer, request.Quantity);
            updateCommand.Parameters.AddWithValue("note", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(request.Note) ? DBNull.Value : request.Note.Trim());
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await RefreshTotalAsync(orderId, cancellationToken);
        return await GetOrderByIdAsync(orderId, cancellationToken);
    }

    public async Task<TableOrderDto?> CompleteOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await RefreshTotalAsync(orderId, cancellationToken);

        await using var command = DataSource.CreateCommand("""
            update table_orders
            set status = 'completed',
                completed_at = now()
            where id = @order_id and status = 'open'
            returning id;
            """);
        command.Parameters.AddWithValue("order_id", NpgsqlDbType.Uuid, orderId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid ? await GetOrderByIdAsync(orderId, cancellationToken) : null;
    }

    public async Task<IReadOnlyList<TableOrderDto>> GetOpenOrdersAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var command = DataSource.CreateCommand("""
            select id
            from table_orders
            where status = 'open'
            order by created_at;
            """);

        var orderIds = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            orderIds.Add(reader.GetGuid(0));
        }

        var orders = new List<TableOrderDto>();
        foreach (var orderId in orderIds)
        {
            var order = await GetOrderByIdAsync(orderId, cancellationToken);
            if (order is not null)
            {
                orders.Add(order);
            }
        }

        return orders;
    }

    public async Task<ReceiptDto?> GetReceiptAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        var order = await GetOrderByIdAsync(orderId, cancellationToken);
        if (order is null || order.CompletedAt is null)
        {
            return null;
        }

        return new ReceiptDto(order.Id, order.TableName, order.CreatedAt, order.CompletedAt.Value, order.Total, order.Items);
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

                create table if not exists table_orders (
                    id uuid primary key,
                    table_id uuid not null references dining_tables(id),
                    status varchar(30) not null default 'open',
                    total numeric(10, 2) not null default 0,
                    created_at timestamptz not null default now(),
                    completed_at timestamptz null
                );

                alter table table_orders
                    add column if not exists customer_name varchar(120) null,
                    add column if not exists customer_phone varchar(50) null,
                    add column if not exists order_note text null;

                create table if not exists table_order_items (
                    id uuid primary key,
                    order_id uuid not null references table_orders(id) on delete cascade,
                    category varchar(120) not null,
                    name varchar(160) not null,
                    unit_price numeric(10, 2) not null,
                    quantity integer not null check (quantity > 0),
                    note text null,
                    created_at timestamptz not null default now()
                );

                create unique index if not exists ux_table_orders_open_table
                    on table_orders (table_id)
                    where status = 'open';

                create index if not exists ix_table_order_items_order
                    on table_order_items (order_id);
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

    private async Task<Guid?> GetOrCreateOpenOrderIdAsync(Guid tableId, CancellationToken cancellationToken)
    {
        await using var command = DataSource.CreateCommand("""
            with selected_table as (
                select id
                from dining_tables
                where id = @table_id and is_active = true
            ),
            existing_order as (
                select id
                from table_orders
                where table_id = @table_id and status = 'open'
                limit 1
            ),
            inserted_order as (
                insert into table_orders (id, table_id, status, total, created_at)
                select @order_id, id, 'open', 0, now()
                from selected_table
                where not exists (select 1 from existing_order)
                returning id
            )
            select id from existing_order
            union all
            select id from inserted_order
            limit 1;
            """);

        command.Parameters.AddWithValue("table_id", NpgsqlDbType.Uuid, tableId);
        command.Parameters.AddWithValue("order_id", NpgsqlDbType.Uuid, Guid.NewGuid());

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid orderId ? orderId : null;
    }

    private async Task<TableOrderDto?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var command = DataSource.CreateCommand("""
            select o.id, o.table_id, t.name, o.status, o.created_at, o.completed_at, o.total,
                   o.customer_name, o.customer_phone, o.order_note
            from table_orders o
            inner join dining_tables t on t.id = o.table_id
            where o.id = @order_id;
            """);
        command.Parameters.AddWithValue("order_id", NpgsqlDbType.Uuid, orderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var order = new TableOrderDto(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetDecimal(6),
            [],
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9));

        await reader.DisposeAsync();
        var items = await GetOrderItemsAsync(orderId, cancellationToken);
        return order with { Items = items };
    }

    private async Task<Guid?> FindActiveTableIdAsync(string tableNumber, CancellationToken cancellationToken)
    {
        var normalized = tableNumber.Trim();
        var digits = new string(normalized.Where(char.IsDigit).ToArray());

        await using var command = DataSource.CreateCommand("""
            select id
            from dining_tables
            where is_active = true
              and (
                  lower(name) = lower(@table_name)
                  or regexp_replace(name, '[^0-9]+', '', 'g') = @table_digits
              )
            order by capacity asc, name asc
            limit 1;
            """);
        command.Parameters.AddWithValue("table_name", NpgsqlDbType.Varchar, normalized);
        command.Parameters.AddWithValue("table_digits", NpgsqlDbType.Varchar, digits);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid tableId ? tableId : null;
    }

    private async Task<IReadOnlyList<TableOrderItemDto>> GetOrderItemsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var command = DataSource.CreateCommand("""
            select id, order_id, category, name, unit_price, quantity, note, unit_price * quantity as line_total
            from table_order_items
            where order_id = @order_id
            order by created_at, name;
            """);
        command.Parameters.AddWithValue("order_id", NpgsqlDbType.Uuid, orderId);

        var items = new List<TableOrderItemDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TableOrderItemDto(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetDecimal(7)));
        }

        return items;
    }

    private async Task RefreshTotalAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var command = DataSource.CreateCommand("""
            update table_orders
            set total = coalesce((
                select sum(unit_price * quantity)
                from table_order_items
                where order_id = @order_id
            ), 0)
            where id = @order_id and status = 'open';
            """);
        command.Parameters.AddWithValue("order_id", NpgsqlDbType.Uuid, orderId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DiningTableDto ReadTable(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetBoolean(3),
            reader.GetFieldValue<DateTimeOffset>(4));

    private static void AddTableParameters(NpgsqlCommand command, DiningTableRequest request)
    {
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, request.Name.Trim());
        command.Parameters.AddWithValue("capacity", NpgsqlDbType.Integer, request.Capacity);
        command.Parameters.AddWithValue("is_active", NpgsqlDbType.Boolean, request.IsActive);
    }

    private static void AddItemParameters(NpgsqlCommand command, AddOrderItemRequest request)
    {
        command.Parameters.AddWithValue("category", NpgsqlDbType.Varchar, request.Category.Trim());
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, request.Name.Trim());
        command.Parameters.AddWithValue("unit_price", NpgsqlDbType.Numeric, request.UnitPrice);
        command.Parameters.AddWithValue("quantity", NpgsqlDbType.Integer, request.Quantity);
        command.Parameters.AddWithValue("note", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(request.Note) ? DBNull.Value : request.Note.Trim());
    }

    private static void ValidateTable(DiningTableRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Tafelnaam is verplicht.");
        }

        if (request.Capacity is < 1 or > 40)
        {
            throw new ArgumentException("Capaciteit moet tussen 1 en 40 liggen.");
        }
    }

    private static void ValidateItem(AddOrderItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            request.UnitPrice < 0 ||
            request.Quantity < 1)
        {
            throw new ArgumentException("Orderregel is niet geldig.");
        }
    }

    private static void ValidateCustomerOrder(CustomerTableOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TableNumber))
        {
            throw new ArgumentException("Tafelnummer is verplicht.");
        }

        if (request.Items.Count == 0)
        {
            throw new ArgumentException("Kies minimaal een gerecht.");
        }

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Category) ||
                string.IsNullOrWhiteSpace(item.Name) ||
                item.UnitPrice < 0 ||
                item.Quantity < 1)
            {
                throw new ArgumentException("Een orderregel is niet geldig.");
            }
        }
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
