using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using Harmony;
using KoiSkinOverlayX;
using MakerAPI;
using MakerAPI.Chara;
using MessagePack;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace KoiClothesOverlayX
{
    [RequireComponent(typeof(ChaControl))]
    public class KoiClothesOverlayController : CharaCustomFunctionController
    {
        // todo change based on coord event, maybe listen for updates in ui? or not necessary
        private Dictionary<ChaFileDefine.CoordinateType, Dictionary<ClothesTexId, Texture2D>> _allOverlayTextures;

        private Dictionary<ClothesTexId, Texture2D> CurrentOverlayTextures
        {
            get
            {
                if (_allOverlayTextures == null) return null;

                var coordinateType = (ChaFileDefine.CoordinateType)ChaControl.fileStatus.coordinateType;
                _allOverlayTextures.TryGetValue(coordinateType, out var dict);

                if (dict == null)
                {
                    dict = new Dictionary<ClothesTexId, Texture2D>();
                    _allOverlayTextures.Add(coordinateType, dict);
                }

                return dict;
            }
        }

        private ClothesTexId _dumpClothesId;
        private Action<byte[]> _dumpCallback;

        public void SetOverlayTex(Texture2D tex, ClothesTexId texType)
        {
            if (tex == null)
                CurrentOverlayTextures.Remove(texType);
            else
                CurrentOverlayTextures[texType] = tex;

            RefreshTexture(texType);
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            // Let the previously loaded values get copied if not in maker since there's no way for them to be changed
            if (currentGameMode != GameMode.Maker) return;

            var data = new PluginData();
            data.version = 1;

            foreach (var dict in _allOverlayTextures)
            {
                if (dict.Value != null && dict.Value.Count > 0)
                {
                    data.data.Add(dict.Key.ToString(), dict.Value.ToDictionary(x => MessagePackSerializer.Serialize(x.Key), x => x.Value.EncodeToPNG()));
                }
            }

            SetExtendedData(data);
        }

        protected override void OnReload(GameMode currentGameMode)
        {
            if (_allOverlayTextures != null)
            {
                foreach (var textures in _allOverlayTextures)
                {
                    foreach (var texture in textures.Value)
                    {
                        Destroy(texture.Value);
                    }
                }
            }

            _allOverlayTextures = new Dictionary<ChaFileDefine.CoordinateType, Dictionary<ClothesTexId, Texture2D>>();

            var data = GetExtendedData();

            if (data?.data != null)
            {
                foreach (ChaFileDefine.CoordinateType coord in Enum.GetValues(typeof(ChaFileDefine.CoordinateType)))
                {
                    if (data.data.TryGetValue(coord.ToString(), out var obj) && obj is Dictionary<object, object> overlayBytes)
                    {
                        _allOverlayTextures.Add(coord, overlayBytes.ToDictionary(pair => MessagePackSerializer.Deserialize<ClothesTexId>((byte[])pair.Key), pair => Util.TextureFromBytes((byte[])pair.Value)));
                    }
                }
            }

            RefreshAllTextures();
        }

        public void RefreshAllTextures()
        {
            for (var i = 0; i < ChaControl.cusClothesCmp.Length; i++)
                ChaControl.ChangeCustomClothes(true, i, true, false, false, false, false);

            for (int i = 0; i < ChaControl.cusClothesSubCmp.Length; i++)
                ChaControl.ChangeCustomClothes(false, i, true, false, false, false, false);
        }

        public ChaClothesComponent GetCustomClothesComponent(string clothesObjectName)
        {
            return ChaControl.cusClothesCmp.Concat(ChaControl.cusClothesSubCmp).FirstOrDefault(x => x != null && x.gameObject.name == clothesObjectName);
        }

        public void RefreshTexture(ClothesTexId texType)
        {
            var i = Array.FindIndex(ChaControl.objClothes, x => x.name == texType.ClothesName);
            if (i >= 0)
                ChaControl.ChangeCustomClothes(true, i, true, false, false, false, false);
            else
            {
                i = Array.FindIndex(ChaControl.objParts, x => x.name == texType.ClothesName);
                if (i >= 0)
                    ChaControl.ChangeCustomClothes(false, i, true, false, false, false, false);
                else
                {
                    Logger.Log(LogLevel.Error | LogLevel.Message, "This should not have happened");
                    RefreshAllTextures();
                }
            }
        }

        private void ApplyOverlays(ChaClothesComponent clothesCtrl)
        {
            if (CurrentOverlayTextures == null) return;

            var clothesName = clothesCtrl.name;

            var rendererArrs = GetRendererArrays(clothesCtrl);

            if (_dumpCallback != null && _dumpClothesId.ClothesName == clothesName)
                DumpBaseTextureImpl(rendererArrs);

            var overlays = CurrentOverlayTextures.Where(x => x.Key.ClothesName == clothesName).ToList();

            for (var i = 0; i < rendererArrs.Length; i++)
            {
                var renderers = rendererArrs[i];
                foreach (var overlay in overlays.Where(x => x.Key.RendererGroup == (ClothesRendererGroup)i))
                {
                    if (renderers.Length > overlay.Key.RendererId)
                    {
                        var mat = renderers[overlay.Key.RendererId].material;
                        KoiSkinOverlayController.ApplyOverlay((RenderTexture)mat.mainTexture, overlay.Value);
                    }
                    else
                    {
                        Logger.Log(MakerAPI.MakerAPI.Instance.InsideMaker ? LogLevel.Warning | LogLevel.Message : LogLevel.Debug, $"[KCOX] Removing unused overlay for {overlay.Key.ClothesName}");
                        Destroy(overlay.Value);
                        overlays.Remove(overlay);
                    }
                }
            }
        }

        public static Renderer[][] GetRendererArrays(ChaClothesComponent clothesCtrl)
        {
            return new[] { clothesCtrl.rendNormal01, clothesCtrl.rendNormal02, clothesCtrl.rendAlpha01, clothesCtrl.rendAlpha02 };
        }

        private void DumpBaseTextureImpl(Renderer[][] rendererArrs)
        {
            try
            {
                var renderer = rendererArrs.ElementAtOrDefault((int)_dumpClothesId.RendererGroup)?.ElementAtOrDefault(_dumpClothesId.RendererId);
                if (renderer == null) throw new Exception("Specified renderer doesn't exist");

                var renderTexture = (RenderTexture)renderer.material.mainTexture;

                var act = RenderTexture.active;
                RenderTexture.active = renderTexture;

                var tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
                tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);

                RenderTexture.active = act;

                var png = tex.EncodeToPNG();

                Destroy(tex);

                _dumpCallback(png);
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Error | LogLevel.Message, "Dumping texture failed - " + e.Message);
                Logger.Log(LogLevel.Debug, e);
                RenderTexture.active = null;
            }
            finally
            {
                _dumpCallback = null;
            }
        }

        internal static class Hooks
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(global::ChaControl.ChangeCustomClothes))]
            public static void ChangeCustomClothesPostHook(ChaControl __instance, bool main, int kind)
            {
                var controller = __instance.GetComponent<KoiClothesOverlayController>();
                if (controller == null) return;

                var clothesCtrl = GetCustomClothesComponent(__instance, main, kind);
                if (clothesCtrl == null) return;

                controller.ApplyOverlays(clothesCtrl);
            }

            public static void Init(string guid)
            {
                HarmonyInstance.Create(guid).PatchAll(typeof(Hooks));
            }

            private static ChaClothesComponent GetCustomClothesComponent(ChaControl chaControl, bool main, int kind)
            {
                /* for top clothes it fires once at start with first bool true (main load), then for each subpart with bool false
                * if true, objClothes are used, if false objParts
                * ignore 0 main, handle separate sub parts instead
                */

                if (main)
                {
                    if (kind == 0)
                        return null;

                    return chaControl.GetCustomClothesComponent(kind);
                }

                return chaControl.GetCustomClothesSubComponent(kind);
            }
        }

        public Texture2D GetOverlayTex(ClothesTexId clothesId)
        {
            if (CurrentOverlayTextures != null)
            {
                CurrentOverlayTextures.TryGetValue(clothesId, out var tex);
                return tex;
            }
            return null;
        }

        public void DumpBaseTexture(ClothesTexId clothesId, Action<byte[]> callback)
        {
            _dumpCallback = callback;
            _dumpClothesId = clothesId;

            // Force redraw to trigger the dump
            RefreshTexture(clothesId);
        }

        public Renderer GetRenderer(ClothesTexId clothesTexId)
        {
            var ccc = GetCustomClothesComponent(clothesTexId.ClothesName);
            if (ccc != null)
            {
                var arr = GetRendererArrays(ccc).ElementAtOrDefault((int)clothesTexId.RendererGroup);
                if (arr != null)
                    return arr.ElementAtOrDefault(clothesTexId.RendererId);
            }
            return null;
        }
    }
}
