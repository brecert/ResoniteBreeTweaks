using System;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;

using BreeTweaks.Attributes;
using HarmonyLib;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution.Nodes;
using ProtoFlux.Runtimes.Execution.Nodes.Operators;
using ProtoFlux.Runtimes.Execution.Nodes.Math.Quaternions;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Transform;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Slots;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Async;

namespace BreeTweaks.Patches;

[HarmonyPatchCategory("ProtoFluxTool Contextual Actions"), TweakCategory("Adds 'Contextual Actions' to the ProtoFluxTool. The secondary press while holding a protoflux tool will be open a context menu of quick actions based on what wire you're dragging instead of always spawning an input node. Pressing secondary again will spawn out an input node like normal.")]
[HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.OnSecondaryPress))]
internal static class ProtoFluxTool_ContextualActions_Patch
{

    internal readonly struct MenuItem(Type node, Type? binding = null)
    {
        internal readonly Type node = node;
        internal readonly Type? binding = binding;
    }

    internal static bool Prefix(ProtoFluxTool __instance, SyncRef<ProtoFluxElementProxy> ____currentProxy)
    {
        var elementProxy = ____currentProxy.Target;
        var items = MenuItems(__instance).Take(10).ToArray();

        if (items.Length != 0)
        {
            if (__instance.LocalUser.IsContextMenuOpen())
            {
                __instance.LocalUser.CloseContextMenu(__instance);
                return true;
            }

            __instance.StartTask(async () =>
            {
                var menu = await __instance.LocalUser.OpenContextMenu(__instance, __instance.Slot);
                Traverse.Create(menu).Field<float?>("_speedOverride").Value = 10;

                switch (elementProxy)
                {
                    case ProtoFluxInputProxy inputProxy:
                        {
                            foreach (var item in items)
                            {
                                AddMenuItem(__instance, menu, inputProxy.InputType.Value.GetTypeColor(), item, n =>
                                {
                                    var output = n.NodeOutputs.First(o => typeof(INodeOutput<>).MakeGenericType(inputProxy.InputType).IsAssignableFrom(o.GetType()));
                                    elementProxy.Node.Target.TryConnectInput(inputProxy.NodeInput.Target, output, allowExplicitCast: false, undoable: true);
                                });
                            }
                            break;
                        }
                    case ProtoFluxOutputProxy outputProxy:
                        {
                            foreach (var item in items)
                            {
                                AddMenuItem(__instance, menu, outputProxy.OutputType.Value.GetTypeColor(), item, n =>
                                {
                                    var input = n.NodeInputs.First(i => typeof(INodeOutput<>).MakeGenericType(outputProxy.OutputType).IsAssignableFrom(i.TargetType));
                                    n.TryConnectInput(input, outputProxy.NodeOutput.Target, allowExplicitCast: false, undoable: true);
                                });
                            }
                            break;
                        }
                    case ProtoFluxImpulseProxy impulseProxy:
                        {
                            foreach (var item in items)
                            {
                                // the colors should almost always be the same so unique colors are more important maybe?
                                AddMenuItem(__instance, menu, item.node.GetTypeColor(), item, n =>
                                {
                                    n.TryConnectImpulse(impulseProxy.NodeImpulse.Target, n.GetOperation(0), undoable: true);
                                });
                            }
                            break;
                        }
                    case ProtoFluxOperationProxy operationProxy:
                        {
                            foreach (var item in items)
                            {
                                AddMenuItem(__instance, menu, item.node.GetTypeColor(), item, n =>
                                {
                                    n.TryConnectImpulse(n.GetImpulse(0), operationProxy.NodeOperation.Target, undoable: true);
                                });
                            }
                            break;
                        }
                    default:
                        throw new Exception("found items for unsupported protoflux contextual action type");
                }
            });

            return false;
        }

        return true;
    }

    private static void AddMenuItem(ProtoFluxTool __instance, ContextMenu menu, colorX color, MenuItem item, Action<ProtoFluxNode> setup)
    {
        var nodeMetadata = NodeMetadataHelper.GetMetadata(item.node);
        var label = (LocaleString)(nodeMetadata.Name ?? item.node.GetNiceTypeName());
        var menuItem = menu.AddItem(in label, (Uri?)null, color);
        menuItem.Button.LocalPressed += (button, data) =>
        {
            var nodeBinding = item.binding ?? ProtoFluxHelper.GetBindingForNode(item.node);
            __instance.SpawnNode(nodeBinding, n =>
            {
                setup(n);
                __instance.LocalUser.CloseContextMenu(__instance);
                CleanupDraggedWire(__instance);
            });
        };
    }

    // note: if we can build up a graph then we can egraph reduce to make matches like this easier to spot automatically rather than needing to check each one manually
    // todo: detect add + 1 and offer to convert to inc?
    // todo: detect add + 1 or inc and write and offer to convert to increment?

