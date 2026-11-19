// ThunderFeet's checks.
//   S  speed: with ThunderFeet off, then every ground at 200 %, then at 50 %, sprint and jog the corridor and read BOTH the speed
//      the game computes (Terminal.m_testList["Player Speed"], written by Character.UpdateWalking when m_showTests is on) and the
//      speed the body actually moves (position over time). The ratios must follow the multiplier.
//   F  flicker: sprint up and down the corridor over natural ground; log every change of the RAW detection (with the collider,
//      slope and paint that caused it) and every change of the TRACKED surface; a tracked A -> B -> A blip under 0.5 s fails.
//   D  detection: a wood floor, a stone floor and a hoe-paved patch on the corridor; each must be read as itself.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using ThunderFeet;
using UnityEngine;

namespace ThunderFeetAutoTest
{
    public partial class AutoTest
    {
        // ------------------------------------------------------------------------------------------ driving the player

        static bool drive, driveRun, controlsPatched;

        /// <summary>What PlayerController would pass with W (and Shift) held: movedir is in look space (Player.SetControls).</summary>
        static void ControlsPre(Player __instance, ref Vector3 movedir, ref bool run, ref bool autoRun, ref bool crouch)
        {
            if (!drive || __instance == null || __instance != Player.m_localPlayer) return;
            movedir = Vector3.forward;
            run = driveRun;
            autoRun = false;
            crouch = false;
        }

        static void InstallControls()
        {
            if (controlsPatched) return;
            controlsPatched = true;
            Patch(typeof(Player), nameof(Player.SetControls),
                new[] { typeof(Vector3), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) },
                prefix: nameof(ControlsPre));
        }

        sealed class RunResult
        {
            public float Target, Actual, JogMul, SprintMul;
            public string Surfaces = "";
            public int Samples;
        }

        /// <summary>One straight run along the corridor: 1 s to get up to speed, then up to 1.6 s measured (less if the corridor
        /// ends first). Target = median of the game's computed speed over the measured part.</summary>
        static IEnumerator Measure(bool run, RunResult r)
        {
            Player p = Player.m_localPlayer;
            yield return PlaceAt(Origin, Fwd);
            drive = true;
            driveRun = run;
            float t0 = Time.time, ta = 0f;
            Vector3 a = Vector3.zero;
            bool started = false;
            var targets = new List<float>();
            var seen = new Dictionary<Surface, int>();
            float jm = 0f, sm = 0f;
            int n = 0;
            while (Time.time - t0 < 2.6f)
            {
                p.AddStamina(100f);
                Face(Fwd);
                if (Time.time - t0 >= 1.0f)
                {
                    if (!started) { started = true; a = p.transform.position; ta = Time.time; }
                    if (Terminal.m_testList.TryGetValue("Player Speed", out string s) && float.TryParse(s, NumberStyles.Float, IC, out float v))
                        targets.Add(v);
                    Surface cur = ThunderTracker.Current;
                    seen[cur] = seen.TryGetValue(cur, out int c) ? c + 1 : 1;
                    jm += ThunderTracker.JogMul; sm += ThunderTracker.SprintMul; n++;
                }
                if (Vector3.Dot(p.transform.position - Origin, Fwd) > CorridorLength - 1.5f) break;
                yield return null;
            }
            Vector3 b = p.transform.position;
            float dt = Time.time - ta;
            drive = false;
            var d = b - a;
            d.y = 0f;
            r.Actual = started && dt > 0.2f ? d.magnitude / dt : 0f;
            targets.Sort();
            r.Target = targets.Count > 0 ? targets[targets.Count / 2] : 0f;
            r.Samples = targets.Count;
            r.JogMul = n > 0 ? jm / n : 0f;
            r.SprintMul = n > 0 ? sm / n : 0f;
            r.Surfaces = string.Join(" ", seen.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}x{kv.Value}"));
            yield return Sec(0.4f);
        }

        static string Fmt(RunResult r) =>
            $"computed {r.Target.ToString("0.00", IC)} m/s ({r.Samples} samples), moved {r.Actual.ToString("0.00", IC)} m/s, " +
            $"tracker jog x{r.JogMul.ToString("0.00", IC)} sprint x{r.SprintMul.ToString("0.00", IC)}, on {r.Surfaces}";

        static void SetAll(int percent)
        {
            foreach (var e in ThunderConfig.s_sprint.Values) e.Value = percent;
            foreach (var e in ThunderConfig.s_jog.Values) e.Value = percent;
        }

