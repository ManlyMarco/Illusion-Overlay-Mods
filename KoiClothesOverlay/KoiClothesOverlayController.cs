using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using KoiSkinOverlayX;
using MakerAPI;
using MakerAPI.Chara;
using MessagePack;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace KoiClothesOverlayX
{
    [RequireComponent(typeof(ChaControl))]
    public partial class KoiClothesOverlayController : CharaCustomFunctionController
    {
        private const string OverlayDataKey = "Overlays";
        
        private Dictionary<ChaFileDefine.CoordinateType, Dictionary<ClothesTexId, ClothesTexData>> _allOverlayTextures;

        private Dictionary<ClothesTexId, ClothesTexData> CurrentOverlayTextures
        {
            get
            {
                if (_allOverlayTextures == null) return null;

                var coordinateType = (ChaFileDefine.CoordinateType)ChaControl.fileStatus.coordinateType;
                _allOverlayTextures.TryGetValue(coordinateType, out var dict);

                if (dict == null)
                {
                    dict = new Dictionary<ClothesTexId, ClothesTexData>();
                    _allOverlayTextures.Add(coordinateType, dict);
                }

                return dict;
            }
        }

        private ClothesTexId _dumpClothesId;
        private Action<byte[]> _dumpCallback;

        public void SetOverlayTex(ClothesTexData tex, ClothesTexId texType)
        {
            if (CurrentOverlayTextures.TryGetValue(texType, out var existing))
            {
                if (existing != null && existing.Texture != tex?.Texture)
                    Destroy(existing.Texture);
            }

            if (tex == null || tex.IsEmpty())
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

            CleanupTextureList();
            data.data.Add(OverlayDataKey, MessagePackSerializer.Serialize(_allOverlayTextures));

            SetExtendedData(data);
        }

        private void CleanupTextureList()
        {
            foreach (var group in _allOverlayTextures.Values)
            {
                foreach (var texture in group.Where(x => x.Value.IsEmpty()).ToList())
                    group.Remove(texture.Key);
            }

            foreach (var group in _allOverlayTextures.Where(x => !x.Value.Any()).ToList())
                _allOverlayTextures.Remove(group.Key);
        }

        protected override void OnReload(GameMode currentGameMode)
        {
            if (currentGameMode == GameMode.Maker && !KoiClothesOverlayGui.MakerLoadFromCharas) return;

            if (_allOverlayTextures != null)
            {
                foreach (var textures in _allOverlayTextures)
                {
                    foreach (var texture in textures.Value)
                    {
                        Destroy(texture.Value.Texture);
                    }
                }
                _allOverlayTextures = null;
            }
            
            var pd = GetExtendedData();
            if (pd != null && pd.data.TryGetValue(OverlayDataKey, out var overlayData))
            {
                if (overlayData is byte[] overlayBytes)
                {
                    try
                    {
                        _allOverlayTextures = MessagePackSerializer.Deserialize<
                            Dictionary<ChaFileDefine.CoordinateType, Dictionary<ClothesTexId, ClothesTexData>>>(
                            overlayBytes);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Warning | LogLevel.Message, "[KCOX] Failed to deserialize overlay data for " + (ChaFileControl?.charaFileName ?? "?"));
                        Logger.Log(LogLevel.Debug, ex);
                    }
                }
            }

            if (_allOverlayTextures == null)
                _allOverlayTextures = new Dictionary<ChaFileDefine.CoordinateType, Dictionary<ClothesTexId, ClothesTexData>>();

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
            if (texType?.ClothesName != null)
            {
                var i = Array.FindIndex(ChaControl.objClothes, x => x != null && x.name == texType.ClothesName);
                if (i >= 0)
                {
                    ChaControl.ChangeCustomClothes(true, i, true, false, false, false, false);
                    return;
                }

                i = Array.FindIndex(ChaControl.objParts, x => x != null && x.name == texType.ClothesName);
                if (i >= 0)
                {
                    ChaControl.ChangeCustomClothes(false, i, true, false, false, false, false);
                    return;
                }
            }

            RefreshAllTextures();
        }

        private void ApplyOverlays(ChaClothesComponent clothesCtrl)
        {
            if (CurrentOverlayTextures == null) return;

            var clothesName = clothesCtrl.name;

            var rendererArrs = GetRendererArrays(clothesCtrl);

            if (_dumpCallback != null && _dumpClothesId.ClothesName == clothesName)
                DumpBaseTextureImpl(rendererArrs);

            if (CurrentOverlayTextures.Count == 0) return;
            
            var overlays = CurrentOverlayTextures.Where(x => x.Key.ClothesName == clothesName).ToList();

            for (var i = 0; i < rendererArrs.Length; i++)
            {
                var renderers = rendererArrs[i];
                foreach (var overlay in overlays.Where(x => x.Key.RendererGroup == (ClothesRendererGroup)i).ToList())
                {
                    if (renderers.Length > overlay.Key.RendererId)
                    {
                        var mat = renderers[overlay.Key.RendererId].material;

                        var mainTexture = (RenderTexture)mat.mainTexture;
                        if (mainTexture == null) return;

                        if (overlay.Value.Override)
                        {
                            var rta = RenderTexture.active;
                            RenderTexture.active = mainTexture;
                            GL.Clear(false, true, Color.clear);
                            RenderTexture.active = rta;
                        }

                        if (overlay.Value.Texture != null)
                            KoiSkinOverlayController.ApplyOverlay(mainTexture, overlay.Value.Texture);
                    }
                    else
                    {
                        Logger.Log(MakerAPI.MakerAPI.Instance.InsideMaker ? LogLevel.Warning | LogLevel.Message : LogLevel.Debug, $"[KCOX] Removing unused overlay for {overlay.Key.ClothesName}");
                        Destroy(overlay.Value.Texture);
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

        public ClothesTexData GetOverlayTex(ClothesTexId clothesId)
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
