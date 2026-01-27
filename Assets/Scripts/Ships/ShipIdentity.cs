using UnityEngine;

/// <summary>
/// Ship Identity - Assigns origin country and ship name
/// based on the current map region.
/// 
/// Adds flavor like:
/// - "MV Horizon (Singapore)" for merchants
/// - "Pirate Vessel (Somali Waters)" for pirates  
/// - "KD Lekiu (Malaysian Navy)" for security
/// </summary>
public class ShipIdentity : MonoBehaviour
{
    [Header("=== IDENTITY (Auto-assigned) ===")]
    public string shipName;
    public string originCountry;
    public string destinationCountry;
    public string fullTitle;

    // Ship name prefixes by type
    private static readonly string[] MerchantPrefixes = { "MV", "SS", "MT", "Container Ship", "Cargo Vessel", "Tanker" };
    private static readonly string[] PiratePrefixes = { "Pirate Skiff", "Raider", "Vessel", "Boat", "Craft" };
    private static readonly string[] NavyPrefixes = { "Patrol Boat", "Corvette", "Frigate", "Coast Guard" };

    // Ship names (generic, work for any region)
    private static readonly string[] ShipNames = {
        "Horizon", "Fortune", "Swift", "Endeavor", "Voyager",
        "Star", "Wave", "Spirit", "Discovery", "Venture",
        "Pride", "Glory", "Hope", "Liberty", "Valor",
        "Phoenix", "Titan", "Aurora", "Neptune", "Atlas"
    };

    // Countries by region and role
    public static class Regions
    {
        // Strait of Malacca
        public static readonly string[] MalaccaMerchants = { "Singapore", "Malaysia", "Indonesia", "China", "Japan", "South Korea" };
        public static readonly string[] MalaccaPirates = { "Indonesian Waters", "Riau Islands", "Batam" };
        public static readonly string[] MalaccaNavy = { "Singapore", "Malaysia", "Indonesia" };
        public static readonly string[] MalaccaNavyNames = { "RSN", "RMN", "KRI" }; // Republic of Singapore Navy, Royal Malaysian Navy, Indonesian Navy

        // Gulf of Aden
        public static readonly string[] AdenMerchants = { "Yemen", "Djibouti", "Saudi Arabia", "Oman", "UAE", "Egypt" };
        public static readonly string[] AdenPirates = { "Somali Waters", "Puntland Coast", "Mogadishu" };
        public static readonly string[] AdenNavy = { "Djibouti", "Yemen", "International Coalition" };
        public static readonly string[] AdenNavyNames = { "Combined Task Force", "EUNAVFOR", "Naval Patrol" };

        // Gulf of Guinea
        public static readonly string[] GuineaMerchants = { "Nigeria", "Cameroon", "Gabon", "Ghana", "Equatorial Guinea", "São Tomé" };
        public static readonly string[] GuineaPirates = { "Nigerian Waters", "Niger Delta", "Cameroon Coast" };
        public static readonly string[] GuineaNavy = { "Nigeria", "Cameroon", "Ghana" };
        public static readonly string[] GuineaNavyNames = { "NNS", "Cameroon Navy", "Ghana Navy" }; // Nigerian Navy Ship
    }

    public enum MapRegion
    {
        StraitOfMalacca,
        GulfOfAden,
        GulfOfGuinea
    }

    // Current active region (set by map selection)
    public static MapRegion CurrentRegion = MapRegion.StraitOfMalacca;

    private ShipController shipController;
    private static System.Random rng = new System.Random();

    void Start()
    {
        shipController = GetComponent<ShipController>();
        
        // Delay to ensure ship data is ready
        Invoke(nameof(GenerateIdentity), 0.1f);
    }

    void GenerateIdentity()
    {
        if (shipController == null || shipController.Data == null)
        {
            Invoke(nameof(GenerateIdentity), 0.1f);
            return;
        }

        ShipType type = shipController.Data.type;
        
        // Get appropriate country lists for current region
        string[] merchantCountries, pirateOrigins, navyCountries, navyPrefixes;
        GetRegionData(out merchantCountries, out pirateOrigins, out navyCountries, out navyPrefixes);

        // Generate identity based on ship type
        switch (type)
        {
            case ShipType.Cargo:
                GenerateMerchantIdentity(merchantCountries);
                break;
            case ShipType.Pirate:
                GeneratePirateIdentity(pirateOrigins);
                break;
            case ShipType.Security:
                GenerateNavyIdentity(navyCountries, navyPrefixes);
                break;
        }
    }

    void GetRegionData(out string[] merchants, out string[] pirates, out string[] navy, out string[] navyNames)
    {
        switch (CurrentRegion)
        {
            case MapRegion.GulfOfAden:
                merchants = Regions.AdenMerchants;
                pirates = Regions.AdenPirates;
                navy = Regions.AdenNavy;
                navyNames = Regions.AdenNavyNames;
                break;
            case MapRegion.GulfOfGuinea:
                merchants = Regions.GuineaMerchants;
                pirates = Regions.GuineaPirates;
                navy = Regions.GuineaNavy;
                navyNames = Regions.GuineaNavyNames;
                break;
            case MapRegion.StraitOfMalacca:
            default:
                merchants = Regions.MalaccaMerchants;
                pirates = Regions.MalaccaPirates;
                navy = Regions.MalaccaNavy;
                navyNames = Regions.MalaccaNavyNames;
                break;
        }
    }

    void GenerateMerchantIdentity(string[] countries)
    {
        string prefix = MerchantPrefixes[rng.Next(MerchantPrefixes.Length)];
        string name = ShipNames[rng.Next(ShipNames.Length)];
        originCountry = countries[rng.Next(countries.Length)];
        
        // Pick a different destination
        do {
            destinationCountry = countries[rng.Next(countries.Length)];
        } while (destinationCountry == originCountry && countries.Length > 1);

        shipName = $"{prefix} {name}";
        fullTitle = $"{shipName} ({originCountry})";
    }

    void GeneratePirateIdentity(string[] origins)
    {
        string prefix = PiratePrefixes[rng.Next(PiratePrefixes.Length)];
        originCountry = origins[rng.Next(origins.Length)];
        destinationCountry = ""; // Pirates don't have destinations

        // Pirates get intimidating or generic names
        string[] pirateNames = { "Shadow", "Reaper", "Ghost", "Viper", "Shark", "Wolf", "Black", "Storm" };
        string name = pirateNames[rng.Next(pirateNames.Length)];

        shipName = $"{prefix} {name}";
        fullTitle = $"{shipName}";
    }

    void GenerateNavyIdentity(string[] countries, string[] prefixes)
    {
        string navyPrefix = prefixes[rng.Next(prefixes.Length)];
        string name = ShipNames[rng.Next(ShipNames.Length)];
        originCountry = countries[rng.Next(countries.Length)];
        destinationCountry = "Patrol Route";

        shipName = $"{navyPrefix} {name}";
        fullTitle = $"{shipName} ({originCountry} Navy)";
    }

    /// <summary>
    /// Get a display string for the tooltip
    /// </summary>
    public string GetRouteString()
    {
        if (shipController == null || shipController.Data == null)
            return "";

        switch (shipController.Data.type)
        {
            case ShipType.Cargo:
                return $"{originCountry} → {destinationCountry}";
            case ShipType.Pirate:
                return $"From {originCountry}";
            case ShipType.Security:
                return $"Patrolling from {originCountry}";
            default:
                return "";
        }
    }
}