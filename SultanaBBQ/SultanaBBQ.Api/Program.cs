using SultanaBBQ.Api.Services;
using SultanaBBQ.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<ReservationRepository>();
builder.Services.AddSingleton<StaffRepository>();
builder.Services.AddTransient<ReservationEmailSender>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDevOrdering", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5170",
                "https://localhost:5170",
                "http://127.0.0.1:5170",
                "https://127.0.0.1:5170")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseStaticFiles();
app.UseCors("LocalDevOrdering");
app.MapStaticAssets();

app.MapPost("/api/reservations", async (
    ReservationRequest request,
    ReservationRepository repository,
    ReservationEmailSender emailSender,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ReservationValidator.Validate(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        var id = await repository.CreateAsync(request, cancellationToken);
        await emailSender.SendOwnerNotificationAsync(request, id, cancellationToken);

        return Results.Created($"/api/reservations/{id}", new ReservationResponse(
            id,
            "Uw reservering is ontvangen. U krijgt een bevestiging zodra de eigenaar de reservering heeft bevestigd."));
    }
    catch (Exception ex) when (ex is InvalidOperationException or Npgsql.PostgresException or Npgsql.NpgsqlException or MailKit.Net.Smtp.SmtpCommandException or MailKit.Net.Smtp.SmtpProtocolException)
    {
        logger.LogError(ex, "Reservering verwerken is mislukt.");
        return Results.Problem("De reservering kon niet worden bevestigd. Controleer de database- en e-mailinstellingen.");
    }
});

app.MapPost("/api/table-orders", async (
    CustomerTableOrderRequest request,
    StaffRepository repository,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        var order = await repository.SubmitCustomerTableOrderAsync(request, cancellationToken);
        if (order is null)
        {
            return Results.NotFound(new StaffMessageResponse("Deze tafel is niet gevonden of is niet actief."));
        }

        return Results.Created($"/api/staff/orders/{order.Id}", order);
    }
    catch (Exception ex) when (ex is ArgumentException)
    {
        return Results.BadRequest(new StaffMessageResponse(ex.Message));
    }
    catch (Exception ex) when (ex is InvalidOperationException or Npgsql.PostgresException or Npgsql.NpgsqlException)
    {
        logger.LogError(ex, "Tafelbestelling verwerken is mislukt.");
        return Results.Problem("De tafelbestelling kon niet worden geplaatst. Controleer de database-instellingen.");
    }
});

app.MapGet("/api/owner/reservations", async (
    HttpRequest request,
    IConfiguration configuration,
    ReservationRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    var reservations = await repository.GetAllAsync(cancellationToken);
    return Results.Ok(reservations);
});

app.MapPost("/api/owner/reservations/{id:guid}/confirm", async (
    Guid id,
    HttpRequest request,
    IConfiguration configuration,
    ReservationRepository repository,
    ReservationEmailSender emailSender,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    try
    {
        var (reservation, confirmedNow, error) = await repository.ConfirmAsync(id, cancellationToken);
        if (reservation is null)
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            return Results.Conflict(new StaffMessageResponse(error));
        }

        if (confirmedNow)
        {
            await emailSender.SendCustomerConfirmationAsync(reservation, cancellationToken);
        }

        return Results.Ok(reservation);
    }
    catch (Exception ex) when (ex is InvalidOperationException or Npgsql.PostgresException or Npgsql.NpgsqlException or MailKit.Net.Smtp.SmtpCommandException or MailKit.Net.Smtp.SmtpProtocolException)
    {
        logger.LogError(ex, "Reservering bevestigen is mislukt.");
        return Results.Problem("De reservering kon niet worden bevestigd. Controleer de database- en e-mailinstellingen.");
    }
});

app.MapGet("/api/staff/tables", async (
    HttpRequest request,
    IConfiguration configuration,
    StaffRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    var tables = await repository.GetTablesAsync(cancellationToken);
    return Results.Ok(tables);
});

app.MapPost("/api/staff/tables", async (
    DiningTableRequest table,
    HttpRequest request,
    IConfiguration configuration,
    StaffRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    try
    {
        var created = await repository.CreateTableAsync(table, cancellationToken);
        return Results.Created($"/api/staff/tables/{created.Id}", created);
    }
    catch (Exception ex) when (ex is ArgumentException or Npgsql.PostgresException or Npgsql.NpgsqlException)
    {
        return Results.BadRequest(new StaffMessageResponse(ex.Message));
    }
});

app.MapPut("/api/staff/tables/{id:guid}", async (
    Guid id,
    DiningTableRequest table,
    HttpRequest request,
    IConfiguration configuration,
    StaffRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    try
    {
        var updated = await repository.UpdateTableAsync(id, table, cancellationToken);
        return updated is null ? Results.NotFound() : Results.Ok(updated);
    }
    catch (Exception ex) when (ex is ArgumentException or Npgsql.PostgresException or Npgsql.NpgsqlException)
    {
        return Results.BadRequest(new StaffMessageResponse(ex.Message));
    }
});

app.MapGet("/api/staff/tables/status", async (
    HttpRequest request,
    IConfiguration configuration,
    StaffRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    var statuses = await repository.GetTableStatusesAsync(cancellationToken);
    return Results.Ok(statuses);
});

app.MapGet("/api/staff/tables/{tableId:guid}/order", async (
    Guid tableId,
    HttpRequest request,
    IConfiguration configuration,
    StaffRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    var order = await repository.GetOpenOrderForTableAsync(tableId, cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapPost("/api/staff/tables/{tableId:guid}/order", async (
    Guid tableId,
    AddOrderItemRequest item,
    HttpRequest request,
    IConfiguration configuration,
    StaffRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    try
    {
        var order = await repository.AddItemToTableOrderAsync(tableId, item, cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(order);
    }
    catch (Exception ex) when (ex is ArgumentException or Npgsql.PostgresException or Npgsql.NpgsqlException)
    {
        return Results.BadRequest(new StaffMessageResponse(ex.Message));
    }
});

app.MapPut("/api/staff/orders/{orderId:guid}/items/{itemId:guid}", async (
    Guid orderId,
    Guid itemId,
    UpdateOrderItemRequest item,
    HttpRequest request,
    IConfiguration configuration,
    StaffRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    var order = await repository.UpdateOrderItemAsync(orderId, itemId, item, cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapPost("/api/staff/orders/{orderId:guid}/complete", async (
    Guid orderId,
    HttpRequest request,
    IConfiguration configuration,
    StaffRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    var order = await repository.CompleteOrderAsync(orderId, cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapGet("/api/staff/orders/open", async (
    HttpRequest request,
    IConfiguration configuration,
    StaffRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    var orders = await repository.GetOpenOrdersAsync(cancellationToken);
    return Results.Ok(orders);
});

app.MapGet("/api/staff/orders/{orderId:guid}/receipt", async (
    Guid orderId,
    HttpRequest request,
    IConfiguration configuration,
    StaffRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!IsOwnerAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    var receipt = await repository.GetReceiptAsync(orderId, cancellationToken);
    return receipt is null ? Results.NotFound() : Results.Ok(receipt);
});

app.MapFallbackToFile("index.html");

app.Run();

static bool IsOwnerAuthorized(HttpRequest request, IConfiguration configuration)
{
    var expectedCode = configuration["Owner:AccessCode"];
    if (string.IsNullOrWhiteSpace(expectedCode))
    {
        return false;
    }

    return request.Headers.TryGetValue("X-Owner-Code", out var providedCode) &&
        string.Equals(providedCode.ToString(), expectedCode, StringComparison.Ordinal);
}
