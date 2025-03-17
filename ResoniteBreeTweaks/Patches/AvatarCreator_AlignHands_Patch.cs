using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BreeTweaks.Attributes;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.FinalIK;
using HarmonyLib;

[HarmonyPatch(typeof(AvatarCreator), "AlignHands")]
[HarmonyPatchCategory("AvatarCreator AlignHands Fix"), TweakCategory("Fixes AlignHands to face the correct direction when suing AlignHands.")]
class AvatarCreator_AlignHands_Patch
{

  internal static void Postfix(AvatarCreator __instance, IButton button, ButtonEventData eventData, SyncRef<Slot> ____leftReference, SyncRef<Slot> ____rightReference)
  {
    if (TryGetBipedFromHead(__instance, out var headsetObjects) is BipedRig bipedRig && bipedRig.IsValid)
    {
      var vrik = bipedRig.Slot.GetComponentInChildrenOrParents<VRIK>();
      var leftArm = vrik.Solver.leftArm;
      var rightArm = vrik.Solver.rightArm;

      vrik.Solver.GuessHandOrientations(false);

      Slot leftReference = ____leftReference.Target;
      Slot rightReference = ____rightReference.Target;

      {
        var forearm = vrik.Solver.BoneReferences.leftForearm.Target.GlobalPosition;
        var hand = vrik.Solver.BoneReferences.leftHand.Target.GlobalPosition;
        var dir = forearm - hand;
        var rot = floatQ.LookRotation(in dir);
        rot *= floatQ.AxisAngle(leftArm.WristToPalmAxis.Value, 180);
        rot *= floatQ.AxisAngle(leftArm.PalmToThumbAxis.Value, 180);
        leftReference.GlobalRotation = rot;
      }

      {
        var forearm = vrik.Solver.BoneReferences.rightForearm.Target.GlobalPosition;
        var hand = vrik.Solver.BoneReferences.rightHand.Target.GlobalPosition;
        var dir = forearm - hand;
        var rot = floatQ.LookRotation(in dir);
        rot *= floatQ.AxisAngle(rightArm.WristToPalmAxis.Value, 180);
        rot *= floatQ.AxisAngle(rightArm.PalmToThumbAxis.Value, 180);
        rightReference.GlobalRotation = rot;
      }
    }
  }

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(AvatarCreator), "TryGetBipedFromHead")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static BipedRig TryGetBipedFromHead(AvatarCreator instance, out List<Slot> headsetObjects) => throw new NotImplementedException();
}
