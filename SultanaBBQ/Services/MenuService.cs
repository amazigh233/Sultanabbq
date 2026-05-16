namespace SultanaBBQ.Services;

public static class MenuService
{
    public static List<string> GetCategories()
    {
        return new List<string>
        {
            "Grillgerechten",
            "Mixed grill",
            "Kipgerechten",
            "Vleesgerechten",
            "Burgers",
            "Schotels",
            "Bijgerechten",
            "Dranken"
        };
    }

    public static List<MenuItem> GetItemsByCategory(string category)
    {
        return category switch
        {
            "Grillgerechten" => new List<MenuItem>
            {
                new("Adana Kebab", "Pittig gekruid halal grillgerecht van lamsgehakt met salade, rijst of friet", 15.00m),
                new("Kofta Grill", "Huisgemaakte gehaktspies met kruiden, gegrild op hoge temperatuur", 14.00m),
                new("Lamskoteletten", "Gemarineerde lamskoteletten van de grill met knoflooksaus en garnituur", 18.00m),
                new("Shish Kebab", "Malse vleesspies met gegrilde groenten, ideaal voor liefhebbers van grillrestaurant Utrecht", 14.50m),
                new("Whole Chicken", "Hele kip, langzaam gemarineerd en vers gegrild voor twee personen", 22.00m),
            },
            "Mixed grill" => new List<MenuItem>
            {
                new("Mixed Grill Sultana", "Combinatie van kip, lam en kofta: mixed grill in Utrecht Overvecht", 16.50m),
                new("Mixed Grill Royal", "Royale schaal met meerdere spiesen, lamskoteletten en bijgerechten", 24.50m),
                new("Family Mixed Grill", "Grote halal BBQ-schotel om te delen met familie of vrienden", 42.00m),
                new("Mixed Grill Kip & Vlees", "Gevarieerde grillmix met kip shish, kofta en Adana kebab", 19.50m),
                new("Sultana Groepsschotel", "Royale mixed grill voor groepen, met rijst, salade, brood en sauzen", 58.00m),
            },
            "Kipgerechten" => new List<MenuItem>
            {
                new("Kip Shish Taouk", "Gemarineerde kipspiesjes met knoflooksaus, salade en rijst", 14.50m),
                new("Halve Kip Grill", "Halve kip van de grill met kruidige marinade en friet", 13.50m),
                new("Kip Kebab Schotel", "Halal kip kebab met rijst, salade, brood en huisgemaakte saus", 12.50m),
                new("Chicken Burger", "Krokante kipfilet met sla, saus en brioche bun", 10.00m),
                new("Kip Wrap", "Gegrilde kip in wrap met knoflooksaus en verse groenten", 10.00m),
            },
            "Vleesgerechten" => new List<MenuItem>
            {
                new("Döner Kebab", "Klassieke döner met verse groenten en saus", 9.50m),
                new("Iskender Kebab", "Döner op brood met tomatensaus en yoghurt", 13.50m),
                new("Shawarma Wrap", "Dun gesneden gekruid vlees met groenten en saus", 10.00m),
                new("Lamsvlees Schotel", "Mals lamsvlees met rijst, salade en warme saus", 16.50m),
                new("Kapsalon Vlees", "Friet met vlees, kaas, salade en saus", 11.00m),
            },
            "Burgers" => new List<MenuItem>
            {
                new("Classic Burger", "Rundvleesburger met sla, tomaat en huisgemaakte saus", 9.50m),
                new("Cheese Burger", "Burger met cheddar, gekaramelliseerde ui en frisse salade", 10.50m),
                new("BBQ Burger", "Burger met BBQ-saus, jalapeño en crispy ui", 11.50m),
                new("Chicken Burger", "Krokante kipburger met mayo en sla", 10.00m),
                new("Falafel Burger", "Vegetarische burger met falafel en tahinisaus", 9.50m),
            },
            "Schotels" => new List<MenuItem>
            {
                new("Falafel Schotel", "Knapperige falafel met hummus, salade en brood", 11.00m),
                new("Kip Schotel", "Gegrilde kip met rijst, salade, brood en saus", 13.50m),
                new("Kebab Schotel", "Kebabvlees met rijst of friet, salade en knoflooksaus", 13.50m),
                new("Mixed Schotel", "Combinatie van vlees en kip met royale bijgerechten", 15.50m),
                new("Halloumi Schotel", "Gegrilde halloumi met salade, brood en frisse dips", 12.50m),
            },
            "Bijgerechten" => new List<MenuItem>
            {
                new("Hummus", "Romige kikkererwtendip met tahini en olijfolie", 5.50m),
                new("Baba Ganoush", "Gegrilde auberginedip met tahini en knoflook", 6.00m),
                new("Tabbouleh", "Frisse salade met bulgur, peterselie, tomaat en munt", 6.50m),
                new("Kibbeh", "Gefrituurde bulgur-gehaktballetjes met pijnboompitten", 7.50m),
                new("Friet", "Knapperige frites met saus naar keuze", 4.00m),
                new("Loaded Fries", "Friet met kebabvlees, kaas en jalapeño", 10.50m),
            },
            "Dranken" => new List<MenuItem>
            {
                new("Ayran", "Verfrissende yoghurt drank", 2.50m),
                new("Mint Limonade", "Huisgemaakte limonade met verse munt", 3.50m),
                new("Cola / Fanta / Sprite", "Fris 33cl", 2.50m),
                new("Water", "Plat of bruis 50cl", 2.00m),
                new("Turkse Thee", "Traditionele zwarte thee", 2.00m),
            },
            _ => new List<MenuItem>()
        };
    }

    public static List<MenuItem> GetAllItems()
    {
        var allItems = new List<MenuItem>();
        foreach (var category in GetCategories())
        {
            allItems.AddRange(GetItemsByCategory(category));
        }
        return allItems;
    }
}

public record MenuItem(string Name, string Description, decimal Price);
