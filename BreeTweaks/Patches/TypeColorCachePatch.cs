using BreeTweaks.Attributes;

using Elements.Core;

using FrooxEngine.ProtoFlux;

using HarmonyLib;

[HarmonyPatch(typeof(DatatypeColorHelper), nameof(DatatypeColorHelper.GetTypeColor))]
[TweakCategory("Type Color Cache", "Adds caching for `GetTypeColor`. This has no measured performance impact.", defaultValue: false)]
internal static class TypeColorCachePatch
{
  internal static readonly Dictionary<Type, colorX> TypeColorMap = [];

  internal static bool Prefix(Type type, ref colorX __result, out bool __state)
  {
    if (__state = TypeColorMap.TryGetValue(type, out __result)) return false;
    return true;
  }

  internal static void Postfix(Type type, ref colorX __result, bool __state)
  {
    if (__state) return;
    TypeColorMap.TryAdd(type, __result);
  }
}
