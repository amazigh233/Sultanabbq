using System.Globalization;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SultanaBBQ.Shared;

namespace SultanaBBQ.Api.Services;

public sealed class EmailOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string FromAddress { get; init; } = "bilal.zambib4@gmail.com";
    public string FromName { get; init; } = "Sultana BBQ";
    public string RestaurantNotificationAddress { get; init; } = "bilal.zambib4@gmail.com";
}

public sealed class ReservationEmailSender(IOptions<EmailOptions> options)
{
    private static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");
    private readonly EmailOptions _options = options.Value;

    public async Task SendOwnerNotificationAsync(ReservationRequest request, Guid reservationId, CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        var customerReceived = BuildCustomerReceived(request, reservationId);
        await SendAsync(customerReceived, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.RestaurantNotificationAddress))
        {
            var notification = BuildRestaurantNotification(request, reservationId);
            await SendAsync(notification, cancellationToken);
        }
    }

    public async Task SendCustomerConfirmationAsync(OwnerReservation reservation, CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        var confirmation = BuildCustomerConfirmation(reservation);
        await SendAsync(confirmation, cancellationToken);
    }

    private MimeMessage BuildCustomerConfirmation(OwnerReservation reservation)
    {
        var message = CreateBaseMessage(reservation.Email.Trim(), reservation.Name.Trim());
        message.Subject = "Uw reservering bij Sultana BBQ is bevestigd";

        var date = FormatDate(reservation.Date);
        var notes = string.IsNullOrWhiteSpace(reservation.Notes) ? "Geen bijzonderheden opgegeven." : reservation.Notes.Trim();
        var safeName = WebUtility.HtmlEncode(reservation.Name.Trim());
        var safeNotes = WebUtility.HtmlEncode(notes);

        var body = new BodyBuilder
        {
            TextBody = $"""
                Beste {reservation.Name.Trim()},

                Uw reservering bij Sultana BBQ is bevestigd.

                Reserveringsgegevens:
                - Datum: {date}
                - Tijd: {reservation.Time}
                - Aantal personen: {reservation.Guests}
                - Reserveringsnummer: {reservation.Id}

                Bijzonderheden: {notes}

                Adres:
                Zamenhofdreef 69
                3562 JV Utrecht

                Tot dan!
                Sultana BBQ
                """,
            HtmlBody = $"""
                <p>Beste {safeName},</p>
                <p><strong>Uw reservering bij Sultana BBQ is bevestigd.</strong></p>
                <p>
                    Datum: <strong>{date}</strong><br>
                    Tijd: <strong>{WebUtility.HtmlEncode(reservation.Time)}</strong><br>
                    Aantal personen: <strong>{reservation.Guests}</strong><br>
                    Reserveringsnummer: <strong>{reservation.Id}</strong>
                </p>
                <p>Bijzonderheden: {safeNotes}</p>
                <p>
                    Zamenhofdreef 69<br>
                    3562 JV Utrecht
                </p>
                <p>Tot dan!<br>Sultana BBQ</p>
                """
        };

        message.Body = body.ToMessageBody();
        return message;
    }

    private MimeMessage BuildCustomerReceived(ReservationRequest request, Guid reservationId)
    {
        var message = CreateBaseMessage(request.Email.Trim(), request.Name.Trim());
        message.Subject = "Uw reserveringsaanvraag bij Sultana BBQ is ontvangen";

        var date = FormatDate(request.Date);
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? "Geen bijzonderheden opgegeven." : request.Notes.Trim();
        var safeName = WebUtility.HtmlEncode(request.Name.Trim());
        var safeNotes = WebUtility.HtmlEncode(notes);

        var body = new BodyBuilder
        {
            TextBody = $"""
                Beste {request.Name.Trim()},

                We hebben uw reserveringsaanvraag bij Sultana BBQ ontvangen.
                De eigenaar controleert de aanvraag. U ontvangt nog een aparte bevestigingsmail zodra de reservering is goedgekeurd.

                Aanvraaggegevens:
                - Datum: {date}
                - Tijd: {request.Time}
                - Aantal personen: {request.Guests}
                - Aanvraagnummer: {reservationId}

                Bijzonderheden: {notes}

                Sultana BBQ
                """,
            HtmlBody = $"""
                <p>Beste {safeName},</p>
                <p><strong>We hebben uw reserveringsaanvraag bij Sultana BBQ ontvangen.</strong></p>
                <p>De eigenaar controleert de aanvraag. U ontvangt nog een aparte bevestigingsmail zodra de reservering is goedgekeurd.</p>
                <p>
                    Datum: <strong>{date}</strong><br>
                    Tijd: <strong>{WebUtility.HtmlEncode(request.Time)}</strong><br>
                    Aantal personen: <strong>{request.Guests}</strong><br>
                    Aanvraagnummer: <strong>{reservationId}</strong>
                </p>
                <p>Bijzonderheden: {safeNotes}</p>
                <p>Sultana BBQ</p>
                """
        };

        message.Body = body.ToMessageBody();
        return message;
    }

    private MimeMessage BuildRestaurantNotification(ReservationRequest request, Guid reservationId)
    {
        var message = CreateBaseMessage(_options.RestaurantNotificationAddress, "Sultana BBQ");
        message.ReplyTo.Add(new MailboxAddress(request.Name.Trim(), request.Email.Trim()));
        message.Subject = $"Nieuwe reservering: {request.Name.Trim()} op {FormatDate(request.Date)} om {request.Time}";

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? "Geen bijzonderheden opgegeven." : request.Notes.Trim();
        var body = new BodyBuilder
        {
            TextBody = $"""
                Nieuwe reservering ontvangen. Bevestig deze in de eigenaarspagina.

                Reserveringsnummer: {reservationId}
                Naam: {request.Name.Trim()}
                E-mail: {request.Email.Trim()}
                Telefoon: {request.Phone.Trim()}
                Aantal personen: {request.Guests}
                Datum: {FormatDate(request.Date)}
                Tijd: {request.Time}
                Bijzonderheden: {notes}
                """
        };

        message.Body = body.ToMessageBody();
        return message;
    }

    private MimeMessage CreateBaseMessage(string toAddress, string toName)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName, toAddress));
        return message;
    }

    private async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();
        var socketOptions = _options.EnableSsl
            ? _options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);
        await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.Username) ||
            string.IsNullOrWhiteSpace(_options.Password) ||
            string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException("E-mail is nog niet geconfigureerd. Vul Email:Host, Email:Username, Email:Password en Email:FromAddress in.");
        }
    }

    private static string FormatDate(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("dddd d MMMM yyyy", DutchCulture);
}
