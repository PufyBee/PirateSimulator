using System.Collections.Generic;
using UnityEngine;

public static class RegionalData
{  
    public static class Malacca
    {
        public static readonly string[] Ports = new string[]
        {
            "Port Klang (Malaysia)", "Singapore", "Tanjung Pelepas (Malaysia)",
            "Belawan (Indonesia)", "Penang (Malaysia)", "Melaka (Malaysia)",
            "Dumai (Indonesia)", "Port of Tanjung Priok (Indonesia)"
        };

        public static readonly PirateHotspot[] Hotspots = new PirateHotspot[]
        {
            new("One Fathom Bank", new Vector2(101.2f, 2.5f), "Frequent boardings 2000-2010"),
            new("Philips Channel", new Vector2(103.8f, 1.2f), "Singapore Strait, 15+ incidents"),
            new("Pulau Nipa", new Vector2(103.5f, 1.1f), "Indonesia-Singapore border"),
            new("Karimun Island", new Vector2(103.2f, 1.0f), "Anchorage attacks")
        };

        public static readonly string[] CargoTypes = new string[]
        {
            "Crude Oil (15M barrels/day)", "LNG (40% of global trade)",
            "Container ships (30M TEU/year)", "Palm Oil from Indonesia",
            "Rubber from Malaysia", "Electronics from Singapore"
        };

        public static readonly NavyForce[] NavalForces = new NavyForce[]
        {
            new("MALSINDO", "Malaysia-Singapore-Indonesia", "Coordinated patrols since 2004"),
            new("Eyes in the Sky", "Maritime air patrols", "Joint aerial surveillance"),
            new("ReCAAP", "Regional Cooperation Agreement", "Information sharing hub"),
            new("RMN", "Royal Malaysian Navy", "KD Lekiu-class frigates"),
            new("RSN", "Republic of Singapore Navy", "Formidable-class frigates"),
            new("TNI-AL", "Indonesian Navy", "Diponegoro-class corvettes")
        };

        public static readonly RegionStats Stats = new()
        {
            annualVesselTraffic = 90000,
            worldTradePercentage = 0.40f,
            cargoValueUSD = 1_000_000_000_000,
            peakPiracyYear = 2004,
            peakIncidents = 38,
            currentRiskLevel = "LOW-MODERATE",
            lastMajorIncident = "2023 - Attempted boarding"
        };
    }

    // ============================================================================
    // GULF OF ADEN - East Africa / Arabian Sea
    // ============================================================================
    public static class Aden
    {
      public static readonly string[] Ports = new string[]
        {
            "Djibouti", "Aden (Yemen)", "Berbera (Somaliland)", "Salalah (Oman)",
            "Mombasa (Kenya)", "Port Sudan", "Jeddah (Saudi Arabia)", "Hodeidah (Yemen)"
        };
      
        public static readonly PirateHotspot[] Hotspots = new PirateHotspot[]
        {
            new("Eyl", new Vector2(49.0f, 8.0f), "Pirate base 2008-2012"),
            new("Hobyo", new Vector2(48.5f, 5.5f), "Mother ship launch point"),
            new("Gara'ad", new Vector2(48.0f, 6.0f), "Hijacking staging area"),
            new("IRTC", new Vector2(55.0f, 12.0f), "Internationally Recommended Transit Corridor"),
            new("Bab el-Mandeb", new Vector2(43.0f, 12.5f), "Strait of Tears - choke point")
        };

        public static readonly PirateTactic[] Tactics = new PirateTactic[]
        {
            new("Mother Ships", "Large dhows carrying attack skiffs", 2008, 2014),
            new("Skiff Swarm", "Multiple small boats simultaneously", 2009, 2012),
            new("Ladder Boarding", "Hook ladders to climb high freeboard", 2008, 2013),
            new("RPG Warning Shots", "Rocket-propelled grenades to stop vessels", 2008, 2012)
        };

        public static readonly NavyForce[] NavalForces = new NavyForce[]
        {
            new("EUNAVFOR Atalanta", "European Union", "Operation since 2008, 20+ warships"),
            new("CTF-151", "Combined Maritime Forces", "25+ nations rotating command"),
            new("NATO SNMG", "NATO Standing Groups", "Periodic deployments"),
            new("IMSC", "International Maritime Security Construct", "Gulf of Oman patrols"),
            new("UKMTO", "UK Maritime Trade Operations", "Reporting hub Dubai"),
            new("MSCHOA", "Maritime Security Centre Horn of Africa", "EU monitoring")
        };

        public static readonly RegionStats Stats = new()
        {
            annualVesselTraffic = 20000,
            worldTradePercentage = 0.08f,
            cargoValueUSD = 500_000_000_000,
            peakPiracyYear = 2011,
            peakIncidents = 237,
            currentRiskLevel = "LOW",
            lastMajorIncident = "2019 - Dhow hijacking"
        };
    }

    // ============================================================================
    // GULF OF GUINEA - West Africa
    // ============================================================================
    public static class Guinea
    {
        public static readonly string[] Ports = new string[]
        {
            "Lagos (Nigeria)", "Tema (Ghana)", "Abidjan (Côte d'Ivoire)",
            "Douala (Cameroon)", "Libreville (Gabon)", "Malabo (Equatorial Guinea)",
            "Pointe-Noire (Congo)", "Luanda (Angola)"
        };