        /// <summary>The values the run started with (a compat run's profile values, or the defaults), restored after each phase.</summary>
        static Dictionary<ConfigEntry<int>, int> s_saved;
        static bool s_savedEnabled;

        static void Snapshot()
        {
            if (s_saved != null) return;
            s_saved = new Dictionary<ConfigEntry<int>, int>();
            foreach (var e in ThunderConfig.s_sprint.Values.Concat(ThunderConfig.s_jog.Values)) s_saved[e] = e.Value;
            s_savedEnabled = ThunderConfig.Enabled.Value;
        }

        static void RestoreDefaults()
        {
            Snapshot();
            foreach (var kv in s_saved) kv.Key.Value = kv.Value;
            ThunderConfig.Enabled.Value = s_savedEnabled;
        }

        // ------------------------------------------------------------------------------------------ S: speed

        IEnumerator PhaseS()
        {
            Snapshot();
            InstallControls();
            Terminal.m_showTests = true;
            var res = new Dictionary<string, RunResult>();

            var plan = new (string label, bool enabled, int pct)[] { ("off", false, 0), ("200", true, 200), ("50", true, 50), ("restored", true, -1) };
            foreach (var (label, enabled, pct) in plan)
            {
                if (pct == -1) RestoreDefaults();
                else
                {
                    ThunderConfig.Enabled.Value = enabled;
                    if (pct > 0) SetAll(pct);
                }
                yield return Sec(0.5f);
                foreach (bool run in new[] { false, true })
                {
                    var r = new RunResult();
                    yield return Measure(run, r);
                    string key = $"{label}/{(run ? "sprint" : "jog")}";
                    res[key] = r;
                    Result("INFO", "run " + key, Fmt(r));
                }
            }
            RestoreDefaults();

            foreach (string gait in new[] { "jog", "sprint" })
            {
                RunResult off = res["off/" + gait], hi = res["200/" + gait], lo = res["50/" + gait];
                if (off.Target <= 0.1f || off.Actual <= 0.1f)
                {
                    Result("FAIL", $"{gait}: baseline", $"no baseline movement measured ({Fmt(off)})");
                    continue;
                }
                float th = hi.Target / off.Target, tl = lo.Target / off.Target;
                float ah = hi.Actual / off.Actual, al = lo.Actual / off.Actual;
                Check(Mathf.Abs(th - 2f) < 0.16f && Mathf.Abs(tl - 0.5f) < 0.06f, $"{gait}: game's computed speed follows the multiplier",
                    $"200% -> x{th.ToString("0.00", IC)}, 50% -> x{tl.ToString("0.00", IC)} (want x2.00 and x0.50)");
                Check(ah > 1.7f && al < 0.62f && al > 0.38f, $"{gait}: the body moves at it",
                    $"200% -> x{ah.ToString("0.00", IC)}, 50% -> x{al.ToString("0.00", IC)} of the {off.Actual.ToString("0.00", IC)} m/s baseline");
            }
            RunResult dj = res["restored/jog"], oj = res["off/jog"];
            Result("INFO", "start values on this ground", $"jog moved x{(oj.Actual > 0 ? dj.Actual / oj.Actual : 0).ToString("0.00", IC)} of vanilla; {Fmt(dj)}");
            yield break;
        }

        // ------------------------------------------------------------------------------------------ F: flicker