    internal static IEnumerable<MenuItem> MenuItems(ProtoFluxTool __instance)
    {
        var _currentProxy = Traverse.Create(__instance).Field("_currentProxy").GetValue<SyncRef<ProtoFluxElementProxy>>();
        var target = _currentProxy?.Target;

        switch (target)
        {
            case ProtoFluxInputProxy { InputType.Value: var inputType }
                when inputType == typeof(float)
                  || inputType == typeof(double)
                  || typeof(IVector<float>).IsAssignableFrom(inputType)
                  || typeof(IVector<double>).IsAssignableFrom(inputType):
                {
                    yield return new MenuItem(typeof(ValueAdd<>).MakeGenericType(inputType));
                    yield return new MenuItem(typeof(ValueSub<>).MakeGenericType(inputType));
                    yield return new MenuItem(typeof(ValueMul<>).MakeGenericType(inputType));
                    yield return new MenuItem(typeof(ValueDiv<>).MakeGenericType(inputType));
                    break;
                }
            case ProtoFluxOutputProxy { OutputType.Value: var outputType }
                // todo: more robust through coder / generic checking
                when outputType == typeof(float)
                  || outputType == typeof(double)
                  || outputType == typeof(int)
                  || outputType == typeof(long)
                  || typeof(IVector<float>).IsAssignableFrom(outputType)
                  || typeof(IVector<double>).IsAssignableFrom(outputType)
                  || typeof(IVector<int>).IsAssignableFrom(outputType)
                  || typeof(IVector<long>).IsAssignableFrom(outputType):
                {
                    // core
                    yield return new MenuItem(typeof(ValueAdd<>).MakeGenericType(outputType));
                    yield return new MenuItem(typeof(ValueSub<>).MakeGenericType(outputType));
                    yield return new MenuItem(typeof(ValueMul<>).MakeGenericType(outputType));
                    yield return new MenuItem(typeof(ValueDiv<>).MakeGenericType(outputType));
                    // this may be a bit much?
                    // todo: investigate if contextual hovered actions works
                    yield return new MenuItem(typeof(ValueLessThan<>).MakeGenericType(outputType));
                    yield return new MenuItem(typeof(ValueLessOrEqual<>).MakeGenericType(outputType));
                    yield return new MenuItem(typeof(ValueGreaterThan<>).MakeGenericType(outputType));
                    yield return new MenuItem(typeof(ValueGreaterOrEqual<>).MakeGenericType(outputType));
                    yield return new MenuItem(typeof(ValueEquals<>).MakeGenericType(outputType));
                    break;
                }
            case ProtoFluxOutputProxy { OutputType.Value: var valueType } when typeof(IQuaternion).IsAssignableFrom(valueType):
                {
                    if (valueType == typeof(floatQ)) yield return new MenuItem(typeof(InverseRotation_floatQ));
                    if (valueType == typeof(doubleQ)) yield return new MenuItem(typeof(InverseRotation_doubleQ));
                    yield return new MenuItem(typeof(ValueAdd<>).MakeGenericType(valueType));
                    yield return new MenuItem(typeof(ValueSub<>).MakeGenericType(valueType));
                    yield return new MenuItem(typeof(ValueMul<>).MakeGenericType(valueType));
                    yield return new MenuItem(typeof(ValueDiv<>).MakeGenericType(valueType));
                    break;
                }

        }
        // if (target is ProtoFluxInputProxy { InputType.Value: var type } when type == typeof(float) or ProtoFluxOutputProxy { OutputType.Value: var type } ) {

        // }

        if (target is ProtoFluxInputProxy inputProxy)
        {
            if (TryGetPackNode(inputProxy.InputType, out var packNodeType))
            {
                yield return new MenuItem(packNodeType);
            }

            if (inputProxy.InputType.Value == typeof(User))
            {
                yield return new MenuItem(typeof(LocalUser));
                yield return new MenuItem(typeof(HostUser));
                yield return new MenuItem(typeof(UserFromUsername));
            }

            else if (inputProxy.InputType.Value == typeof(UserRoot))
            {
                yield return new MenuItem(typeof(GetActiveUserRoot));
                yield return new MenuItem(typeof(LocalUserRoot));
                yield return new MenuItem(typeof(UserUserRoot));
            }

            else if (inputProxy.InputType.Value == typeof(bool))
            {
                yield return new MenuItem(typeof(ValueLessThan<int>));
                yield return new MenuItem(typeof(ValueLessOrEqual<int>));
                yield return new MenuItem(typeof(ValueGreaterThan<int>));
                yield return new MenuItem(typeof(ValueGreaterOrEqual<int>));
                yield return new MenuItem(typeof(ValueEquals<int>));
            }
        }

        else if (target is ProtoFluxOutputProxy outputProxy)
        {
            if (TryGetUnpackNode(outputProxy.OutputType, out var unpackNodeType))
            {
                yield return new MenuItem(unpackNodeType);
            }

            if (outputProxy.OutputType.Value == typeof(Slot))
            {
                yield return new MenuItem(typeof(GlobalTransform));
                yield return new MenuItem(typeof(GetForward));
                yield return new MenuItem(typeof(GetChild));
            }

            else if (outputProxy.OutputType.Value == typeof(bool))
            {
                yield return new MenuItem(typeof(If));
                yield return new MenuItem(typeof(ValueConditional<int>)); // dummy type when
                // todo: convert to multi?
                yield return new MenuItem(typeof(AND_Bool));
                yield return new MenuItem(typeof(OR_Bool));
                yield return new MenuItem(typeof(NOT_Bool));
            }
        }

        else if (target is ProtoFluxImpulseProxy impulseProxy)
        {
            // TODO: convert to while?
            yield return new MenuItem(typeof(For));
            yield return new MenuItem(typeof(If));
            yield return new MenuItem(typeof(ValueWrite<dummy>));
            yield return new MenuItem(typeof(Sequence));

            switch (impulseProxy.ImpulseType.Value)
            {
                case ImpulseType.AsyncCall:
                case ImpulseType.AsyncResumption:
                    yield return new MenuItem(typeof(AsyncFor));
                    yield return new MenuItem(typeof(AsyncSequence));
                    break;
            }
        }

        else if (target is ProtoFluxOperationProxy operationProxy)
        {
            if (operationProxy.IsAsync)
            {
                yield return new MenuItem(typeof(StartAsyncTask));
            }
        }
    }