        public static readonly PirateHotspot[] Hotspots = new PirateHotspot[]
        {
            new("Bonny River", new Vector2(7.0f, 4.5f), "Nigeria - highest risk globally"),
            new("Brass", new Vector2(6.0f, 4.0f), "Oil terminal attacks"),
            new("Bayelsa Coast", new Vector2(5.5f, 4.0f), "Kidnapping hotspot"),
            new("Akwa Ibom", new Vector2(8.0f, 4.5f), "Product theft"),
            new("Cameroon Coast", new Vector2(8.5f, 3.5f), "Transnational attacks"),
            new("Annobon Zone", new Vector2(5.0f, 1.5f), "International waters attacks")
        };

        public static readonly PirateTactic[] Tactics = new PirateTactic[]
        {
            new("Kidnapping for Ransom", "Crew taken ashore, held in Niger Delta", 2020, 2024),
            new("Product Theft", "Siphoning oil from tankers", 2015, 2024),
            new("Armed Robbery", "At anchorages, stealing crew valuables", 2010, 2024),
            new("Hijacking", "Taking whole vessel to offload cargo", 2012, 2020)
        };

        public static readonly NavyForce[] NavalForces = new NavyForce[]
        {
            new("Yaoundé Code", "ECOWAS + ECCAS", "Regional cooperation framework 2013"),
            new("SHADE Guinea", "Shared Awareness and De-confliction", "International coordination"),
            new("Deep Blue Project", "Nigeria", "$195M maritime security initiative"),
            new("NNS", "Nigerian Navy", "Modernizing with new vessels"),
            new("CRESMAC", "Regional Maritime Security Centre", "Zone D monitoring"),
            new("G7++ FoGG", "Friends of Gulf of Guinea", "International donor support")
        };

        public static readonly RegionStats Stats = new()
        {
            annualVesselTraffic = 1500,
            worldTradePercentage = 0.04f,
            cargoValueUSD = 300_000_000_000,
            peakPiracyYear = 2020,
            peakIncidents = 130,
            currentRiskLevel = "HIGH",
            lastMajorIncident = "2024 - Tanker boarding"
        };
    }

    // ============================================================================
    // HISTORICAL TIMELINE OF PIRACY
    // ============================================================================
    public static class History
    {
        public static readonly TimelineEvent[] Events = new TimelineEvent[]
        {
            new(2005, "Malacca", "Peak Southeast Asian piracy, MALSINCO formed"),
            new(2008, "Aden", "First EUNAVFOR Atalanta deployment"),
            new(2009, "Aden", "Maersk Alabama hijacking - Captain Phillips incident"),
            new(2011, "Aden", "Peak Somali piracy: 237 attacks, $160M in ransoms"),
            new(2012, "Global", "BMP4 released - Best Management Practices"),
            new(2013, "Guinea", "Yaoundé Code of Conduct signed"),
            new(2015, "Global", "Armed guards reduce hijackings by 90%"),
            new(2018, "Guinea", "Nigeria's Deep Blue Project launches"),
            new(2020, "Guinea", "130+ seafarers kidnapped - highest globally"),
            new(2021, "Global", "IMB reports lowest piracy since 1994"),
            new(2023, "Global", "115 incidents worldwide (IMB annual report)"),
            new(2024, "Guinea", "Current hotspot - 43% of global kidnappings")
        };
    }

    // ============================================================================
    // SUPPORTING DATA STRUCTURES
    // ============================================================================

    [System.Serializable]
    public struct PirateHotspot
    {
        public string name;
        public Vector2 coordinates;
        public string description;
        public PirateHotspot(string name, Vector2 coordinates, string description)
        {
            this.name = name; this.coordinates = coordinates; this.description = description;
        }
    }

    [System.Serializable]
    public struct NavyForce
    {
        public string name;
        public string operator_;
        public string mission;
        public NavyForce(string name, string operator_, string mission)
        {
            this.name = name; this.operator_ = operator_; this.mission = mission;
        }
    }

    [System.Serializable]
    public struct PirateTactic
    {
        public string name;
        public string description;
        public int activeFrom;
        public int activeTo;
        public PirateTactic(string name, string description, int activeFrom, int activeTo)
        {
            this.name = name; this.description = description;
            this.activeFrom = activeFrom; this.activeTo = activeTo;
        }
    }

    [System.Serializable]
    public struct TimelineEvent
    {
        public int year;
        public string region;
        public string description;
        public TimelineEvent(int year, string region, string description)
        {
            this.year = year; this.region = region; this.description = description;
        }
    }

    [System.Serializable]
    public class RegionStats
    {
        public int annualVesselTraffic;
        public float worldTradePercentage;
        public long cargoValueUSD;
        public int peakPiracyYear;
        public int peakIncidents;
        public string currentRiskLevel;
        public string lastMajorIncident;

        public string GetTrafficString() => $"{annualVesselTraffic:N0} vessels/year";
        public string GetTradeString() => $"{worldTradePercentage:P0} of global trade";
        public string GetValueString() => $"${cargoValueUSD:N0}";
    }
}