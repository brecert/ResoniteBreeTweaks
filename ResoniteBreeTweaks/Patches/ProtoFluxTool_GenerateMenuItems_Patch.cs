using System;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;

using BreeTweaks.Attributes;
using HarmonyLib;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Transform;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes;

namespace BreeTweaks.Patches;

[HarmonyPatchCategory("ProtoFluxTool Contextual Actions"), TweakCategory("Adds 'Contextual Actions' to the ProtoFluxTool. The secondary press while holding a protoflux tool will be open a context menu of quick actions based on what wire you're dragging instead of always spawning an input node. Pressing secondary again will spawn out an input node like normal.")]
[HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.OnSecondaryPress))]
internal static class ProtoFluxTool_GenerateMenuItems_Patch
{

    internal readonly struct MenuItem(Type node)
    {
        internal readonly Type node = node;
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

                switch (elementProxy)
                {
                    case ProtoFluxInputProxy inputProxy:
                        {
                            foreach (var item in items)
                            {
                                var label = item.node.Name.AsLocaleKey();
                                var menuItem = menu.AddItem(in label, (Uri?)null, inputProxy.InputType.Value.GetTypeColor());
                                menuItem.Button.LocalPressed += (button, data) =>
                                {
                                    __instance.SpawnNode(item.node, n =>
                                    {
                                        inputProxy.Node.Target.TryConnectInput(inputProxy.NodeInput.Target, n.GetOutput(0), allowExplicitCast: false, undoable: true);
                                        __instance.LocalUser.CloseContextMenu(__instance);
                                        CleanupDraggedWire(__instance);
                                    });
                                };
                            }
                            break;
                        }
                    case ProtoFluxOutputProxy outputProxy:
                        {
                            foreach (var item in items)
                            {
                                var label = item.node.Name.AsLocaleKey();
                                var menuItem = menu.AddItem(in label, (Uri?)null, outputProxy.OutputType.Value.GetTypeColor());
                                menuItem.Button.LocalPressed += (button, data) =>
                                {
                                    __instance.SpawnNode(item.node, n =>
                                    {
                                        n.TryConnectInput(n.GetInput(0), outputProxy.NodeOutput.Target, allowExplicitCast: false, undoable: true);
                                        __instance.LocalUser.CloseContextMenu(__instance);
                                        CleanupDraggedWire(__instance);
                                    });
                                };
                            }
                            break;
                        }
                    case ProtoFluxImpulseProxy impulseProxy:
                        {
                            foreach (var item in items)
                            {
                                var label = item.node.Name.AsLocaleKey();
                                var menuItem = menu.AddItem(in label, (Uri?)null, impulseProxy.ElementContentType.GetTypeColor());
                                menuItem.Button.LocalPressed += (button, data) =>
                                {
                                    __instance.SpawnNode(item.node, n =>
                                    {
                                        n.TryConnectImpulse(impulseProxy.NodeImpulse.Target, n.GetOperation(0), undoable: true);
                                        __instance.LocalUser.CloseContextMenu(__instance);
                                        CleanupDraggedWire(__instance);
                                    });
                                };
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

    internal static IEnumerable<MenuItem> MenuItems(ProtoFluxTool __instance)
    {
        var _currentProxy = Traverse.Create(__instance).Field("_currentProxy").GetValue<SyncRef<ProtoFluxElementProxy>>();
        var target = _currentProxy?.Target;

        if (target is ProtoFluxInputProxy inputProxy)
        {
            if (inputProxy.InputType.Value == typeof(User))
            {
                yield return new MenuItem(typeof(LocalUser));
                yield return new MenuItem(typeof(HostUser));
                yield return new MenuItem(typeof(UserFromUsername));
            }
        }

        if (target is ProtoFluxOutputProxy outputProxy)
        {
            if (outputProxy.OutputType.Value == typeof(Slot))
            {
                yield return new MenuItem(typeof(GlobalTransform));
                yield return new MenuItem(typeof(GetForward));
            }
        }

        if (target is ProtoFluxImpulseProxy)
        {
            yield return new MenuItem(typeof(For));
            yield return new MenuItem(typeof(If));
            yield return new MenuItem(typeof(ValueWrite<dummy>));
            yield return new MenuItem(typeof(Sequence));
        }

        if (target is ProtoFluxOperationProxy)
        {

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

}