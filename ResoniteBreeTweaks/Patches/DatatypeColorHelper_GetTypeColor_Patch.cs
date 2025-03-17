using FrooxEngine;
using BreeTweaks.Attributes;
using HarmonyLib;
using FrooxEngine.CommonAvatar;
using System.Reflection.Emit;
using System.Collections.Generic;
using ResoniteModLoader;
using FrooxEngine.ProtoFlux;
using System;
using Elements.Core;

namespace BreeTweaks.Patches;

// Use Global Distance for Earmuff Mode
[HarmonyPatchCategory("Dummy Type Color"), TweakCategory("Modifies the Type color of dummy to give it a custom one that's more visually fitting.")]
[HarmonyPatch(typeof(DatatypeColorHelper), "GetTypeColor")]
internal static class DatatypeColorHelper_GetTypeColor_Patch
{
  internal static bool Prefix(Type type, ref colorX __result)
  {
    if (type == typeof(dummy))
    {
      __result = RadiantUI_Constants.Neutrals.LIGHT.ConstructHDR(0.5f);
      return false;
    }
    return true;
  }
}