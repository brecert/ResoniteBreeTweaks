using System.Reflection;

using BreeTweaks.Attributes;

using Elements.Core;

using HarmonyLib;

using ResoniteModLoader;

namespace BreeTweaks;

using System.Collections.Generic;

using BreeTweaks.Patches;


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

  [AutoRegisterConfigKey]
  internal static ModConfigurationKey<bool> IsProtoFluxEnabledKey = new("IsProtoFluxEnabled", "Is ProtoFlux Enabled?", computeDefault: () => true);
  public static bool IsProtoFluxEnabled => IsProtoFluxEnabledKey.Value;

  [AutoRegisterConfigKey]
  internal static ModConfigurationKey<bool> AreCamerasEnabledKey = new("AreCamerasEnabled", "Are Cameras Enabled?", computeDefault: () => true);
  public static bool AreCamerasEnabled => AreCamerasEnabledKey.Value;

  internal static ModConfiguration? Config;

  private static readonly Dictionary<string, ModConfigurationKey<bool>> PatchCategoryKeys;
  // private static readonly HashSet<ModConfigurationKey> NestedAutoKeys;

  static ResoniteBreeTweaksMod()
  {
    DebugFunc(() => $"Static Initializing {nameof(ResoniteBreeTweaksMod)}...");

    var types = AccessTools.GetTypesFromAssembly(ModAssembly);

    PatchCategoryKeys = types
      .Select(t => t.GetCustomAttribute<TweakCategory>())
      .OfType<TweakCategory>()
      .Select(t => new ModConfigurationKey<bool>(t.info.category, t.Description, () => t.DefaultValue, internalAccessOnly: t.Hidden))
      .ToDictionary(k => k.Name);

    // NestedAutoKeys = types
    //   .SelectMany(t => t.GetFields())
    //   .Where(f => Attribute.IsDefined(f, typeof(AutoRegisterConfigKeyAttribute)))
    //   .Select(f => f.GetValue(null))
    //   .Cast<ModConfigurationKey>()
    //   .ToHashSet();
  }

  public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
  {
    foreach (var key in PatchCategoryKeys.Values)
    {
      DebugFunc(() => $"Adding configuration key for {key.Name}...");
      builder.Key(key);
    }
  }


  public override void OnEngineInit()
  {
    Config = GetConfiguration()!; // todo: tired, fix
    Config.OnThisConfigurationChanged += OnConfigChanged;
    AreCamerasEnabledKey.OnChanged += UpdateCameras;

    PatchCategories();

#if DEBUG
    HotReloader.RegisterForHotReload(this);
#endif
  }

#if DEBUG
  protected static void BeforeHotReload()
  {
    AreCamerasEnabledKey.OnChanged -= UpdateCameras;
    harmony.UnpatchAll(HarmonyId);
  }

  protected static void OnHotReload(ResoniteMod modInstance)
  {
    PatchCategories();
    AreCamerasEnabledKey.OnChanged += UpdateCameras;
    UpdateCameras(null);
  }
#endif

  protected static void PatchCategories()
  {
    foreach (var (category, key) in PatchCategoryKeys)
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
    if (change.Key is ModConfigurationKey<bool> key && PatchCategoryKeys.ContainsKey(key.Name))
    {
      UpdatePatch(key.Name, change.Config.GetValue(key));
    }
  }

  static void UpdateCameras(object? _) => CamerasEnabledPatch.UpdateCameras();
}
