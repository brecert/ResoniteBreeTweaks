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
    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(dummy<>))
    {
      __result = colorX.AdditiveBlend(colorX.White, type.GenericTypeArguments[0].GetTypeColor().MulSaturation(0.675f)).NormalizeHDR(out _);
      return false;
    }

    if (type == typeof(dummy))
    {
      __result = colorX.White;
      return false;
    }

    return true;
  }
}