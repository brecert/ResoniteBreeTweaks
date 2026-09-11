using HarmonyLib;

namespace BreeTweaks.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class TweakCategory(string name, string description, bool defaultValue = true) : HarmonyPatchCategory(category: name)
{
  public readonly string Description = description;
  public readonly bool DefaultValue = defaultValue;
}
