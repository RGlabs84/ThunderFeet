using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ThunderFeet
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "wubarrk.thunderfeet";
        public const string Name = "ThunderFeet";
        public const string Version = "0.1.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        private readonly Harmony _harmony = new Harmony(Guid);

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ThunderConfig.Init(Config);
            ThunderSync.Init(Config);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo($"{Name} {Version} loaded: {SurfaceInfo.All.Length} surfaces, icons from {SurfaceIcons.IconDir}");
        }

        private void Update()
        {
            ThunderSync.Tick();
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
    }
}