    internal static readonly Dictionary<Type, Type> UnpackNodeMapping = new() {
        {typeof(bool2), typeof(Unpack_Bool2)},
        {typeof(bool3), typeof(Unpack_Bool3)},
        {typeof(bool4), typeof(Unpack_Bool4)},

        {typeof(int2), typeof(Unpack_Int2)},
        {typeof(int3), typeof(Unpack_Int3)},
        {typeof(int4), typeof(Unpack_Int4)},

        {typeof(long2), typeof(Unpack_Long2)},
        {typeof(long3), typeof(Unpack_Long3)},
        {typeof(long4), typeof(Unpack_Long4)},

        {typeof(uint2), typeof(Unpack_Uint2)},
        {typeof(uint3), typeof(Unpack_Uint3)},
        {typeof(uint4), typeof(Unpack_Uint4)},

        {typeof(ulong2), typeof(Unpack_Ulong2)},
        {typeof(ulong3), typeof(Unpack_Ulong3)},
        {typeof(ulong4), typeof(Unpack_Ulong4)},

        {typeof(float2), typeof(Unpack_Float2)},
        {typeof(float3), typeof(Unpack_Float3)},
        {typeof(float4), typeof(Unpack_Float4)},

        {typeof(double2), typeof(Unpack_Double2)},
        {typeof(double3), typeof(Unpack_Double3)},
        {typeof(double4), typeof(Unpack_Double4)},
    };

    internal static bool TryGetUnpackNode(Type nodeType, out Type value) => UnpackNodeMapping.TryGetValue(nodeType, out value);

    internal static readonly Dictionary<Type, Type> PackNodeMapping = new() {
        {typeof(bool2), typeof(Pack_Bool2)},
        {typeof(bool3), typeof(Pack_Bool3)},
        {typeof(bool4), typeof(Pack_Bool4)},

        {typeof(int2), typeof(Pack_Int2)},
        {typeof(int3), typeof(Pack_Int3)},
        {typeof(int4), typeof(Pack_Int4)},

        {typeof(long2), typeof(Pack_Long2)},
        {typeof(long3), typeof(Pack_Long3)},
        {typeof(long4), typeof(Pack_Long4)},

        {typeof(uint2), typeof(Pack_Uint2)},
        {typeof(uint3), typeof(Pack_Uint3)},
        {typeof(uint4), typeof(Pack_Uint4)},

        {typeof(ulong2), typeof(Pack_Ulong2)},
        {typeof(ulong3), typeof(Pack_Ulong3)},
        {typeof(ulong4), typeof(Pack_Ulong4)},

        {typeof(float2), typeof(Pack_Float2)},
        {typeof(float3), typeof(Pack_Float3)},
        {typeof(float4), typeof(Pack_Float4)},

        {typeof(double2), typeof(Pack_Double2)},
        {typeof(double3), typeof(Pack_Double3)},
        {typeof(double4), typeof(Pack_Double4)},
    };

    internal static bool TryGetPackNode(Type nodeType, out Type value) => PackNodeMapping.TryGetValue(nodeType, out value);

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
}