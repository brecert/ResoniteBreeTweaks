using System.Reflection;

using BreeTweaks.Attributes;

using Elements.Core;

using HarmonyLib;

using ResoniteModLoader;

namespace BreeTweaks;

using System.Collections.Generic;


#if DEBUG
using ResoniteHotReloadLib;
#endif

public class ResoniteBreeTweaksMod : ResoniteMod
{
  private static Assembly ModAssembly => typeof(ResoniteBreeTweaksMod).Assembly;

  public override string Name => ModAssembly.GetCustomAttribute<AssemblyTitleAttribute>()!.Title;
  public override string Author => ModAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
  public override string Version => ModAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
  public override string Link => ModAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()!.First(meta => meta.Key == "RepositoryUrl").Value!;

  internal static string HarmonyId => $"dev.bree.{ModAssembly.GetName()}";

  private static readonly Harmony harmony = new(HarmonyId);

  private static ModConfiguration? Config;

  private static readonly Dictionary<string, ModConfigurationKey<bool>> patchCategoryKeys = [];
  // private static readonly Dictionary<ModConfigurationKey, FieldInfo> patchOptionKeys = [];

  static ResoniteBreeTweaksMod()
  {
    DebugFunc(() => $"Static Initializing {nameof(ResoniteBreeTweaksMod)}...");

    var types = AccessTools.GetTypesFromAssembly(ModAssembly);

    var categoryKeys = types
      .Select(t => t.GetCustomAttribute<TweakCategory>())
      .OfType<TweakCategory>()
      .Select(t => new ModConfigurationKey<bool>(t.info.category, t.Description, () => t.DefaultValue));

    foreach (var key in categoryKeys)
    {
      DebugFunc(() => $"Registering patch category {key.Name}...");
      patchCategoryKeys[key.Name] = key;
    }
  }

  public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
  {
    foreach (var key in patchCategoryKeys.Values)
    {
      DebugFunc(() => $"Adding configuration key for {key.Name}...");
      builder.Key(key);
    }
  }


  public override void OnEngineInit()
  {
    Config = GetConfiguration()!; // todo: tired, fix
    Config.OnThisConfigurationChanged += OnConfigChanged;

    PatchCategories();

#if DEBUG
    HotReloader.RegisterForHotReload(this);
#endif
  }


#if DEBUG
  protected static void BeforeHotReload() =>
    harmony.UnpatchAll(HarmonyId);

  protected static void OnHotReload(ResoniteMod modInstance) =>
    PatchCategories();
#endif

  protected static void PatchCategories()
  {
    foreach (var (category, key) in patchCategoryKeys)
    {
      if (Config?.GetValue(key) ?? true) // enable if fail?
      {
        harmony.PatchCategory(ModAssembly, category);
      }
    }
  }

  protected static void UpdatePatch(string category, bool enabled)
  {
    try
    {

      if (enabled)
      {
        DebugFunc(() => $"Patching {category}...");
        harmony.PatchCategory(category);
      }
      else
      {
        DebugFunc(() => $"Unpatching {category}...");
        harmony.UnpatchCategory(category);
      }
    }
    catch (Exception e)
    {
      Error(e);
    }
  }

  private static void OnConfigChanged(ConfigurationChangedEvent change)
  {
    if (change.Key is ModConfigurationKey<bool> key)
    {
      UpdatePatch(key.Name, change.Config.GetValue(key));
    }
  }

}
