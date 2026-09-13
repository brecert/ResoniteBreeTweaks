using HarmonyLib;

namespace BreeTweaks.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class TweakCategory(string name, string description, bool defaultValue = true, bool hidden = false) : HarmonyPatchCategory(category: name)
{
  public readonly string Description = description;
  public readonly bool DefaultValue = defaultValue;
  public readonly bool Hidden = hidden;
}
