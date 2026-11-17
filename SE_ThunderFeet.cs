using UnityEngine;

namespace ThunderFeet
{
    /// <summary>
    /// The HUD icon for the surface under your feet. Display only: the speed itself is applied in Patches (Character.UpdateWalking),
    /// because StatusEffect.ModifySpeed would also change turn and swim speed.
    /// Added straight to the local player's SEMan (it clones this template), so it needs no ObjectDB registration and is never saved.
    /// </summary>
    public class SE_ThunderFeet : StatusEffect
    {
        internal const string EffectName = "SE_ThunderFeet";
        internal static readonly int Hash = EffectName.GetStableHashCode();

        internal Surface Surface;
        internal float Sprint = 1f;
        internal float Jog = 1f;

        internal static SE_ThunderFeet CreateTemplate()
        {
            var se = ScriptableObject.CreateInstance<SE_ThunderFeet>();
            se.name = EffectName;
            se.m_name = "ThunderFeet";
            se.m_ttl = 0f; // never runs out; ThunderTracker removes it
            return se;
        }

        internal void Show(Surface surface, float sprint, float jog)
        {
            Surface = surface;
            Sprint = sprint;
            Jog = jog;
            SurfaceInfo info = System.Array.Find(SurfaceInfo.All, i => i.Surface == surface);
            m_name = info != null ? info.DisplayName : surface.ToString();
            m_icon = SurfaceIcons.Get(surface);
        }

        public override string GetIconText()
        {
            if (!ThunderConfig.IconShowsPercent.Value) return "";
            string s = Pct(Sprint), j = Pct(Jog);
            return s == j ? s : $"{s} / {j}";
        }

        public override string GetTooltipString()
        {
            return $"Sprint {Pct(Sprint)}\nJog / walk {Pct(Jog)}";
        }

        private static string Pct(float mul)
        {
            int p = Mathf.RoundToInt((mul - 1f) * 100f);
            return p == 0 ? "±0%" : (p > 0 ? "+" : "") + p + "%";
        }
    }
}
