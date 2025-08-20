using System.Collections.Generic;
using BreeTweaks.Attributes;
using FrooxEngine;
using HarmonyLib;
using Elements.Assets;
using Elements.Core;
using System;
using System.Threading.Tasks;
using Renderite.Shared;

[HarmonyPatch(typeof(CubemapCreator), "OnAttach")]
[HarmonyPatchCategory("Live Cubemap Previews"), TweakCategory("Displays a live preview of what the cubemap will look like, updating every time a change is made.")]
class Cubemap_Preview_Patch
{
  internal static void Postfix(CubemapCreator __instance)
  {
    var slot = __instance.Slot.AddSlot("Cubemap");
    var cubemap = slot.AttachComponent<StaticCubemap>();
    CubemapImporter.CreateSphere(slot, cubemap);
    slot.LocalScale = new float3(25000, 25000, 25000);
    slot.LocalPosition += new float3(1500, 0, 0);

    __instance.Changed += (a) =>
    {
      CreatePreviewCubemap(__instance, cubemap);
    };
  }
  static Bitmap2D EmptyBitmap2D => new(4, 4, TextureFormat.RGBA32, false, ColorProfile.Linear);

  internal static void CreatePreviewCubemap(CubemapCreator __instance, StaticCubemap cubemap)
  {
    __instance.StartTask(async delegate
    {
      await default(ToBackground);
      try
      {
        List<Bitmap2D?> bitmaps = [];
        List<Bitmap2D?> list = bitmaps;
        list.Add(await (__instance.PosX.Asset?.GetOriginalTextureData() ?? Task.Run(() => EmptyBitmap2D)));
        list.Add(await (__instance.NegX.Asset?.GetOriginalTextureData() ?? Task.Run(() => EmptyBitmap2D)));
        list.Add(await (__instance.PosY.Asset?.GetOriginalTextureData() ?? Task.Run(() => EmptyBitmap2D)));
        list.Add(await (__instance.NegY.Asset?.GetOriginalTextureData() ?? Task.Run(() => EmptyBitmap2D)));
        list.Add(await (__instance.PosZ.Asset?.GetOriginalTextureData() ?? Task.Run(() => EmptyBitmap2D)));
        list.Add(await (__instance.NegZ.Asset?.GetOriginalTextureData() ?? Task.Run(() => EmptyBitmap2D)));
        bitmaps = list;
        int num = 0;
        TextureFormat? textureFormat = null;
        bool mipmaps = true;
        foreach (var item in bitmaps)
        {
          if (item != null)
          {
            num = MathX.Max(num, item.Size.x, item.Size.y);
            if (!textureFormat.HasValue)
            {
              textureFormat = item.Format;
            }
            if (!item.HasMipMaps)
            {
              mipmaps = false;
            }
          }
        }
        num = MathX.NearestPowerOfTwo(num);
        for (int i = 0; i < bitmaps.Count; i++)
        {
          int index = i;
          var bitmap2D = bitmaps[i];
          Bitmap2D? value;
          if (bitmap2D == null)
          {
            value = null;
          }
          else
          {
            int2 v = int2.One;
            value = bitmap2D.GetRescaled(v * num);
          }
          bitmaps[index] = value;
        }
        int index2 = 2;
        int index3 = 3;
        switch (__instance.TopBottomRotation.Value)
        {
          case CubemapCreator.Rotation.Rotate180:
            bitmaps[index2] = bitmaps[index2]?.Rotate180();
            bitmaps[index3] = bitmaps[index3]?.Rotate180();
            break;
          case CubemapCreator.Rotation.Rotate90CW:
            bitmaps[index2] = bitmaps[index2]?.Rotate90CW();
            bitmaps[index3] = bitmaps[index3]?.Rotate90CCW();
            break;
          case CubemapCreator.Rotation.Rotate90CCW:
            bitmaps[index2] = bitmaps[index2]?.Rotate90CCW();
            bitmaps[index3] = bitmaps[index3]?.Rotate90CW();
            break;
        }
        var bitmapCube = new BitmapCube(num, num, textureFormat ?? TextureFormat.RGBA32, mipmaps, bitmaps[0]?.Profile ?? ColorProfile.Linear);

        for (int j = 0; j < bitmaps.Count; j++)
        {
          var bitmap2D2 = bitmaps[j];
          if (bitmap2D2 != null)
          {
            bitmapCube.FillFrom(bitmap2D2, (BitmapCube.Face)j, mipmaps);
          }
        }
        Uri asset = await __instance.Engine.LocalDB.SaveAssetAsync(bitmapCube).ConfigureAwait(continueOnCapturedContext: false);
        await default(ToWorld);
        cubemap.URL.Value = asset;
      }
      catch (Exception ex)
      {
        UniLog.Error("Exception creating cubemap:\n" + ex);
        await default(ToWorld);
      }
    });
  }
}
