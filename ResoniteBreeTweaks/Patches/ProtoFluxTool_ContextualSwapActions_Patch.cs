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
using System.Linq;
using FrooxEngine.Undo;
using ProtoFlux.Runtimes.Execution.Nodes;
using ProtoFlux.Runtimes.Execution.Nodes.Math.Easing;


[HarmonyPatchCategory("ProtoFluxTool Contextual Swap Actions"), TweakCategory("Adds 'Contextual Swapping Actions' to the ProtoFlux Tool. Double pressing secondary pointing at a node with protoflux tool will be open a context menu of actions to swap the node for another node.")]
[HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.OnSecondaryPress))]
internal static class ProtoFluxTool_ContextualSwapActions_Patch
{
  internal enum ConnectionTransferType
  {
    /// <summary>
    /// Transfers the connections by name, connections that are not found and are not of the same type will be lost.
    /// </summary>
    ByNameLossy,
    /// <summary>
    /// Transfers the connections by index, only use this for nodes that are identitcal in inputs and outputs.
    /// There is no sanity checking!!
    /// </summary>
    ByIndex,
    /// <summary>
    /// Transfers the connections by a manually made set of mappings. Unmapped connections will be lost.
    /// </summary>
    ByMappingsLossy
  }

  internal readonly struct MenuItem(Type node, Type? binding = null, string? name = null, ConnectionTransferType? connectionTransferType = ConnectionTransferType.ByNameLossy)
  {
    internal readonly Type node = node;
    internal readonly Type? binding = binding;
    internal readonly string? name = name;

    internal readonly ConnectionTransferType? connectionTransferType = connectionTransferType;

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

  // TODO: find alternative to this, even generating it at runtime will feel bad.
  //       alternatives include manual conversions, 
  internal static readonly Dictionary<(Type, Type), Dictionary<int, int>> CompiledInputMappings = new() {
    {(typeof(For), typeof(RangeLoopInt)), new () {
      {NodeMetadataHelper.GetMetadata(typeof(For)).GetInputByName("Count").Index, NodeMetadataHelper.GetMetadata(typeof(RangeLoopInt)).GetInputByName("End").Index}
    }},
    {(typeof(RangeLoopInt), typeof(For)), new () {
      {NodeMetadataHelper.GetMetadata(typeof(RangeLoopInt)).GetInputByName("End").Index, NodeMetadataHelper.GetMetadata(typeof(For)).GetInputByName("Count").Index}
    }},
  };

  // todo: pages
  private static void CreateMenu(ProtoFluxTool __instance, ProtoFluxNode hitNode)
  {
    __instance.StartTask(async () =>
    {
      var menu = await __instance.LocalUser.OpenContextMenu(__instance, __instance.Slot);
      var items = GetMenuItems(__instance, hitNode).Take(10);
      // TODO: pages / menus

      foreach (var menuItem in items)
      {
        AddMenuItem(__instance, menu, colorX.White, menuItem, () =>
        {
          var runtime = hitNode.NodeInstance.Runtime;
          var oldNode = hitNode.NodeInstance;
          var binding = ProtoFluxHelper.GetBindingForNode(menuItem.node);

          var undoBatch = __instance.World.BeginUndoBatch($"Swap {hitNode.Name} to {menuItem.DisplayName}");

          var newNode = __instance.SpawnNode(binding);
          newNode.Slot.TRS = hitNode.Slot.TRS;

          var query = new NodeQueryAcceleration(runtime.Group);
          var evaluatingNodes = query.GetEvaluatingNodes(oldNode);
          var impulsingNodes = query.GetImpulsingNodes(oldNode);
          var nodeReferences = hitNode.Group.Nodes.ToDictionary(n => n.NodeInstance);
          var remappedNodes = new Dictionary<INode, INode>() {
            {oldNode, newNode.NodeInstance}
          };

          // outputs
          foreach (var evaluatingNode in evaluatingNodes)
          {
            for (int i = 0; i < evaluatingNode.FixedInputCount; i++)
            {
              var source = evaluatingNode.GetInputSource(i);
              if (source?.OwnerNode == oldNode)
              {
                var evaluatingNodeComponent = nodeReferences[evaluatingNode];
                var outputIndex = source.FindLinearOutputIndex();
                newNode.TryConnectInput(evaluatingNodeComponent.GetInput(i), newNode.GetOutput(outputIndex), allowExplicitCast: false, undoable: true);
              }
            }
          }

          // inputs
          switch (menuItem.connectionTransferType)
          {

            case ConnectionTransferType.ByNameLossy:
              {
                var newMeta = NodeMetadataHelper.GetMetadata(menuItem.node);
                var oldMeta = NodeMetadataHelper.GetMetadata(oldNode.GetType());
                var validOutputs = newMeta.FixedOutputs
                  .Zip(oldMeta.FixedOutputs, (a, b) => (a, b))
                  .Where((z) => z.a.OutputType == z.b.OutputType);

                foreach (var (a, b) in validOutputs)
                {
                  if (oldNode.GetInputSource(b.Index) is IOutput sourceNodeOutput)
                  {
                    var output = nodeReferences[sourceNodeOutput.OwnerNode].GetOutput(sourceNodeOutput.FindLinearOutputIndex());
                    newNode.TryConnectInput(newNode.GetInput(a.Index), output, allowExplicitCast: false, undoable: true);
                  }
                }
                break;
              }
            case ConnectionTransferType.ByIndex:
              {
                for (int i = 0; i < oldNode.InputCount; i++)
                {
                  var inputSource = oldNode.GetInputSource(i);
                  if (inputSource != null)
                  {
                    var output = nodeReferences[inputSource.OwnerNode].GetOutput(inputSource.FindLinearOutputIndex());
                    newNode.TryConnectInput(newNode.GetInput(i), output, allowExplicitCast: false, undoable: true);
                  }
                }
                break;
              }
            case ConnectionTransferType.ByMappingsLossy:
              {
                var nodeInputMappings = CompiledInputMappings.GetValueSafe((oldNode.GetType(), menuItem.node));

                for (int i = 0; i < oldNode.InputCount; i++)
                {
                  if (oldNode.GetInputSource(i) is IOutput inputSource)
                  {
                    if (nodeInputMappings?.TryGetValue(i, out var mappedIndex) ?? false)
                    {
                      var newInput = newNode.GetInput(mappedIndex);
                      var output = nodeReferences[inputSource.OwnerNode].GetOutput(inputSource.FindLinearOutputIndex());
                      newNode.TryConnectInput(newInput, output, allowExplicitCast: false, undoable: true);
                    }
                  }
                }
                break;
              }
          }


          // input impulses
          foreach (var impulsingNode in impulsingNodes)
          {
            for (int i = 0; i < impulsingNode.FixedImpulseCount; i++)
            {
              var operation = impulsingNode.GetImpulseTarget(i);
              if (operation?.OwnerNode == oldNode)
              {
                var impulsingNodeComponent = nodeReferences[impulsingNode];
                var operationIndex = operation.FindLinearOperationIndex();
                newNode.TryConnectImpulse(impulsingNodeComponent.GetImpulse(i), newNode.GetOperation(operationIndex), undoable: true);
              }
            }
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
    var label = (LocaleString)item.DisplayName;
    var menuItem = menu.AddItem(in label, (Uri?)null, color);
    menuItem.Button.LocalPressed += (button, data) =>
    {
      setup();
      __instance.LocalUser.CloseContextMenu(__instance);
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

  static readonly HashSet<Type> ForLoopGroup = [
    typeof(For),
    typeof(RangeLoopInt),
  ];

  static readonly HashSet<Type> EasingGroupFloat = [
    typeof(EaseInBounceFloat),
    typeof(EaseInCircularFloat),
    typeof(EaseInCubicFloat),
    typeof(EaseInElasticFloat),
    typeof(EaseInExponentialFloat),
    typeof(EaseInOutBounceFloat),
    typeof(EaseInOutCircularFloat),
    typeof(EaseInOutCubicFloat),
    typeof(EaseInOutElasticFloat),
    typeof(EaseInOutExponentialFloat),
    typeof(EaseInOutQuadraticFloat),
    typeof(EaseInOutQuarticFloat),
    typeof(EaseInOutQuinticFloat),
    typeof(EaseInOutReboundFloat),
    typeof(EaseInOutSineFloat),
    typeof(EaseInQuadraticFloat),
    typeof(EaseInQuarticFloat),
    typeof(EaseInQuinticFloat),
    typeof(EaseInReboundFloat),
    typeof(EaseInSineFloat),
    typeof(EaseOutBounceFloat),
    typeof(EaseOutCircularFloat),
    typeof(EaseOutCubicFloat),
    typeof(EaseOutElasticFloat),
    typeof(EaseOutExponentialFloat),
    typeof(EaseOutQuadraticFloat),
    typeof(EaseOutQuarticFloat),
    typeof(EaseOutQuinticFloat),
    typeof(EaseOutReboundFloat),
    typeof(EaseOutSineFloat),
  ];

  static readonly HashSet<Type> EasingGroupDouble = [
    typeof(EaseInBounceDouble),
    typeof(EaseInCircularDouble),
    typeof(EaseInCubicDouble),
    typeof(EaseInElasticDouble),
    typeof(EaseInExponentialDouble),
    typeof(EaseInOutBounceDouble),
    typeof(EaseInOutCircularDouble),
    typeof(EaseInOutCubicDouble),
    typeof(EaseInOutElasticDouble),
    typeof(EaseInOutExponentialDouble),
    typeof(EaseInOutQuadraticDouble),
    typeof(EaseInOutQuarticDouble),
    typeof(EaseInOutQuinticDouble),
    typeof(EaseInOutReboundDouble),
    typeof(EaseInOutSineDouble),
    typeof(EaseInQuadraticDouble),
    typeof(EaseInQuarticDouble),
    typeof(EaseInQuinticDouble),
    typeof(EaseInReboundDouble),
    typeof(EaseInSineDouble),
    typeof(EaseOutBounceDouble),
    typeof(EaseOutCircularDouble),
    typeof(EaseOutCubicDouble),
    typeof(EaseOutElasticDouble),
    typeof(EaseOutExponentialDouble),
    typeof(EaseOutQuadraticDouble),
    typeof(EaseOutQuarticDouble),
    typeof(EaseOutQuinticDouble),
    typeof(EaseOutReboundDouble),
    typeof(EaseOutSineDouble),
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

    if (ForLoopGroup.Contains(nodeType))
    {
      foreach (var match in ForLoopGroup)
      {
        if (match == nodeType) continue;
        yield return new MenuItem(match, connectionTransferType: ConnectionTransferType.ByMappingsLossy);
      }
    }

    if (EasingGroupFloat.Contains(nodeType))
    {
      foreach (var match in EasingGroupFloat)
      {
        if (match == nodeType) continue;
        yield return new MenuItem(match);
      }
    }

    if (EasingGroupDouble.Contains(nodeType))
    {
      foreach (var match in EasingGroupDouble)
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
