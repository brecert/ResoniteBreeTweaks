using System.Runtime.CompilerServices;

using BreeTweaks.Attributes;

using Elements.Core;

using FrooxEngine;

using HarmonyLib;

using static FrooxEngine.World;

namespace BreeTweaks.Patches;

[TweakCategory("CullDistantSlotsPatch", "Culls inspector contents that are far away.")]
internal static class CullDistantSlotsPatch
{
  static readonly ConditionalWeakTable<SyncRef<Slot>, Slot> InspectorContentRoots = [];

  [HarmonyPostfix]
  [HarmonyPatch(typeof(World), "RefreshStep")]
  public static void CheckDistance(World __instance)
  {
    if (__instance.Stage != RefreshStage.Updates && !__instance.RunFullUpdateCycle) return;

    foreach (var (_, contentRoot) in InspectorContentRoots.Where(r => r.Key.World == __instance))
    {
      if (contentRoot is not null)
      {
        var before = contentRoot.Parent.GetActiveInHierarchy();
        var after = MathX.Distance(contentRoot.GlobalPosition, __instance.LocalUserViewPosition) <= ResoniteBreeTweaksMod.InspectorCullingDistance;
        if (before != after)
        {
          // this is very hacky, but it's cheap
          contentRoot.Parent.GetActiveInHierarchy() = after;
          contentRoot.UpdateActiveHierarchy();
          contentRoot.MarkChangeDirty();
        }
      }
    }
  }

  [HarmonyPostfix]
  [HarmonyPatch(typeof(SceneInspector), "OnStart")]
  public static void AddRoot(SceneInspector __instance)
  {
    __instance.StartTask(async () =>
    {
      await new Updates(30);
      TryAddSlot(__instance.GetHierarchyContentRoot());
      TryAddSlot(__instance.GetComponentsContentRoot());
    });
  }

  private static void TryAddSlot(SyncRef<Slot> syncRef)
  {
    // todo: cleanup on destroy?
    if (syncRef != null)
    {
      syncRef.OnTargetChange += (value) => TryAddSlot(syncRef, value);
      TryAddSlot(syncRef, syncRef);
    }
  }

  private static void TryAddSlot(SyncRef<Slot> syncRef, SyncRef<Slot> value)
  {
    if (value != null && syncRef.Target != null && syncRef.IsTargetRemoved == false)
    {
      InspectorContentRoots.Add(syncRef, value);
    }
  }

  [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hierarchyContentRoot")]
  private static extern ref SyncRef<Slot> GetHierarchyContentRoot(this SceneInspector slot);

  [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_componentsContentRoot")]
  private static extern ref SyncRef<Slot> GetComponentsContentRoot(this SceneInspector slot);

  [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_activeInHierarchy")]
  private static extern ref bool GetActiveInHierarchy(this Slot slot);

  [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "UpdateActiveHierarchy")]
  private static extern void UpdateActiveHierarchy(this Slot slot);

  [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SendActivatedEvents")]
  private static extern void SendActivatedEvents(this Slot slot);

}
