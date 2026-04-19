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

    [Header("=== REGIONAL DATA (Random per ship) ===")]
    public string regionalHotspotName;
    public string regionalHotspotDescription;
    public string regionalTacticName;
    public string regionalPortName;
    public string regionalCargoType;
    public string regionalNavyForce1;
    public string regionalNavyForce2;
    public string regionalProtectedPort1;
    public string regionalProtectedPort2;


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

    // Direction; for use in determining origin/destination countries
    private int direction = -1;

    // Countries by region and role
    public static class Regions
    {
        // Strait of Malacca
        public static readonly string[] MalaccaMerchantsLeft = { "India", "India", "India", "India", "Sri Lanka", "Sri Lanka", "United Arab Emirates", "United Arab Emirates", "Saudi Arabia", "Saudi Arabia", "Qatar", "Kuwait", "Oman", "Pakistan", "Bangladesh", "Egypt", "Egypt", "Kenya", "Tanzania", "South Africa", "Yemen", "Iran", "Iraq", "Bahrain", "Djibouti", "Mozambique", "Somalia" };
        public static readonly string[] MalaccaMerchantsRight = { "China", "China", "China", "China", "Japan", "Japan", "Japan", "South Korea", "South Korea", "Taiwan", "Taiwan", "Philippines", "Philippines", "Australia", "Australia", "United States", "United States", "Canada", "Russia", "Hong Kong", "Hong Kong", "Brunei", "New Zealand", "South Korea", "Japan" };
        public static readonly string[] MalaccaPirates = { "Indonesian Waters", "Riau Islands", "Batam" };
        public static readonly string[] MalaccaNavy = { "Singapore", "Malaysia", "Indonesia" };
        public static readonly string[] MalaccaNavyNames = { "RSN", "RMN", "KRI" }; // Republic of Singapore Navy, Royal Malaysian Navy, Indonesian Navy

        // Gulf of Aden
        public static readonly string[] AdenMerchantsLeft = { "United Kingdom", "United Kingdom", "Netherlands", "Netherlands", "Germany", "Germany", "France", "France", "Italy", "Italy", "Spain", "Spain", "Belgium", "Greece", "Turkey", "Egypt", "Egypt", "Egypt", "Turkey", "Greece", "France", "Germany", "Italy", "United Kingdom", "Spain", "Belgium", "Russia", "United Kingdom","United Kingdom","Germany","Netherlands","Greece","United States","United States","United States","France","France","Spain","Spain","Turkey","Turkey","Belgium","Norway"};
        public static readonly string[] AdenMerchantsRight = { "China", "China", "China", "China", "Japan", "Japan", "South Korea", "South Korea", "India", "India", "Singapore", "Malaysia", "Indonesia", "Philippines", "Taiwan", "Bangladesh", "Sri Lanka", "Australia", "Australia", "United Arab Emirates", "Russia", "United Arab Emirates", "Saudi Arabia", "Qatar", "Kuwait", "Oman", "China","China","China","China","China","China","India","India","India","India","India","Singapore","Singapore","Singapore","Singapore","United Arab Emirates","United Arab Emirates","United Arab Emirates","Saudi Arabia","Saudi Arabia","Saudi Arabia","South Korea","South Korea","Japan","Japan","Indonesia","Indonesia","Malaysia","Malaysia","Iran" };
        public static readonly string[] AdenPirates = { "Somali Waters", "Puntland Coast", "Mogadishu" };
        public static readonly string[] AdenNavy = { "Djibouti", "Yemen", "International Coalition" };
        public static readonly string[] AdenNavyNames = { "Combined Task Force", "EUNAVFOR", "Naval Patrol" };

        // Gulf of Guinea
        public static readonly string[] GuineaMerchantsLeft = { "Ghana", "Ghana", "Cameroon", "Cameroon", "Ivory Coast", "Ivory Coast", "South Africa", "South Africa", "Brazil", "Brazil", "United States", "United States", "United Kingdom", "France", "Netherlands", "Spain", "Brazil","Brazil","Brazil","Brazil","Brazil","United States","United States","United States","United States","United Kingdom","United Kingdom","United Kingdom","Netherlands","Netherlands","Netherlands","France","France","France","Spain","Spain","Portugal","Portugal","Germany","Germany", "Ivory Coast", "Morocco", "Morocco" };
        public static readonly string[] GuineaMerchantsRight = { "Angola","Angola","South Africa","South Africa","Cameroon","Gabon", "South Africa", "South Africa", "Mozambique", "Madagascar", "Madagascar", "Namibia", "Kenya", "Kenya", "Tanzania", "China", "China", "China", "Japan", "South Korea", "Indonesia", "United Arab Emirates" };
        public static readonly string[] GuineaPirates = { "Nigerian Waters", "Niger Delta", "Cameroon Coast" };
        public static readonly string[] GuineaNavy = { "Nigeria", "Cameroon", "Ghana" };
        public static readonly string[] GuineaNavyNames = { "NNS", "Cameroon Navy", "Ghana Navy" }; // Nigerian Navy Ship
    }

    public enum MapRegion
    {
        GulfOfGuinea = 0,
        StraitOfMalacca = 1,
        GulfOfAden = 2
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
        string[] merchantCountriesLeft, merchantCountriesRight, pirateOrigins, navyCountries, navyPrefixes;
        GetRegionData(out merchantCountriesLeft, out merchantCountriesRight, out pirateOrigins, out navyCountries, out navyPrefixes);

        // Generate identity based on ship type
        switch (type)
        {
            case ShipType.Cargo:
                GenerateMerchantIdentity(merchantCountriesLeft, merchantCountriesRight);
                // GenerateMerchantIdentity(merchantCountries);
                GenerateMerchantRegionalData();
                break;
            case ShipType.Pirate:
                GeneratePirateIdentity(pirateOrigins);
                GeneratePirateRegionalData(); 
                break;
            case ShipType.Security:
                GenerateNavyIdentity(navyCountries, navyPrefixes);
                GenerateNavyRegionalData();
                break;
        }
    }

    void GetRegionData(out string[] merchantsLeft, out string[] merchantsRight, out string[] pirates, out string[] navy, out string[] navyNames)
    {
        switch (CurrentRegion)
        {
            case MapRegion.GulfOfAden:
                merchantsLeft = Regions.AdenMerchantsLeft;
                merchantsRight = Regions.AdenMerchantsRight;
                pirates = Regions.AdenPirates;
                navy = Regions.AdenNavy;
                navyNames = Regions.AdenNavyNames;
                break;
            case MapRegion.GulfOfGuinea:
                merchantsLeft = Regions.GuineaMerchantsLeft;
                merchantsRight = Regions.GuineaMerchantsRight;
                pirates = Regions.GuineaPirates;
                navy = Regions.GuineaNavy;
                navyNames = Regions.GuineaNavyNames;
                break;
            case MapRegion.StraitOfMalacca:
            default:
                merchantsLeft = Regions.MalaccaMerchantsLeft;
                merchantsRight = Regions.MalaccaMerchantsRight;
                pirates = Regions.MalaccaPirates;
                navy = Regions.MalaccaNavy;
                navyNames = Regions.MalaccaNavyNames;
                break;
        }
    }

    private void GenerateMerchantRegionalData()
    {        
        switch (CurrentRegion)
        {
            case MapRegion.StraitOfMalacca:
                regionalPortName = RegionalData.Malacca.Ports[Random.Range(0, RegionalData.Malacca.Ports.Length)];
                regionalCargoType = RegionalData.Malacca.CargoTypes[Random.Range(0, RegionalData.Malacca.CargoTypes.Length)];
                break;

            case MapRegion.GulfOfAden:
                regionalPortName = RegionalData.Aden.Ports[Random.Range(0, RegionalData.Aden.Ports.Length)];
                regionalCargoType = "Container ship, Bulk carrier"; // Generic fallback
                break;

            case MapRegion.GulfOfGuinea:
                regionalPortName = RegionalData.Guinea.Ports[Random.Range(0, RegionalData.Guinea.Ports.Length)];
                regionalCargoType = "Oil tanker";
                break;
        }
    }

    private void GeneratePirateRegionalData()
    {
        switch (CurrentRegion)
        {
            case MapRegion.StraitOfMalacca:
                var malaccaHotspot = RegionalData.Malacca.Hotspots[Random.Range(0, RegionalData.Malacca.Hotspots.Length)];
                regionalHotspotName = malaccaHotspot.name;
                regionalHotspotDescription = malaccaHotspot.description;
                break;

            case MapRegion.GulfOfAden:
                var adenHotspot = RegionalData.Aden.Hotspots[Random.Range(0, RegionalData.Aden.Hotspots.Length)];
                var adenTactic = RegionalData.Aden.Tactics[Random.Range(0, RegionalData.Aden.Tactics.Length)];
                regionalHotspotName = adenHotspot.name;
                regionalHotspotDescription = adenHotspot.description;
                regionalTacticName = adenTactic.name;
                break;

            case MapRegion.GulfOfGuinea:
                var guineaHotspot = RegionalData.Guinea.Hotspots[Random.Range(0, RegionalData.Guinea.Hotspots.Length)];
                var guineaTactic = RegionalData.Guinea.Tactics[Random.Range(0, RegionalData.Guinea.Tactics.Length)];
                regionalHotspotName = guineaHotspot.name;
                regionalHotspotDescription = guineaHotspot.description;
                regionalTacticName = guineaTactic.name;
                break;
        }
    }

    private void GenerateNavyRegionalData()
    {
        switch (CurrentRegion)
        {
            case MapRegion.StraitOfMalacca:
                regionalNavyForce1 = RegionalData.Malacca.NavalForces[Random.Range(0, RegionalData.Malacca.NavalForces.Length)].name;
                regionalNavyForce2 = RegionalData.Malacca.NavalForces[Random.Range(0, RegionalData.Malacca.NavalForces.Length)].name;
                regionalProtectedPort1 = RegionalData.Malacca.Ports[Random.Range(0, RegionalData.Malacca.Ports.Length)];
                regionalProtectedPort2 = RegionalData.Malacca.Ports[Random.Range(0, RegionalData.Malacca.Ports.Length)];
                break;

            case MapRegion.GulfOfAden:
                regionalNavyForce1 = RegionalData.Aden.NavalForces[Random.Range(0, RegionalData.Aden.NavalForces.Length)].name;
                regionalNavyForce2 = RegionalData.Aden.NavalForces[Random.Range(0, RegionalData.Aden.NavalForces.Length)].name;
                regionalProtectedPort1 = RegionalData.Aden.Ports[Random.Range(0, RegionalData.Aden.Ports.Length)];
                regionalProtectedPort2 = RegionalData.Aden.Ports[Random.Range(0, RegionalData.Aden.Ports.Length)];
                break;

            case MapRegion.GulfOfGuinea:
                regionalNavyForce1 = RegionalData.Guinea.NavalForces[Random.Range(0, RegionalData.Guinea.NavalForces.Length)].name;
                regionalNavyForce2 = RegionalData.Guinea.NavalForces[Random.Range(0, RegionalData.Guinea.NavalForces.Length)].name;
                regionalProtectedPort1 = RegionalData.Guinea.Ports[Random.Range(0, RegionalData.Guinea.Ports.Length)];
                regionalProtectedPort2 = RegionalData.Guinea.Ports[Random.Range(0, RegionalData.Guinea.Ports.Length)];
                break;
        }
    }
    
    public string GetDetailedRouteString()
    {
        if (shipController == null || shipController.Data == null) return "";

        switch (shipController.Data.type)
        {
            case ShipType.Cargo:
                return $"{originCountry} → {destinationCountry}\n" +
                       $"Route: {GetShippingRoute()}";
            case ShipType.Pirate:
                return $"Operating from: {originCountry}\n" +
                       $"Tactics: {GetPirateTactics()}";
            case ShipType.Security:
                return $"Patrol Area: {originCountry} waters\n" +
                       $"Mission: {GetNavyMission()}";
            default:
                return "";
        }
    }

    private string GetShippingRoute()
    {
        return CurrentRegion switch
        {
            MapRegion.StraitOfMalacca => "Through Malacca Strait (draft restriction 20m)",
            MapRegion.GulfOfAden => "Via Internationally Recommended Transit Corridor (IRTC)",
            MapRegion.GulfOfGuinea => "Coastal transit - High Risk Area",
            _ => "Open ocean"
        };
    }

    private string GetPirateTactics()
    {
        return CurrentRegion switch
        {
            MapRegion.StraitOfMalacca => "Boarding from fishing vessels, night attacks",
            MapRegion.GulfOfAden => "Mother ships, skiffs, RPG attacks",
            MapRegion.GulfOfGuinea => "Kidnapping for ransom, product theft",
            _ => "Unknown tactics"
        };
    }

    private string GetNavyMission()
    {
        return CurrentRegion switch
        {
            MapRegion.StraitOfMalacca => "MALSINDO coordinated patrols",
            MapRegion.GulfOfAden => "EUNAVFOR Atalanta / CTF-151 counter-piracy",
            MapRegion.GulfOfGuinea => "Yaoundé Code of Conduct implementation",
            _ => "Maritime security"
        };
    }

    public int GetDirection()
    {
        if (shipController == null || shipController.Data == null) {
            return -1;
        }

        float x = shipController.Data.position.x;
        if (x > -200f) {
            return 1; // right
        }
        if (x < -200f) {
            return 0; // left
        }

        return -1;
    }

    void GenerateMerchantIdentity(string[] countriesLeft, string[] countriesRight)
    {
        string prefix = MerchantPrefixes[rng.Next(MerchantPrefixes.Length)];
        string name = ShipNames[rng.Next(ShipNames.Length)];
        
        while (direction == -1 && string.IsNullOrEmpty(originCountry))
        {
            direction = GetDirection();
        }
        if (direction == 0 && string.IsNullOrEmpty(originCountry)) {
            originCountry = countriesLeft[rng.Next(countriesLeft.Length)];
            destinationCountry = countriesRight[rng.Next(countriesRight.Length)];
        }
        else if (direction == 1 && string.IsNullOrEmpty(originCountry)) {
            originCountry = countriesRight[rng.Next(countriesRight.Length)];
            destinationCountry = countriesLeft[rng.Next(countriesLeft.Length)];
        }

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