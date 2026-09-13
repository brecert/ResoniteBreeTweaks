using System.Runtime.CompilerServices;

using BreeTweaks.Attributes;

using FrooxEngine;

using HarmonyLib;

namespace BreeTweaks.Patches;

[TweakCategory("CamerasEnabledPatch", "CamerasEnabledPatch", hidden: true)]
internal static class CamerasEnabledPatch
{
  [HarmonyPrefix]
  [HarmonyPatch(typeof(RenderableComponent), nameof(RenderableComponent.IsRenderable), MethodType.Getter)]
  public static bool Patch_RenderTextureProvider_Enabled(RenderableComponent __instance, ref bool __result)
  {
    if (__instance.World.IsUserspace() || __instance is not Camera)
    {
      return true;
    }

    __result = ResoniteBreeTweaksMod.AreCamerasEnabled;
    return ResoniteBreeTweaksMod.AreCamerasEnabled;
  }

  internal static void UpdateCameras()
  {
    var cameras = Engine.Current.WorldManager.FocusedWorld.AllSlots.AsParallel().SelectMany(s => s.Components).OfType<Camera>();
    foreach (var camera in cameras)
    {
      OnChanges(camera);
    }
  }

  [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OnChanges")]
  private static extern void OnChanges(Camera camera);
}
