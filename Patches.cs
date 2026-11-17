using HarmonyLib;

namespace ThunderFeet
{
    [HarmonyPatch(typeof(Player), "Update")]
    internal static class Player_Update_Patch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            ThunderTracker.Tick(__instance);
        }
    }

    /// <summary>
    /// Character.UpdateWalking (1.0.21, Character.cs 1603) picks one base speed:
    ///   jog  = m_speed * GetJogSpeedFactor()     walk = m_walkSpeed
    ///   run  = m_runSpeed * GetRunSpeedFactor()  sneak / encumbered = m_crouchSpeed
    /// then applies liquid drag, attack slowdown, status effects and lava. Scaling the four base fields for the length of the call
    /// changes ONLY on-foot ground speed: turn speed, swimming, flying and every other mod's multipliers stay as they are.
    /// The finalizer puts the fields back even if the call throws.
    /// </summary>
    [HarmonyPatch(typeof(Character), "UpdateWalking")]
    internal static class Character_UpdateWalking_Patch
    {
        internal struct Saved
        {
            public bool Active;
            public float Speed, Run, Walk, Crouch;
        }

        private static void Prefix(Character __instance, out Saved __state)
        {
            __state = default;
            if (!ReferenceEquals(__instance, Player.m_localPlayer)) return;

            float jog = ThunderTracker.JogMul, sprint = ThunderTracker.SprintMul;
            if (jog == 1f && sprint == 1f) return;

            __state = new Saved
            {
                Active = true,
                Speed = __instance.m_speed,
                Run = __instance.m_runSpeed,
                Walk = __instance.m_walkSpeed,
                Crouch = __instance.m_crouchSpeed,
            };
            __instance.m_speed *= jog;
            __instance.m_walkSpeed *= jog;
            __instance.m_crouchSpeed *= jog;
            __instance.m_runSpeed *= sprint;
        }

        private static void Finalizer(Character __instance, Saved __state)
        {
            if (!__state.Active) return;
            __instance.m_speed = __state.Speed;
            __instance.m_runSpeed = __state.Run;
            __instance.m_walkSpeed = __state.Walk;
            __instance.m_crouchSpeed = __state.Crouch;
        }
    }
}
