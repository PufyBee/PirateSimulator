using UnityEngine;

public static class AudioClipNames
{
    // ===== SFX =====
    public static class SFX
    {
        // UI Sounds
        public const string ButtonClick = "ButtonClick";
        public const string ButtonHover = "ButtonHover";
        public const string PanelOpen = "PanelOpen";
        public const string PanelClose = "PanelClose";
        public const string Error = "Error";
        public const string Confirm = "Confirm";
        public const string Cancel = "Cancel";

        // Ship Sounds
        public const string ShipSpawn = "ShipSpawn";
        public const string ShipSink = "ShipSink";
        public const string ShipCapture = "ShipCapture";

        // Pirate Sounds
        public const string PirateAlert = "PirateAlert";
        public const string PirateChase = "PirateChase";
        public const string PirateCapture = "PirateCapture";

        // Navy Sounds
        public const string NavyAlert = "NavyAlert";
        public const string NavyRespond = "NavyRespond";

        // Environment
        public const string Thunder = "Thunder";
        public const string Rain = "Rain";
        public const string Wind = "Wind";
        public const string Waves = "Waves";

        // Coastal Defense
        public const string MissileLaunch = "MissileLaunch";
        public const string MissileImpact = "MissileImpact";
        public const string LockOn = "LockOn";

        // UI Feedback
        public const string StartSim = "StartSim";
        public const string PauseSim = "PauseSim";
        public const string ResetSim = "ResetSim";
        public const string StepSim = "StepSim";
    }

    // ===== MUSIC =====
    public static class Music
    {
        public const string MainMenu = "MainMenu";
        public const string Setup = "SetupMusic";
        public const string Simulation = "Simulation";
        public const string Results = "ResultsMusic";
    }

    // ===== AMBIENT =====
    public static class Ambient
    {
        public const string OceanWaves = "OceanWaves";
        public const string Seagulls = "Seagulls";
        public const string Storm = "Storm";
        public const string Fog = "Fog";
        public const string NightCrickets = "NightCrickets";
        public const string Harbor = "Harbor";
    }
}