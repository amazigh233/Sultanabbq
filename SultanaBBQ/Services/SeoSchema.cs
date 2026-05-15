using System.Text.Encodings.Web;
using System.Text.Json;

namespace SultanaBBQ.Services;

public static class SeoSchema
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static string BuildPageSchema(
        string path,
        IEnumerable<BreadcrumbItem>? breadcrumbs = null,
        IEnumerable<FaqItem>? faqs = null,
        bool includeMenu = false)
    {
        var graph = new List<object>
        {
            RestaurantNode(),
            LocalBusinessNode(),
            BreadcrumbNode(breadcrumbs ?? DefaultBreadcrumbs(path))
        };

        if (includeMenu)
        {
            graph.Add(MenuNode());
        }

        if (faqs is not null)
        {
            var faqList = faqs.ToList();
            if (faqList.Count > 0)
            {
                graph.Add(FaqNode(faqList));
            }
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        }, JsonOptions);
    }

    public static IReadOnlyList<FaqItem> DefaultFaqs =>
    [
        new("Is Sultana BBQ halal?", "Ja, Sultana BBQ richt zich op halal BBQ- en grillgerechten. Vraag het team gerust naar ingredienten of allergenen."),
        new("Waar zit Sultana BBQ?", "Sultana BBQ zit aan de Zamenhofdreef 69 in Utrecht Overvecht."),
        new("Kan ik afhalen bij Sultana BBQ?", "Ja, je kunt gerechten afhalen. Bekijk het menu en bestel vooraf voor een soepele afhaalmoment."),
        new("Kan ik reserveren bij Sultana BBQ?", "Ja, reserveren kan online via de reserveringspagina of telefonisch."),
        new("Is Sultana BBQ geschikt voor families?", "Ja, het restaurant is geschikt voor families, vrienden en kleine gezelschappen."),
        new("Heeft Sultana BBQ mixed grill?", "Ja, mixed grill is een belangrijk onderdeel van het menu met verschillende halal grillgerechten."),
        new("Kan ik met een groep eten bij Sultana BBQ?", "Ja, groepen zijn welkom. Reserveer vooraf, vooral bij grotere gezelschappen."),
        new("Is er parkeergelegenheid in de buurt?", "In de omgeving van de Zamenhofdreef zijn parkeermogelijkheden. Controleer ter plekke de actuele parkeerregels.")
    ];

    private static object RestaurantNode() => new Dictionary<string, object?>
    {
        ["@type"] = "Restaurant",
        ["@id"] = $"{RestaurantInfo.BaseUrl}/#restaurant",
        ["name"] = RestaurantInfo.Name,
        ["url"] = RestaurantInfo.BaseUrl,
        ["telephone"] = RestaurantInfo.PhoneHref,
        ["email"] = RestaurantInfo.Email,
        ["priceRange"] = RestaurantInfo.PriceRange,
        ["servesCuisine"] = new[] { "Halal", "Barbecue", "Grill", "Middle Eastern" },
        ["address"] = AddressNode(),
        ["areaServed"] = new[] { "Utrecht", "Overvecht" },
        ["openingHoursSpecification"] = OpeningHours(),
        ["hasMenu"] = RestaurantInfo.BuildUrl("/menu")
    };

    private static object LocalBusinessNode() => new Dictionary<string, object?>
    {
        ["@type"] = "LocalBusiness",
        ["@id"] = $"{RestaurantInfo.BaseUrl}/#localbusiness",
        ["name"] = RestaurantInfo.Name,
        ["url"] = RestaurantInfo.BaseUrl,
        ["telephone"] = RestaurantInfo.PhoneHref,
        ["priceRange"] = RestaurantInfo.PriceRange,
        ["address"] = AddressNode(),
        ["openingHours"] = "Mo-Su 11:00-22:00"
    };

    private static object AddressNode() => new Dictionary<string, object?>
    {
        ["@type"] = "PostalAddress",
        ["streetAddress"] = RestaurantInfo.StreetAddress,
        ["postalCode"] = RestaurantInfo.PostalCode,
        ["addressLocality"] = RestaurantInfo.Locality,
        ["addressRegion"] = RestaurantInfo.Region,
        ["addressCountry"] = RestaurantInfo.Country
    };

    private static object[] OpeningHours() =>
    [
        new Dictionary<string, object?>
        {
            ["@type"] = "OpeningHoursSpecification",
            ["dayOfWeek"] = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" },
            ["opens"] = "11:00",
            ["closes"] = "22:00"
        }
    ];

    private static object MenuNode() => new Dictionary<string, object?>
    {
        ["@type"] = "Menu",
        ["@id"] = $"{RestaurantInfo.BaseUrl}/menu#menu",
        ["name"] = "Menu van Sultana BBQ",
        ["url"] = RestaurantInfo.BuildUrl("/menu"),
        ["hasMenuSection"] = MenuService.GetCategories().Select(category => new Dictionary<string, object?>
        {
            ["@type"] = "MenuSection",
            ["name"] = category,
            ["hasMenuItem"] = MenuService.GetItemsByCategory(category).Select(item => new Dictionary<string, object?>
            {
                ["@type"] = "MenuItem",
                ["name"] = item.Name,
                ["description"] = item.Description,
                ["offers"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Offer",
                    ["price"] = item.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    ["priceCurrency"] = "EUR"
                }
            }).ToArray()
        }).ToArray()
    };

    private static object FaqNode(IReadOnlyList<FaqItem> faqs) => new Dictionary<string, object?>
    {
        ["@type"] = "FAQPage",
        ["mainEntity"] = faqs.Select(faq => new Dictionary<string, object?>
        {
            ["@type"] = "Question",
            ["name"] = faq.Question,
            ["acceptedAnswer"] = new Dictionary<string, object?>
            {
                ["@type"] = "Answer",
                ["text"] = faq.Answer
            }
        }).ToArray()
    };

    private static object BreadcrumbNode(IEnumerable<BreadcrumbItem> breadcrumbs) => new Dictionary<string, object?>
    {
        ["@type"] = "BreadcrumbList",
        ["itemListElement"] = breadcrumbs.Select((breadcrumb, index) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = index + 1,
            ["name"] = breadcrumb.Name,
            ["item"] = RestaurantInfo.BuildUrl(breadcrumb.Path)
        }).ToArray()
    };

    private static IEnumerable<BreadcrumbItem> DefaultBreadcrumbs(string path)
    {
        yield return new BreadcrumbItem("Home", "/");

        if (!string.IsNullOrWhiteSpace(path) && path != "/")
        {
            yield return new BreadcrumbItem(path.Trim('/').Replace("-", " "), path);
        }
    }
}
