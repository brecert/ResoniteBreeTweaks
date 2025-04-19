using System;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;

using BreeTweaks.Attributes;
using HarmonyLib;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Transform;
using ProtoFlux.Core;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.Strings;
using System.Linq;
using FrooxEngine.Undo;


[HarmonyPatchCategory("ProtoFluxTool Contextual Swap Actions"), TweakCategory("Adds 'Contextual Swapping Actions' to the ProtoFlux Tool. Double pressing secondary pointing at a node with protoflux tool will be open a context menu of actions to swap the node for another node.")]
[HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.OnSecondaryPress))]
internal static class ProtoFluxTool_ContextualSwapActions_Patch
{
  internal readonly struct MenuItem(Type node, Type? binding = null, string? name = null, bool overload = false)
  {
    internal readonly Type node = node;
    internal readonly Type? binding = binding;
    internal readonly string? name = name;

    internal readonly bool overload = overload;

    internal readonly string DisplayName => name ?? NodeMetadataHelper.GetMetadata(node).Name ?? node.GetNiceTypeName();
  }

  internal class ProtoFluxNodeData
  {
    internal DateTime? lastSecondaryPress;
    internal ProtoFluxNode? lastSecondaryPressNode;

    internal double SecondsSinceLastSecondaryPress() => (DateTime.Now - lastSecondaryPress.GetValueOrDefault()).TotalSeconds;
  }

  const double DoublePressTime = 1;

  private static readonly ConditionalWeakTable<ProtoFluxTool, ProtoFluxNodeData> additionalData = new();

  internal static bool Prefix(ProtoFluxTool __instance, SyncRef<ProtoFluxElementProxy> ____currentProxy)
  {
    var data = additionalData.GetOrCreateValue(__instance);
    var elementProxy = ____currentProxy.Target;

    if (elementProxy is null)
    {
      var hit = GetHit(__instance);
      if (hit is { Collider.Slot: var hitSlot })
      {
        var hitNode = hitSlot.GetComponentInParents<ProtoFluxNode>();
        if (hitNode != null)
        {
          if (data.SecondsSinceLastSecondaryPress() < DoublePressTime && data.lastSecondaryPressNode != null && !data.lastSecondaryPressNode.IsRemoved && data.lastSecondaryPressNode == hitNode)
          {
            CreateMenu(__instance, hitNode);
            data.lastSecondaryPressNode = null;
            data.lastSecondaryPressNode = null;
            // skip rest
            return false;
          }
          else
          {
            data.lastSecondaryPressNode = hitNode;
            data.lastSecondaryPress = DateTime.Now;
            // skip null
            return true;
          }
        }
      }

      data.lastSecondaryPressNode = null;
      data.lastSecondaryPress = null;
    }

    return true;
  }

  // internal static readonly HashSet<Type>[] SelectionGroups = [
  //   [typeof(GetForward), typeof(GetBackward), typeof(GetUp), typeof(GetDown), typeof(GetLeft), typeof(GetRight)],
  //   [typeof(ValueAdd<>), typeof(ValueSub<>), typeof(ValueMul<>), typeof(ValueDiv<>)],
  //   [typeof(For), typeof(AsyncFor)],
  //   [typeof(ValueInc<>), typeof(ValueDec<>)],
  // ];

