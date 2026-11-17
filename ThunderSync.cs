using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using ServerSync;

namespace ThunderFeet
{
    // SERVER AUTHORITY (ServerSync, libs-Tools/ServerSync.cs, compiled in):
    //   - Every gameplay setting is the server's. A client that joins takes the server's values while connected; its own file is untouched
    //     and comes back at disconnect.
    //   - "Lock Configuration" (on by default): only the server, or an admin from adminlist.txt, can change a synced value. An admin's
    //     in-game change (Configuration Manager) is sent to the server and on to every player.
    //   - The "Display (local)" section is each player's own (icon on/off, log).
    //   - The server's cfg is watched: an edit on disk is read again and reaches every client within a second.
    internal static class ThunderSync
    {
        internal static readonly ConfigSync Instance = new ConfigSync(Plugin.Guid)
        {
            DisplayName = Plugin.Name,
            CurrentVersion = Plugin.Version,
            MinimumRequiredVersion = Plugin.Version,
            ModRequired = false,
        };

        private static ConfigFile s_cfg;
        private static FileSystemWatcher s_watcher;
        private static volatile bool s_changed;
        private static DateTime s_changedAt;

        // after every setting is bound (ThunderConfig.Init)
        internal static void Init(ConfigFile cfg)
        {
            s_cfg = cfg;
            Instance.AddLockingConfigEntry(ThunderConfig.LockConfiguration);

            var add = typeof(ConfigSync).GetMethod(nameof(ConfigSync.AddConfigEntry));
            int synced = 1, local = 0;
            foreach (ConfigDefinition d in new List<ConfigDefinition>(cfg.Keys))
            {
                ConfigEntryBase e = cfg[d];
                if (e == ThunderConfig.LockConfiguration) continue;
                if (ThunderConfig.IsLocal(d)) { local++; continue; }
                add.MakeGenericMethod(e.SettingType).Invoke(Instance, new object[] { e });
                synced++;
            }
            Plugin.Log.LogInfo($"[sync] {synced} settings follow the server (locked to admins while 'Lock Configuration' is on), {local} stay on this machine");

            try
            {
                s_watcher = new FileSystemWatcher(Path.GetDirectoryName(cfg.ConfigFilePath), Path.GetFileName(cfg.ConfigFilePath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };
                s_watcher.Changed += OnFile;
                s_watcher.Created += OnFile;
                s_watcher.Renamed += OnFile;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[sync] the cfg is not watched ({e.Message}): edits are read at the next start"); }
        }

        private static void OnFile(object sender, FileSystemEventArgs e) { s_changedAt = DateTime.UtcNow; s_changed = true; }

        // Main thread, from Plugin.Update: half a second after the last write, the source of truth (a dedicated server, a host, or a
        // client that has not joined) reads its file again. A joined client never does, so the server's values stay.
        internal static void Tick()
        {
            if (!s_changed || (DateTime.UtcNow - s_changedAt).TotalSeconds < 0.5) return;
            s_changed = false;
            if (s_cfg == null || !File.Exists(s_cfg.ConfigFilePath) || !Instance.IsSourceOfTruth) return;
            try
            {
                s_cfg.Reload();
                Plugin.Log.LogInfo("[sync] the cfg changed on disk and was read again" +
                                   (ZNet.instance != null && ZNet.instance.IsServer() ? ": players get the new values" : ""));
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[sync] the cfg could not be read again: {e.Message}"); }
        }
    }
}
