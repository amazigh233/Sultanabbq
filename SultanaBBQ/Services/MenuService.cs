namespace SultanaBBQ.Services;

public static class MenuService
{
    public static List<string> GetCategories()
    {
        return new List<string>
        {
            "Koude Voorgerechten",
            "Warme Voorgerechten",
            "Grillgerechten",
            "Hamburgers",
            "Wraps",
            "Kebab",
            "Falafel",
            "Snacks",
            "Dranken"
        };
    }

    public static List<MenuItem> GetItemsByCategory(string category)
    {
        return category switch
        {
            "Koude Voorgerechten" => new List<MenuItem>
            {
                new("Hummus", "Romige kikkererwten dip met tahini en olijfolie", 5.50m),
                new("Baba Ganoush", "Gegrilde aubergine dip met tahini en knoflook", 6.00m),
                new("Tabbouleh", "Frisse salade met bulgur, peterselie, tomaat en munt", 6.50m),
                new("Fattoush", "Libanese salade met knapperig brood en sumak", 6.50m),
                new("Muhammara", "Pittige walnoot-paprika dip", 6.00m),
            },
            "Warme Voorgerechten" => new List<MenuItem>
            {
                new("Kibbeh", "Gefrituurde bulgur-gehaktballetjes met pijnboompitten", 7.50m),
                new("Sambousek Kaas", "Knapperige deegdriehoeken gevuld met kaas", 6.50m),
                new("Sambousek Vlees", "Knapperige deegdriehoeken gevuld met gekruid gehakt", 7.00m),
                new("Soep van de Dag", "Dagverse soep met brood", 5.50m),
                new("Halloumi Grill", "Gegrilde halloumi kaas met munt", 7.00m),
            },
            "Grillgerechten" => new List<MenuItem>
            {
                new("Mixed Grill", "Combinatie van kip, lam en kofte van de grill", 16.50m),
                new("Lamskoteletten", "Gemarineerde lamskoteletten van de houtskool", 18.00m),
                new("Kip Shish Taouk", "Gemarineerde kipspiesjes met knoflooksaus", 14.50m),
                new("Adana Kebab", "Pittig gekruid lamsgehakt van de grill", 15.00m),
                new("Kofta Grill", "Huisgemaakte gehaktspiesjes met kruiden", 14.00m),
                new("Whole Chicken", "Hele kip gemarineerd en gegrild op houtskool", 22.00m),
            },
            "Hamburgers" => new List<MenuItem>
            {
                new("Classic Burger", "Rundvlees burger met sla, tomaat en saus", 9.50m),
                new("Cheese Burger", "Met cheddar kaas en karamelliseerde ui", 10.50m),
                new("BBQ Burger", "Met BBQ saus, jalapeño en crispy ui", 11.50m),
                new("Chicken Burger", "Krokante kip filet met mayo en sla", 10.00m),
                new("Falafel Burger", "Vegetarische burger met falafel en tahini", 9.50m),
            },
            "Wraps" => new List<MenuItem>
            {
                new("Shawarma Wrap", "Dun gesneden gekruid vlees met groenten en saus", 10.00m),
                new("Falafel Wrap", "Knapperige falafel met hummus en groenten", 9.00m),
                new("Kip Wrap", "Gegrilde kip met knoflooksaus en salade", 10.00m),
                new("Mixed Wrap", "Combinatie van vlees met alle toppings", 11.50m),
                new("Halloumi Wrap", "Gegrilde halloumi met groenten en munt", 9.50m),
            },
            "Kebab" => new List<MenuItem>
            {
                new("Döner Kebab", "Klassieke döner met verse groenten en saus", 9.50m),
                new("Iskender Kebab", "Döner op brood met tomatensaus en yoghurt", 13.50m),
                new("Shish Kebab", "Lam spiesjes met gegrilde groenten", 14.50m),
                new("Kip Kebab", "Gemarineerde kip kebab met rijst", 12.50m),
                new("Dürüm", "Dunne wrap met döner vlees en groenten", 10.00m),
            },
            "Falafel" => new List<MenuItem>
            {
                new("Falafel Bord", "6 stuks falafel met hummus, salade en brood", 11.00m),
                new("Falafel Wrap", "Knapperige falafel in wrap met tahini", 9.00m),
                new("Falafel Pita", "Falafel in warm pitabrood met groenten", 8.50m),
                new("Falafel Salade", "Falafel op bed van frisse salade", 10.50m),
            },
            "Snacks" => new List<MenuItem>
            {
                new("Friet", "Knapperige frites met saus naar keuze", 4.00m),
                new("Kapsalon", "Friet met döner, kaas, salade en saus", 11.00m),
                new("Loaded Fries", "Friet met kebabvlees, kaas en jalapeño", 10.50m),
                new("Kipnuggets", "6 stuks krokante kipnuggets", 6.50m),
                new("Cheese Sticks", "Krokante kaas sticks met dipsaus", 5.50m),
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