  private static void CreateMenu(ProtoFluxTool __instance, ProtoFluxNode hitNode)
  {
    __instance.StartTask(async () =>
    {
      var menu = await __instance.LocalUser.OpenContextMenu(__instance, __instance.Slot);
      Traverse.Create(menu).Field<float?>("_speedOverride").Value = 10;

      foreach (var menuItem in GetMenuItems(__instance, hitNode))
      {
        AddMenuItem(__instance, menu, colorX.White, menuItem, () =>
        {
          var runtime = hitNode.NodeInstance.Runtime;
          var fromNode = hitNode.NodeInstance;
          // var intoNode = runtime.AddNode(menuItem.node);
          var binding = ProtoFluxHelper.GetBindingForNode(menuItem.node);

          var undoBatch = __instance.World.BeginUndoBatch($"Swap {hitNode.Name} to {menuItem.DisplayName}");

          var newNode = __instance.SpawnNode(binding);
          newNode.Slot.TRS = hitNode.Slot.TRS;

          var query = new NodeQueryAcceleration(runtime.Group);
          var evaluatingNodes = query.GetEvaluatingNodes(fromNode);
          var nodeReferences = hitNode.Group.Nodes.ToDictionary(n => n.NodeInstance);

          foreach (var evaluatingNode in evaluatingNodes)
          {
            for (int i = 0; i < evaluatingNode.FixedInputCount; i++)
            {
              var source = evaluatingNode.GetInputSource(i);
              if (source?.OwnerNode == fromNode)
              {
                var evaluatingNodeComponent = nodeReferences[evaluatingNode];
                newNode.TryConnectInput(evaluatingNodeComponent.GetInput(i), newNode.GetOutput(0), allowExplicitCast: false, undoable: true);
              }
            }
          }

          for (int i = 0; i < fromNode.InputCount; i++)
          {
            var inputSource = fromNode.GetInputSource(i);
            if (inputSource == null) continue;
            var output = nodeReferences[inputSource.OwnerNode].GetOutput(i);
            newNode.TryConnectInput(newNode.GetInput(i), output, allowExplicitCast: false, undoable: true);
          }

          // delay for "seamless" transition
          // without this there will be an update where there are no nodes connectedm
          // that is unacceptable as it would cause unintented behavior with protoflux that can lead to destructive actions
          __instance.StartTask(async () =>
          {
            await new Updates(1);
            hitNode.Slot.Destroy();
          });

          __instance.World.EndUndoBatch();
        });
      }
    });
  }

  private static void AddMenuItem(ProtoFluxTool __instance, ContextMenu menu, colorX color, MenuItem item, Action setup)
  {
    var nodeMetadata = NodeMetadataHelper.GetMetadata(item.node);
    var label = (LocaleString)(item.name ?? nodeMetadata.Name ?? item.node.GetNiceTypeName());
    var menuItem = menu.AddItem(in label, (Uri?)null, color);
    menuItem.Button.LocalPressed += (button, data) =>
    {
      setup();
      __instance.LocalUser.CloseContextMenu(__instance);

      // var nodeBinding = item.binding ?? ProtoFluxHelper.GetBindingForNode(item.node);
      // CleanupDraggedWire(__instance);
      // __instance.LocalUser.CloseContextMenu(__instance);
      // __instance.SpawnNode(nodeBinding, n =>
      //     {
      //       __instance.StartTask(async () =>
      //       {
      //         // await new Updates(5); // this is dumb but needed for the spawned node to have information :)
      //         // setup(n);
      //         // __instance.LocalUser.CloseContextMenu(__instance);
      //       });
      //     });
    };
  }

  static readonly HashSet<Type> GetDirectionGroup = [
    typeof(GetForward),
    typeof(GetBackward),
    typeof(GetUp),
    typeof(GetDown),
    typeof(GetLeft),
    typeof(GetRight)
  ];

  internal static IEnumerable<MenuItem> GetMenuItems(ProtoFluxTool __instance, ProtoFluxNode nodeComponent)
  {
    var node = nodeComponent.NodeInstance;
    var nodeType = node.GetType();

    if (GetDirectionGroup.Contains(nodeType))
    {
      foreach (var match in GetDirectionGroup)
      {
        if (match == nodeType) continue;
        yield return new MenuItem(match);
      }
    }
  }



  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxTool), "CleanupDraggedWire")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static void CleanupDraggedWire(ProtoFluxTool instance) => throw new NotImplementedException();

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxTool), "OnSecondaryPress")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static void OnSecondaryPress(ProtoFluxTool instance) => throw new NotImplementedException();


  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxHelper), "GetNodeForType")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static Type GetNodeForType(Type type, List<NodeTypeRecord> list) => throw new NotImplementedException();

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(Tool), "GetHit")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static RaycastHit? GetHit(Tool instance) => throw new NotImplementedException();

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxNodeGroup), "MapCastsAndOverloads")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static void MapCastsAndOverloads(ProtoFluxNodeGroup instance, ProtoFluxNode sourceNode, ProtoFluxNode targetNode, ConnectionResult result, bool undoable) => throw new NotImplementedException();

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxNode), "AssociateInstance")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static void AssociateInstance(ProtoFluxNode instance, ProtoFluxNodeGroup group, INode node) => throw new NotImplementedException();

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxNode), "ReverseMapElements")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static void ReverseMapElements(ProtoFluxNode instance, Dictionary<INode, ProtoFluxNode> nodeMapping, bool undoable) => throw new NotImplementedException();
}
