using BreeTweaks.Attributes;

using Elements.Core;

using FrooxEngine.ProtoFlux;

using HarmonyLib;

namespace BreeTweaks.Patches;

[HarmonyPatch(typeof(DatatypeColorHelper), "GetTypeColor")]
[TweakCategory("Dummy Type Color", "Modifies the Type color of dummy to give it a custom one that's more visually fitting.")]
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
