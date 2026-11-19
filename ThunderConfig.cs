using System.Collections.Generic;
using BepInEx.Configuration;

namespace ThunderFeet
{
    internal enum IconMode
    {
        Always,
        OnlyWhenSpeedChanges,
        Never,
    }

    internal enum RuneMode
    {
        Everyone,
        OnlyMine,
        Off,
    }

    /// <summary>
    /// All settings. Gameplay settings are synced and owned by the server (ThunderSync); the "Display" section is
    /// each player's own. Speeds are percentages: 100 = vanilla, 115 = 15% faster (1.15x), 80 = 20% slower (0.8x).
    /// </summary>
    internal static class ThunderConfig
    {
        internal const string GeneralSection = "00 General";
        internal const string DisplaySection = "00 Display (local)";

        internal static ConfigEntry<bool> LockConfiguration;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> RockSlopeAngle;
        internal static ConfigEntry<float> PaintThreshold;
        internal static ConfigEntry<float> SurfaceChangeDelay;

        internal static ConfigEntry<bool> RunesAllowed;

        internal static ConfigEntry<IconMode> ShowIcon;
        internal static ConfigEntry<RuneMode> RuneMode;
        internal static ConfigEntry<float> RuneIntensity;
        internal static ConfigEntry<bool> IconShowsPercent;
        internal static ConfigEntry<bool> LogSurfaceChanges;

        private static readonly Dictionary<Surface, ConfigEntry<int>> s_sprint = new Dictionary<Surface, ConfigEntry<int>>();
        private static readonly Dictionary<Surface, ConfigEntry<int>> s_jog = new Dictionary<Surface, ConfigEntry<int>>();

        private static readonly AcceptableValueRange<int> PercentRange = new AcceptableValueRange<int>(10, 300);

        internal static void Init(ConfigFile cfg)
        {
            LockConfiguration = cfg.Bind(GeneralSection, "Lock Configuration", true,
                "SERVER: when on, only the server and admins (adminlist.txt) can change the synced settings. Players always use the server's values while connected.");
            Enabled = cfg.Bind(GeneralSection, "Enabled", true,
                "SERVER: master switch. Off = every surface is vanilla speed.");
            RockSlopeAngle = cfg.Bind(GeneralSection, "Rock Slope Angle", 35f,
                new ConfigDescription("SERVER: unpainted terrain steeper than this (degrees) counts as 'Rock slope'. 35 is where the game starts drawing rock.",
                    new AcceptableValueRange<float>(15f, 80f)));
            PaintThreshold = cfg.Bind(GeneralSection, "Paint Threshold", 0.5f,
                new ConfigDescription("SERVER: how strongly terrain must be painted (paved / dirt / cultivated) to count. 0.5 matches the game's own 'is cleared' test; lower catches the soft edges of roads.",
                    new AcceptableValueRange<float>(0.05f, 0.95f)));

            SurfaceChangeDelay = cfg.Bind(GeneralSection, "Surface Change Delay", 0.35f,
                new ConfigDescription("SERVER: seconds a new ground must be read steadily before the speed and the icon switch to it, so a momentary misread never shows. 0 = instant.",
                    new AcceptableValueRange<float>(0f, 2f)));
            RunesAllowed = cfg.Bind(GeneralSection, "Rune Footsteps Allowed", true,
                "SERVER: allow the glowing rune burst on footsteps. Off = nobody sees runes, whatever their own setting.");

            ShowIcon = cfg.Bind(DisplaySection, "Show Surface Icon", IconMode.Always,
                "LOCAL: show a status-effect icon for the surface under your feet.");
            IconShowsPercent = cfg.Bind(DisplaySection, "Icon Shows Percent", true,
                "LOCAL: print the speed change (sprint / jog) on the icon.");
            RuneMode = cfg.Bind(DisplaySection, "Rune Footsteps", ThunderFeet.RuneMode.Everyone,
                "LOCAL: a small burst of glowing runes, tinted by the ground, on every footstep (a bigger ring when landing). Everyone / only your own / off.");
            RuneIntensity = cfg.Bind(DisplaySection, "Rune Intensity", 1f,
                new ConfigDescription("LOCAL: how many runes per step and how big (1 = default).", new AcceptableValueRange<float>(0.25f, 3f)));
            LogSurfaceChanges = cfg.Bind(DisplaySection, "Log Surface Changes", false,
                "LOCAL: write every surface change to the BepInEx log (for tuning and bug reports).");

            foreach (SurfaceInfo info in SurfaceInfo.All)
            {
                s_sprint[info.Surface] = cfg.Bind(info.Section, "Sprint Speed %", info.DefaultSprint,
                    new ConfigDescription($"SERVER: sprint speed on {info.DisplayName} in percent (100 = vanilla). {info.Description}", PercentRange));
                s_jog[info.Surface] = cfg.Bind(info.Section, "Jog Walk Speed %", info.DefaultJog,
                    new ConfigDescription($"SERVER: jog, walk and sneak speed on {info.DisplayName} in percent (100 = vanilla).", PercentRange));
            }
        }

        internal static bool IsLocal(ConfigDefinition d) => d.Section == DisplaySection;

        internal static float SprintMultiplier(Surface s) =>
            s != Surface.None && Enabled.Value && s_sprint.TryGetValue(s, out var e) ? e.Value / 100f : 1f;

        internal static float JogMultiplier(Surface s) =>
            s != Surface.None && Enabled.Value && s_jog.TryGetValue(s, out var e) ? e.Value / 100f : 1f;
    }
}