        IEnumerator PhaseF()
        {
            InstallControls();
            Player p = Player.m_localPlayer;
            yield return PlaceAt(Origin, Fwd);
            string logPath = Path.Combine(Out, "flicker.log");
            File.WriteAllText(logPath, "t_s\tgrounded\traw\ttracked\tcollider\tlayer\tslope_deg\tbiome\tpaint_rgba\n");

            drive = true;
            driveRun = true;
            Vector3 dir = Fwd;
            float t0 = Time.time;
            Surface lastRaw = Surface.None;
            var tracked = new List<(float t, Surface s)>();
            var rawSeq = new List<(float t, Surface s)>();
            var rawCounts = new Dictionary<Surface, int>();
            var causes = new List<string>();
            while (Time.time - t0 < 16f)
            {
                p.AddStamina(100f);
                float along = Vector3.Dot(p.transform.position - Origin, Fwd);
                if (dir == Fwd && along > CorridorLength - 2.5f) dir = -Fwd;
                else if (dir == -Fwd && along < 2.5f) dir = Fwd;
                Face(dir);

                float t = Time.time - t0;
                bool grounded = p.IsOnGround();
                Surface raw = grounded ? SurfaceDetector.Detect(p) : lastRaw;
                rawCounts[raw] = rawCounts.TryGetValue(raw, out int rc) ? rc + 1 : 1;
                if (raw != lastRaw)
                {
                    rawSeq.Add((t, raw));
                    string line = Describe(p, t, grounded, raw);
                    File.AppendAllText(logPath, line + "\n");
                    if (causes.Count < 12 && lastRaw != Surface.None) causes.Add($"{lastRaw}->{raw} [{line.Split('\t').Skip(4).Take(4).Aggregate((x, y) => x + " " + y)}]");
                    lastRaw = raw;
                }
                Surface cur = ThunderTracker.Current;
                if (tracked.Count == 0 || tracked[tracked.Count - 1].s != cur) tracked.Add((t, cur));
                yield return null;
            }
            drive = false;

            int rawBlips = Blips(rawSeq), trackedBlips = Blips(tracked);
            Result("INFO", "raw detection", $"{rawSeq.Count - 1} changes, {rawBlips} blips under 0.5 s; frames: " +
                string.Join(" ", rawCounts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}x{kv.Value}")));
            if (causes.Count > 0) Result("INFO", "raw changes (first 12)", string.Join(" || ", causes));
            Check(trackedBlips == 0, "tracked surface holds steady",
                $"{tracked.Count - 1} changes, {trackedBlips} blips under 0.5 s: " + string.Join(" ", tracked.Select(x => $"{x.t.ToString("0.0", IC)}s:{x.s}")));
            yield break;
        }

        /// <summary>A -> B -> A where B lasted under 0.5 s.</summary>
        static int Blips(List<(float t, Surface s)> seq)
        {
            int n = 0;
            for (int i = 1; i + 1 < seq.Count; i++)
                if (seq[i - 1].s == seq[i + 1].s && seq[i + 1].t - seq[i].t < 0.5f) n++;
            return n;
        }

        static string Describe(Player p, float t, bool grounded, Surface raw)
        {
            Collider c = p.GetLastGroundCollider();
            Vector3 n = p.GetLastGroundNormal();
            float slope = Mathf.Acos(Mathf.Clamp01(n.y)) * Mathf.Rad2Deg;
            string paint = "-", biome = "-";
            Heightmap hm = c != null ? c.GetComponent<Heightmap>() : null;
            if (hm != null)
            {
                Vector3 q = p.transform.position;
                biome = hm.GetBiome(q).ToString();
                q.x -= 0.5f; q.z -= 0.5f;
                Color col = hm.GetPaintMask(q);
                paint = $"{col.r.ToString("0.00", IC)},{col.g.ToString("0.00", IC)},{col.b.ToString("0.00", IC)},{col.a.ToString("0.00", IC)}";
            }
            string name = c != null ? (c.transform.root.name + "/" + c.name) : "(none)";
            string layer = c != null ? LayerMask.LayerToName(c.gameObject.layer) : "-";
            return $"{t.ToString("0.00", IC)}\t{grounded}\t{raw}\t{ThunderTracker.Current}\t{name}\t{layer}\t{slope.ToString("0.0", IC)}\t{biome}\t{paint}";
        }

        // ------------------------------------------------------------------------------------------ P: the loaded values on a stone floor

        /// <summary>The player's case: whatever percentages are loaded (a compat run: the profile's own), on a 28 m strip of
        /// stone_floor_2x2 laid along the corridor, against vanilla (ThunderFeet off) on the same strip.</summary>
        IEnumerator PhaseP()
        {
            Snapshot();
            RestoreDefaults();
            InstallControls();
            Terminal.m_showTests = true;
            Quaternion rot = Quaternion.LookRotation(Fwd);
            var strip = new List<GameObject>();
            for (float at = 1f; at < CorridorLength; at += 2f)
            {
                GameObject go = PlacePiece("stone_floor_2x2", OnGround(Origin + Fwd * at) + Vector3.up * 0.05f, rot);
                if (go != null) strip.Add(go);
            }
            Check(strip.Count >= 12, "stone floor strip", $"{strip.Count} stone_floor_2x2 pieces along {CorridorLength:0} m");
            yield return Sec(2f);

            float wantJ = ThunderConfig.JogMultiplier(Surface.BuiltStone), wantS = ThunderConfig.SprintMultiplier(Surface.BuiltStone);
            ThunderConfig.Enabled.Value = false;
            yield return Sec(0.5f);
            RunResult offJ = new RunResult(), offS = new RunResult();
            yield return Measure(false, offJ);
            yield return Measure(true, offS);
            RestoreDefaults();
            yield return Sec(0.5f);
            RunResult onJ = new RunResult(), onS = new RunResult();
            yield return Measure(false, onJ);
            yield return Measure(true, onS);
            Result("INFO", "strip, ThunderFeet off", $"jog: {Fmt(offJ)} || sprint: {Fmt(offS)}");
            Result("INFO", "strip, loaded values", $"jog: {Fmt(onJ)} || sprint: {Fmt(onS)}");
            if (offJ.Actual > 0.1f && offS.Actual > 0.1f)
            {
                float rj = onJ.Actual / offJ.Actual, rs = onS.Actual / offS.Actual;
                Check(Mathf.Abs(rj - wantJ) < 0.06f, "jog on the stone floor", $"moved x{rj.ToString("0.00", IC)} of vanilla; Built stone Jog Walk Speed % = {wantJ * 100f:0}");
                Check(Mathf.Abs(rs - wantS) < 0.06f, "sprint on the stone floor", $"moved x{rs.ToString("0.00", IC)} of vanilla; Built stone Sprint Speed % = {wantS * 100f:0}");
            }
            else Result("FAIL", "strip baseline", "no movement measured with ThunderFeet off");

            foreach (GameObject go in strip)
            {
                var nv = go != null ? go.GetComponent<ZNetView>() : null;
                if (nv != null && nv.IsValid()) ZNetScene.instance.Destroy(go);
            }
            yield return Sec(1f);
        }

        // ------------------------------------------------------------------------------------------ D: detection

        static GameObject HoePiece(string name)
        {
            var hoe = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Hoe") : null;
            var pieces = hoe != null ? hoe.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces?.m_pieces : null;
            return pieces?.FirstOrDefault(g => g != null && g.name == name);
        }

        IEnumerator PhaseD()
        {
            var spots = new (string what, float at, Surface want)[]
            {
                ("wood_floor", 7f, Surface.BuiltWood),
                ("stone_floor_2x2", 13f, Surface.BuiltStone),
                ("paved_road_v2", 19f, Surface.PavedRoad),
            };
            Quaternion rot = Quaternion.LookRotation(Fwd);
            foreach (var (what, at, _) in spots)
            {
                Vector3 pos = OnGround(Origin + Fwd * at);
                if (what == "paved_road_v2")
                {
                    // The hoe's operations are not ZNetScene prefabs: they live in the Hoe's build-piece table (FACTS section 4).
                    // Instantiating one applies its TerrainOp in Awake and destroys itself. Paint a short strip of it.
                    GameObject op = HoePiece(what);
                    Check(op != null, $"place {what}", op != null ? $"hoe op found; paving 3 patches at {V(pos)}" : "not in the Hoe's build pieces");
                    if (op != null)
                        for (int i = -1; i <= 1; i++) UnityEngine.Object.Instantiate(op, OnGround(pos + Fwd * (i * 1.5f)), rot);
                    continue;
                }
                GameObject go = PlacePiece(what, pos + Vector3.up * 0.05f, rot);
                Check(go != null, $"place {what}", go != null ? $"at {V(pos)}" : "prefab missing");
            }
            yield return Sec(2.5f);   // the paving regenerates the terrain mesh

            int solid = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            foreach (var (what, at, want) in spots)
            {
                Vector3 pos = Origin + Fwd * at;
                if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, solid, QueryTriggerInteraction.Ignore))
                    pos = hit.point;
                yield return PlaceAt(pos, Fwd);
                Player.m_localPlayer.transform.position = pos + Vector3.up * 0.15f;
                yield return Sec(1.5f);
                Surface got = ThunderTracker.Current;
                Surface raw = SurfaceDetector.Detect(Player.m_localPlayer);
                Check(got == want, $"on {what}", $"tracked {got}, raw {raw}, want {want}; jog x{ThunderTracker.JogMul.ToString("0.00", IC)} sprint x{ThunderTracker.SprintMul.ToString("0.00", IC)}");
            }
            yield break;
        }
    }
}
