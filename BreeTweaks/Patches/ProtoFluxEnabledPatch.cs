using BreeTweaks.Attributes;


using FrooxEngine;
using FrooxEngine.ProtoFlux;

using HarmonyLib;

using ResoniteModLoader;


namespace BreeTweaks.Patches;

[TweakCategory("ProtoFluxEnabledPatch", "ProtoFluxEnabledPatch", hidden: true)]
internal static class ProtoFluxEnabledPatch
{
  [HarmonyPrefix]
  [HarmonyPatch(typeof(ProtoFluxController), nameof(ProtoFluxController.RunNodeEvents))]
  public static bool Patch_RunNodeEvents(ProtoFluxController __instance)
  {
    if (ResoniteBreeTweaksMod.IsProtoFluxEnabled || __instance.World.IsUserspace())
    {
      return true;
    }

    return false;
  }

  [HarmonyPrefix]
  [HarmonyPatch(typeof(ProtoFluxController), nameof(ProtoFluxController.RunNodeUpdates))]
  public static bool Patch_RunNodeUpdates(ProtoFluxController __instance)
  {
    if (ResoniteBreeTweaksMod.IsProtoFluxEnabled || __instance.World.IsUserspace())
    {
      return true;
    }

    return false;
  }

  [HarmonyPrefix]
  [HarmonyPatch(typeof(ProtoFluxController), nameof(ProtoFluxController.RunContinuousChanges))]
  public static bool Patch_RunContinuousChanges(ProtoFluxController __instance)
  {
    if (ResoniteBreeTweaksMod.IsProtoFluxEnabled || __instance.World.IsUserspace())
    {
      return true;
    }

    return false;
  }

  [HarmonyPrefix]
  [HarmonyPatch(typeof(ProtoFluxController), nameof(ProtoFluxController.RunDiscreteChanges))]
  public static bool Patch_RunDiscreteChanges(ProtoFluxController __instance)
  {
    if (ResoniteBreeTweaksMod.IsProtoFluxEnabled || __instance.World.IsUserspace())
    {
      return true;
    }

    return false;
  }
}
