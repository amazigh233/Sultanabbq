namespace SultanaBBQ.Services;

public static class RestaurantInfo
{
    public const string Name = "Sultana BBQ";
    public const string CityArea = "Utrecht Overvecht";
    public const string StreetAddress = "Zamenhofdreef 69";
    public const string PostalCode = "3562 JV";
    public const string Locality = "Utrecht";
    public const string Region = "Utrecht";
    public const string Country = "NL";
    public const string PhoneDisplay = "+31 6 85605933";
    public const string PhoneHref = "+31685605933";
    public const string Email = "Sultanabbqinfo@gmail.com";
    public const string PriceRange = "€€";
    public const string BaseUrl = "https://www.sultanabbq.nl";
    public const string MapsUrl = "https://www.google.com/maps/search/?api=1&query=Zamenhofdreef%2069%203562%20JV%20Utrecht";
    public const string BistrooUrl = "https://bistroo.nl/utrecht/restaurants/sultana-bbq";

    public static string BuildUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return $"{BaseUrl}/";
        }

        return $"{BaseUrl}/{path.TrimStart('/')}";
    }
}

public sealed record FaqItem(string Question, string Answer);

public sealed record BreadcrumbItem(string Name, string Path);

public sealed record SeoContentBlock(string Heading, IReadOnlyList<string> Paragraphs);

public sealed record SeoLink(string Text, string Href, string Style = "primary");
