using System;

namespace BreeTweaks.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class TweakCategoryAttribute : Attribute
{
  public string Description { get; }
  public bool DefaultValue { get; }

  public TweakCategoryAttribute(string description, bool defaultValue = true)
  {
    Description = description;
    DefaultValue = defaultValue;
  }
}