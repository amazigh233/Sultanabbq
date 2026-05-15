using System.Net.Mail;
using SultanaBBQ.Shared;

namespace SultanaBBQ.Api.Services;

public static class ReservationValidator
{
    public static Dictionary<string, string[]> Validate(ReservationRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        AddRequired(errors, nameof(request.Name), request.Name, "Naam is verplicht.");
        AddRequired(errors, nameof(request.Email), request.Email, "E-mailadres is verplicht.");
        AddRequired(errors, nameof(request.Phone), request.Phone, "Telefoonnummer is verplicht.");

        if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
        {
            errors[nameof(request.Email)] = ["Vul een geldig e-mailadres in."];
        }

        if (request.Guests is < 1 or > 20)
        {
            errors[nameof(request.Guests)] = ["Kies een aantal personen tussen 1 en 20."];
        }

        if (request.Date < DateOnly.FromDateTime(DateTime.Today))
        {
            errors[nameof(request.Date)] = ["Kies vandaag of een datum in de toekomst."];
        }

        if (!TimeOnly.TryParse(request.Time, out _))
        {
            errors[nameof(request.Time)] = ["Kies een geldig tijdstip."];
        }

        return errors;
    }

    private static void AddRequired(Dictionary<string, string[]> errors, string key, string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[key] = [message];
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
