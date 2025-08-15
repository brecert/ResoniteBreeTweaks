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
using FrooxEngine.UIX;
using System.Runtime.CompilerServices;
using FrooxEngine.FinalIK;

namespace BreeTweaks.Patches;

// Use Global Distance for Earmuff Mode
[HarmonyPatchCategory("Additional BipedRig Actions"), TweakCategory("Adds 'Detect from Rig' and 'Setup VRIK' actions to the BipedRig component.")]
[HarmonyPatch(typeof(BipedRig))]
internal static class DetectBipedRigButtonPatch
{
  [HarmonyPostfix]
  [HarmonyPatch(nameof(BipedRig.BuildInspectorUI))]
  static void BuildInspectorUI(BipedRig __instance, UIBuilder ui)
  {
    var button = ui.Button("Detect from Rig (Mod)");
    button.LocalPressed += (_, _) => DetectFromRig(__instance);

    var setupVRIK = ui.Button("Setup VRIK (Mod)");
    setupVRIK.LocalPressed += (_, _) =>
    {
      var vrik = __instance.Slot.GetComponentOrAttach<VRIK>();
      vrik.Initiate();
    };
  }

  private static void DetectFromRig(BipedRig __instance)
  {
    var rig = __instance.Slot.GetComponentInParentsOrChildren<Rig>();

    if (rig is not null)
    {
      foreach (var bone in rig.GenerateBoneHierarchies())
      {
        ClassifyBiped(bone, __instance);
        AssignBones(__instance, bone, ignoreDuplicates: false);
      }
    }
  }

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(BipedRig), "ClassifyBiped", [typeof(BoneNode), typeof(BipedRig)])]
  [MethodImpl(MethodImplOptions.NoInlining)]
  static void ClassifyBiped(BoneNode node, BipedRig rig) =>
    throw new NotImplementedException();

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(BipedRig), "AssignBones")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  static void AssignBones(BipedRig __instance, BoneNode root, bool ignoreDuplicates) =>
    throw new NotImplementedException();

}