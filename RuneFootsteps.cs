using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ThunderFeet
{
    /// <summary>
    /// A small burst of glowing runes on every footstep, tinted by the surface family, so walking leaves a rune trail. A landing
    /// gives a bigger ring burst.
    ///
    /// Taken from Wings of the Valkyrie (RuneWingVFX / ValkyrieRuneTextureGenerator / ValkyrieRuneVFX_Helper) and Njord (rune wake):
    ///   - one world-space ParticleSystem, emission off, manual Emit only, so standing still costs nothing;
    ///   - a procedural 4x4 atlas of 16 Elder-Futhark-style runes, drawn white and tinted per particle;
    ///   - an additive particle material found at runtime (no shipped shader or bundle).
    ///
    /// Hook: FootStep.RPC_Step. The owner of a character sends "Step" to everybody and every client plays the step effect there,
    /// so a postfix shows every player's runes on every client with no extra network traffic. A remote player's surface comes from
    /// a short raycast under the step point (this client does not track their ground contact).
    /// </summary>
    [HarmonyPatch(typeof(FootStep), "RPC_Step")]
    internal static class RuneFootsteps
    {
        private static readonly AccessTools.FieldRef<FootStep, Character> CharacterRef =
            AccessTools.FieldRefAccess<FootStep, Character>("m_character");

        private const float CullDistance = 40f;

        private static ParticleSystem s_ps;
        private static bool s_failed;

        private static void Postfix(FootStep __instance, int effectIndex, Vector3 point)
        {
            try
            {
                Emit(__instance, effectIndex, point);
            }
            catch (System.Exception e)
            {
                if (!s_failed) Plugin.Log.LogWarning($"[runes] footstep runes switched off after an error: {e}");
                s_failed = true;
            }
        }

        private static void Emit(FootStep step, int effectIndex, Vector3 point)
        {
            if (s_failed || !ThunderConfig.RunesAllowed.Value || ThunderConfig.RuneMode.Value == RuneMode.Off) return;

            Character character = CharacterRef(step);
            if (!(character is Player player)) return;

            bool mine = ReferenceEquals(player, Player.m_localPlayer);
            if (!mine && ThunderConfig.RuneMode.Value == RuneMode.OnlyMine) return;

            Camera cam = Utils.GetMainCamera();
            if (cam == null || (cam.transform.position - point).sqrMagnitude > CullDistance * CullDistance) return;

            // No runes under water (swimming, wading below the surface).
            if (ZoneSystem.instance != null && point.y < ZoneSystem.instance.m_waterLevel - 0.2f) return;

            Surface surface = mine ? ThunderTracker.Current : SurfaceDetector.DetectAt(point, player.InInterior());
            if (surface == Surface.None || surface == Surface.ShallowWater || surface == Surface.Tar) return;

            bool landing = IsLanding(step, effectIndex);
            Vector3 vel = player.GetVelocity();
            float speed = new Vector2(vel.x, vel.z).magnitude;

            float intensity = ThunderConfig.RuneIntensity.Value;
            int count = landing ? 10 : speed > 6f ? 4 : speed > 2.5f ? 2 : 1;
            count = Mathf.Clamp(Mathf.RoundToInt(count * intensity), 1, 24);

            // Faster ground glows brighter, slower ground dimmer (synced multipliers, so every client agrees).
            float boost = Mathf.Clamp(ThunderConfig.SprintMultiplier(surface), 0.6f, 1.4f);
            Color tint = GroupColor(SurfaceInfo.GroupOf(surface)) * Mathf.Lerp(0.55f, 1f, (boost - 0.6f) / 0.8f);
            tint.a = 1f;

            ParticleSystem ps = System();
            if (ps == null) return;

            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector2 disc = Random.insideUnitCircle;
                if (landing)
                {
                    Vector2 dir = disc.sqrMagnitude > 0.0001f ? disc.normalized : Vector2.right;
                    p.position = point + new Vector3(dir.x, 0f, dir.y) * 0.3f + Vector3.up * 0.05f;
                    p.velocity = new Vector3(dir.x, 0f, dir.y) * Random.Range(1.2f, 2.2f) + Vector3.up * Random.Range(0.4f, 0.9f);
                }
                else
                {
                    p.position = point + new Vector3(disc.x, 0f, disc.y) * 0.18f + Vector3.up * 0.05f;
                    p.velocity = new Vector3(disc.x * 0.25f, Random.Range(0.35f, 0.8f), disc.y * 0.25f);
                }
                p.startSize = Random.Range(0.12f, 0.22f) * Mathf.Lerp(1f, intensity, 0.5f) * (landing ? 1.3f : 1f);
                p.startLifetime = Random.Range(0.7f, 1.3f);
                p.rotation = Random.Range(-25f, 25f);
                p.startColor = tint;
                ps.Emit(p, 1);
            }
        }

        // A landing effect is one whose motion mask is Land only (FootStep.OnLand picks it with MotionType.Land).
        private static bool IsLanding(FootStep step, int effectIndex)
        {
            List<FootStep.StepEffect> effects = step.m_effects;
            if (effects == null || effectIndex < 0 || effectIndex >= effects.Count) return false;
            FootStep.MotionType m = effects[effectIndex].m_motionType;
            return (m & FootStep.MotionType.Land) != 0 &&
                   (m & (FootStep.MotionType.Jog | FootStep.MotionType.Run | FootStep.MotionType.Walk | FootStep.MotionType.Sneak)) == 0;
        }

        internal static Color GroupColor(IconGroup g)
        {
            switch (g)
            {
                case IconGroup.Road: return new Color(1.00f, 0.78f, 0.30f); // gold
                case IconGroup.Stone: return new Color(0.55f, 0.80f, 1.00f); // pale lightning blue
                case IconGroup.Wood: return new Color(1.00f, 0.55f, 0.20f); // amber
                case IconGroup.Earth: return new Color(0.95f, 0.45f, 0.25f); // ember
                case IconGroup.Grass: return new Color(0.45f, 1.00f, 0.40f); // green
                case IconGroup.Frost: return new Color(0.55f, 0.95f, 1.00f); // ice
                default: return Color.white;
            }
        }

        private static ParticleSystem System()
        {
            if (s_ps != null) return s_ps;

            Material mat = RuneMaterial.Get(RuneAtlas.Get());
            if (mat == null)
            {
                s_failed = true;
                Plugin.Log.LogWarning("[runes] no particle shader found: footstep runes are off");
                return null;
            }

            var go = new GameObject("ThunderFeet_Runes");
            Object.DontDestroyOnLoad(go);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.maxParticles = 600;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier = -0.04f; // runes drift up

            var emission = ps.emission;
            emission.enabled = false; // manual Emit only

            var shape = ps.shape;
            shape.enabled = false;

            // Slow down after the kick, then hover.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.6f;
            limit.dampen = 0.25f;

            // Flash in, glow, fade out. Additive blending ignores alpha in some shaders, so the colour fades to black as well.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.45f), new GradientColorKey(Color.black, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.15f, 1.1f), new Keyframe(1f, 0.7f)));

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f); // radians per second: a slow tumble

            // One random rune per particle, held for its whole life (frameOverTime is normalised 0..1 over the 16 cells).
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
            s_ps = ps;
            return ps;
        }
    }

    /// <summary>16 runes in a 512x512 4x4 atlas, white on transparent black, premultiplied (tinted per particle). Glyphs from WoV's ValkyrieRuneTextureGenerator.</summary>
    internal static class RuneAtlas
    {
        private const int Atlas = 512, Cell = 128;
        private static Texture2D s_tex;

        // Per rune: segments as {x0, y0, x1, y1, strokeWidth} on a 128 grid. The glow pass is drawn at width + 3.
        private static readonly int[][] Runes =
        {
            new[] { 20,20,108,108,3, 108,20,20,108,3 },
            new[] { 64,16,64,112,3, 64,64,32,96,2, 64,64,96,96,2 },
            new[] { 40,20,40,108,3, 40,96,96,112,2, 40,72,96,88,2 },
            new[] { 40,20,40,112,3, 40,96,88,112,2, 88,112,88,80,2, 88,80,40,96,2 },
            new[] { 64,16,64,112,4 },
            new[] { 64,16,64,112,3, 64,40,32,72,2, 64,40,96,72,2 },
            new[] { 36,16,36,112,3, 92,16,92,112,3, 36,36,92,92,2 },
            new[] { 80,20,40,20,2, 80,20,40,64,2, 40,64,80,108,2, 80,108,40,108,2 },
            new[] { 64,16,64,112,3, 96,96,32,32,2 },
            new[] { 32,48,32,112,3, 96,48,96,112,3, 32,48,64,16,2, 64,16,96,48,2 },
            new[] { 48,16,48,112,3, 48,80,96,48,2 },
            new[] { 64,112,96,72,2, 96,72,64,32,2, 64,32,32,72,2, 32,72,64,112,2, 32,72,16,16,2, 96,72,112,16,2 },
            new[] { 20,20,64,64,2, 20,108,64,64,2, 108,20,64,64,2, 108,108,64,64,2 },
            new[] { 36,16,36,112,3, 36,96,80,80,2, 80,80,36,64,2, 36,64,80,48,2, 80,48,36,32,2 },
            new[] { 32,16,32,112,3, 96,16,96,112,3, 32,112,96,64,2, 96,112,32,64,2 },
            new[] { 64,16,108,64,3, 108,64,64,112,3, 64,112,20,64,3, 20,64,64,16,3 },
        };

        internal static Texture2D Get()
        {
            if (s_tex != null) return s_tex;

            var px = new Color32[Atlas * Atlas];
            var glow = new Color32(110, 110, 110, 110); // premultiplied: additive blending adds RGB and ignores alpha
            var stroke = new Color32(255, 255, 255, 255);
            for (int i = 0; i < Runes.Length; i++)
            {
                int ox = (i % 4) * Cell, oy = (3 - i / 4) * Cell;
                int[] r = Runes[i];
                for (int pass = 0; pass < 2; pass++)
                    for (int s = 0; s + 4 < r.Length; s += 5)
                        Line(px, ox + r[s], oy + r[s + 1], ox + r[s + 2], oy + r[s + 3], pass == 0 ? r[s + 4] + 3 : r[s + 4], pass == 0 ? glow : stroke);
            }

            s_tex = new Texture2D(Atlas, Atlas, TextureFormat.RGBA32, true) { name = "ThunderFeet_RuneAtlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            s_tex.SetPixels32(px);
            s_tex.Apply(true);
            return s_tex;
        }

        private static void Line(Color32[] px, int x0, int y0, int x1, int y1, int r, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy;
            while (true)
            {
                for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y > r * r) continue;
                    int px_ = x0 + x, py = y0 + y;
                    if (px_ < 0 || py < 0 || px_ >= Atlas || py >= Atlas) continue;
                    int idx = py * Atlas + px_;
                    if (px[idx].a < c.a) px[idx] = c; // the stroke wins over the glow, never the other way round
                }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }
    }

    /// <summary>An additive particle material from a shader the game already has (WoV's ValkyrieRuneVFX_Helper, trimmed).</summary>
    internal static class RuneMaterial
    {
        private static Material s_mat;

        internal static Material Get(Texture2D tex)
        {
            if (s_mat != null) return s_mat;

            Shader best = Shader.Find("Particles/Standard Unlit")
                          ?? Shader.Find("Legacy Shaders/Particles/Additive")
                          ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                          ?? Shader.Find("Particles/Additive")
                          ?? Shader.Find("Particles/Alpha Blended");

            if (best == null)
            {
                float bestScore = float.MinValue;
                foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m == null || m.shader == null) continue;
                    if (!m.HasProperty("_Color") && !m.HasProperty("_TintColor")) continue;
                    string n = m.shader.name.ToLowerInvariant();
                    float score = (n.Contains("particle") ? 3f : 0f) + (n.Contains("add") ? 3f : 0f) + (n.Contains("unlit") ? 2f : 0f) + (n.Contains("transparent") ? 1.5f : 0f);
                    if (score > bestScore) { bestScore = score; best = m.shader; }
                }
            }
            if (best == null) return null;

            var mat = new Material(best) { name = "ThunderFeet_RuneMaterial" };
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            try { mat.mainTexture = tex; } catch { }
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.white * 2f);

            // Additive: glows, and never shows a dark square around a rune.
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            Plugin.Log.LogInfo($"[runes] rune material built from '{best.name}'");
            s_mat = mat;
            return mat;
        }
    }
}
