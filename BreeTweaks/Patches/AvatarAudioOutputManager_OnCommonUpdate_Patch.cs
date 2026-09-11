using FrooxEngine;
using BreeTweaks.Attributes;
using HarmonyLib;
using FrooxEngine.CommonAvatar;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace BreeTweaks.Patches;

// Use Global Distance for Earmuff Mode
[HarmonyPatchCategory("Earmuff Global Distance"), TweakCategory("Modified earmuff mode to use the global distance space, rather than scaling the distance by the local scale.")]
[HarmonyPatch(typeof(AvatarAudioOutputManager), "OnCommonUpdate")]
internal static class AvatarAudioOutputManager_OnCommonUpdate_Patch
{
    // [AutoRegisterConfigKey]
    // private static readonly ModConfigurationKey<bool> Enabled = new("Enabled", "Should the global distance be used instead of the local scale.", () => true);

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);
        var refFloat = 0f;

        var propertyInfo = typeof(UserRoot).GetProperty(nameof(UserRoot.GlobalScale));

        codeMatcher
            .MatchEndForward([
                CodeMatch.LoadsConstant(1f),
                CodeMatch.Branches(),
                CodeMatch.Calls(propertyInfo.GetGetMethod()),
                CodeMatch.StoresLocal(),
            ])
            .ThrowIfInvalid("Unable to find GlobalScale IL match.");

        // ensured to be a local due to throw earlier?
        // codeMatcher.Instruction.LocalIndex() reads as a float32 and fails conversion so we do this instead
        var localIndex = (codeMatcher.Instruction.operand as LocalBuilder)!.LocalIndex;

        // advance to the next instruction so that the store local actually stores
        codeMatcher.Advance(1);

        codeMatcher.InsertAndAdvance([
            CodeInstruction.LoadLocal(localIndex, true),
            CodeInstruction.Call(() => ModifyScale(ref refFloat)),
        ]);

        return codeMatcher.Instructions();
    }

    public static void ModifyScale(ref float scale)
    {
        scale = 1;
    }
}