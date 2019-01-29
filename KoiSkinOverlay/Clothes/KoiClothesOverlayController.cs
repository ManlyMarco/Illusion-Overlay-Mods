using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using Harmony;
using MakerAPI;
using MakerAPI.Chara;
using UnityEngine;

namespace KoiSkinOverlayX.Clothes
{
    [RequireComponent(typeof(ChaControl))]
    public class KoiClothesOverlayController : CharaCustomFunctionController
    {
        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            var data = new PluginData();
            data.version = 1;
            data.data.Add("Overlays", _overlayTextures.Select(x => new KeyValuePair<ClothesTexId, byte[]>(x.Key, x.Value.EncodeToPNG())).ToArray());

            SetExtendedData(data);
        }

        protected override void OnReload(GameMode currentGameMode)
        {
            var data = GetExtendedData();

            if (data.data.TryGetValue("Overlays", out var obj) && obj is KeyValuePair<ClothesTexId, byte[]>[] overlayBytes)
                _overlayTextures = overlayBytes.ToDictionary(pair => pair.Key, pair => Util.TextureFromBytes(pair.Value));
            else
                _overlayTextures = new Dictionary<ClothesTexId, Texture2D>();
        }

        private Dictionary<ClothesTexId, Texture2D> _overlayTextures;

        private void UpdateTextures(ChaClothesComponent clothesCtrl)
        {
            var clothesName = clothesCtrl.name;

            var textures = _overlayTextures.Where(x => x.Key.ClothesName == clothesName).ToList();

            var rendererArrs = new[] { clothesCtrl.rendNormal01, clothesCtrl.rendNormal02, clothesCtrl.rendAlpha01, clothesCtrl.rendAlpha02 };

            for (var i = 0; i < rendererArrs.Length; i++)
            {
                var renderers = rendererArrs[i];
                foreach (var texture in textures.Where(x => x.Key.RendererGroup == (ClothesRendererGroup)i))
                {
                    if (renderers.Length > texture.Key.RendererId)
                    {
                        //var testTex = Util.TextureFromBytes(File.ReadAllBytes(@"d:\test.png"));
                        var mat = renderers[texture.Key.RendererId].material;
                        // bug mem leak, slow in maker when changing colors. Needs different approach?
                        var rt = new RenderTexture(mat.mainTexture.width, mat.mainTexture.height, 8);
                        mat.mainTexture = KoiSkinOverlayController.ApplyOverlay(mat.mainTexture, rt, texture.Value);
                    }
                    else
                    {
                        // todo handle properly
                        BepInEx.Logger.Log(LogLevel.Warning | LogLevel.Message, $"Unused clothes overlay for {texture.Key.ClothesName} - {texture.Key.RendererGroup} - {texture.Key.RendererId}");
                    }
                }
            }
        }

        internal static class Hooks
        {
            public static void Init(string guid)
            {
                HarmonyInstance.Create(guid).PatchAll(typeof(Hooks));
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(global::ChaControl.ChangeCustomClothes))]
            public static void ChangeCustomClothesPostHook(ChaControl __instance, bool main, int kind)
            {
                var controller = __instance.GetComponent<KoiClothesOverlayController>();
                if (controller == null) return;

                var clothesCtrl = GetClothingRootGo(__instance, main, kind)?.GetComponent<ChaClothesComponent>();
                if (clothesCtrl == null) return;

                controller.UpdateTextures(clothesCtrl);
            }

            private static GameObject GetClothingRootGo(ChaControl __instance, bool main, int kind)
            {
                /* for top clothes it fires once at start with first bool true (main load), then for each subpart with bool false
                * if true, objClothes are used, if false objParts
                * ignore 0 main, handle separate sub parts instead
                */

                if (main)
                {
                    if (kind == 0)
                        return null;

                    return __instance.objClothes.ElementAtOrDefault(kind);
                }

                return __instance.objParts.ElementAtOrDefault(kind);
            }
        }
    }
}