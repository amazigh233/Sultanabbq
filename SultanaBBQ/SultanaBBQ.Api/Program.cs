using SultanaBBQ.Api.Services;
using SultanaBBQ.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<ReservationRepository>();
builder.Services.AddTransient<ReservationEmailSender>();

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

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
        var (reservation, confirmedNow) = await repository.ConfirmAsync(id, cancellationToken);
        if (reservation is null)
        {
            return Results.NotFound();
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
