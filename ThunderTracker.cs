using UnityEngine;

namespace ThunderFeet
{
    /// <summary>
    /// Keeps the local player's current surface, its two multipliers and the HUD icon up to date (10 times a second).
    /// </summary>
    internal static class ThunderTracker
    {
        private const float Interval = 0.1f;

        internal static Surface Current { get; private set; } = Surface.None;
        internal static float SprintMul { get; private set; } = 1f;
        internal static float JogMul { get; private set; } = 1f;

        private static float s_nextCheck;
        private static SE_ThunderFeet s_template;

        internal static void Tick(Player player)
        {
            if (Time.time < s_nextCheck) return;
            s_nextCheck = Time.time + Interval;

            Surface detected = Current;
            if (player.IsDead() || player.IsSwimming() || player.InDebugFlyMode() || (player.IsAttached() && !player.IsAttachedToShip()))
                detected = Surface.None;
            else if (player.IsOnGround() || player.InLiquid())
                detected = SurfaceDetector.Detect(player);
            // airborne (jumping, falling): keep the last ground, so a jump along a road does not lose the road's speed mid-air

            Surface surface = Settle(detected);

            if (surface != Current && ThunderConfig.LogSurfaceChanges.Value)
                Plugin.Log.LogInfo($"[surface] {Current} -> {surface} at {player.transform.position}");

            Current = surface;
            SprintMul = ThunderConfig.SprintMultiplier(surface);
            JogMul = ThunderConfig.JogMultiplier(surface);
            UpdateIcon(player);
        }

        private static Surface s_candidate = Surface.None;
        private static float s_candidateSince;

        /// <summary>
        /// The buffer: a new ground must be read steadily for <c>Surface Change Delay</c> seconds before the icon and the speed follow
        /// it, so a one-frame misread never shows. The first reading (from None) and leaving the ground (to None: swimming, riding,
        /// dying) apply at once.
        /// </summary>
        private static Surface Settle(Surface detected)
        {
            if (detected == Current)
            {
                s_candidate = detected;
                return Current;
            }
            if (Current == Surface.None || detected == Surface.None)
            {
                s_candidate = detected;
                return detected;
            }
            if (detected != s_candidate)
            {
                s_candidate = detected;
                s_candidateSince = Time.time;
            }
            return Time.time - s_candidateSince >= ThunderConfig.SurfaceChangeDelay.Value ? detected : Current;
        }

        private static void UpdateIcon(Player player)
        {
            SEMan seman = player.GetSEMan();
            if (seman == null) return;

            bool want = Current != Surface.None && ThunderConfig.ShowIcon.Value != IconMode.Never &&
                        (ThunderConfig.ShowIcon.Value == IconMode.Always || !Mathf.Approximately(SprintMul, 1f) || !Mathf.Approximately(JogMul, 1f));

            var se = seman.GetStatusEffect(SE_ThunderFeet.Hash) as SE_ThunderFeet;
            if (!want)
            {
                if (se != null) seman.RemoveStatusEffect(SE_ThunderFeet.Hash, quiet: true);
                return;
            }

            if (se == null)
            {
                if (s_template == null) s_template = SE_ThunderFeet.CreateTemplate();
                se = seman.AddStatusEffect(s_template) as SE_ThunderFeet;
                if (se == null) return;
            }

            if (se.Surface != Current || se.m_icon == null || !Mathf.Approximately(se.Sprint, SprintMul) || !Mathf.Approximately(se.Jog, JogMul))
                se.Show(Current, SprintMul, JogMul);
        }
    }
}
