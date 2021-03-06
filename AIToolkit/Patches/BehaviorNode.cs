﻿using AIToolkit.Features;
using AIToolkit.Util;
using Harmony;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace AIToolkit.Patches
{
    /// <summary>
    /// Append leaf behavior nodes that return an order info to the pause popup
    /// </summary>
    [HarmonyPatch(typeof(BehaviorNode), "Update")]
    public static class BehaviorNode_Update_Patch
    {
        public static void Postfix(BehaviorNode __instance, ref BehaviorTreeResults __result)
        {
            if (__instance is LeafBehaviorNode && __result.orderInfo != null)
                AIPause.PausePopup.AppendText(__instance.GetName());
        }
    }
}
