using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KoiSkinOverlayX;
using MessagePack;
using UnityEngine;
using Logger = KoiClothesOverlayX.KoiClothesOverlayMgr;
#if KK
using ExtensibleSaveFormat;
using CoordinateType = ChaFileDefine.CoordinateType;
#elif EC
using EC.Core.ExtensibleSaveFormat;
using CoordinateType = KoikatsuCharaFile.ChaFileDefine.CoordinateType;
#endif

namespace KoiClothesOverlayX
{
    public partial class KoiClothesOverlayController : CharaCustomFunctionController
    {
        private const string OverlayDataKey = "Overlays";

        private Action<byte[]> _dumpCallback;
        private string _dumpClothesId;

        private Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> _allOverlayTextures;
        private Dictionary<string, ClothesTexData> CurrentOverlayTextures
        {
            get
            {
                if (_allOverlayTextures == null) return null;

#if KK
                // Need to do this instead of polling the CurrentCoordinate prop because it's updated too late
                var coordinateType = (CoordinateType)ChaControl.fileStatus.coordinateType;
#elif EC
                var coordinateType = CoordinateType.School01;
#endif
                _allOverlayTextures.TryGetValue(coordinateType, out var dict);

                if (dict == null)
                {
                    dict = new Dictionary<string, ClothesTexData>();
                    _allOverlayTextures.Add(coordinateType, dict);
                }

                return dict;
            }
        }

        public void DumpBaseTexture(string clothesId, Action<byte[]> callback)
        {
            _dumpCallback = callback;
            _dumpClothesId = clothesId;

            // Force redraw to trigger the dump
            RefreshTexture(clothesId);
        }

        public ChaClothesComponent GetCustomClothesComponent(string clothesObjectName)
        {
            return ChaControl.cusClothesCmp.Concat(ChaControl.cusClothesSubCmp).FirstOrDefault(x => x != null && x.gameObject.name == clothesObjectName);
        }

        public ClothesTexData GetOverlayTex(string clothesId)
        {
            if (CurrentOverlayTextures != null)
            {
                CurrentOverlayTextures.TryGetValue(clothesId, out var tex);
                return tex;
            }
            return null;
        }

        public IEnumerable<Renderer> GetApplicableRenderers(string clothesId)
        {
            var clothesCtrl = GetCustomClothesComponent(clothesId);
            if (clothesCtrl == null) return Enumerable.Empty<Renderer>();

            return GetApplicableRenderers(GetRendererArrays(clothesCtrl));
        }

        public static IEnumerable<Renderer> GetApplicableRenderers(Renderer[][] rendererArrs)
        {
            for (var i = 0; i < rendererArrs.Length; i += 2)
            {
                var renderers = rendererArrs[i];
                var renderer1 = renderers?.ElementAtOrDefault(0);
                if (renderer1 != null)
                {
                    yield return renderer1;

                    renderers = rendererArrs.ElementAtOrDefault(i + 1);
                    var renderer2 = renderers?.ElementAtOrDefault(0);
                    if (renderer2 != null)
                        yield return renderer2;

                    yield break;
                }
            }
        }

        public static Renderer[][] GetRendererArrays(ChaClothesComponent clothesCtrl)
        {
            return new[] {
                clothesCtrl.rendNormal01,
                clothesCtrl.rendNormal02,
                clothesCtrl.rendAlpha01,
#if KK
                clothesCtrl.rendAlpha02
#endif
            };
        }

        public void RefreshAllTextures()
        {
#if KK
            if (KKAPI.Studio.StudioAPI.InsideStudio)
            {
                // Studio needs a more aggresive refresh to update the textures
                // Refresh needs to happen through OCIChar or dynamic bones get messed up
                Studio.Studio.Instance.dicInfo.Values.OfType<Studio.OCIChar>()
                    .FirstOrDefault(x => x.charInfo == ChaControl)
                    ?.SetCoordinateInfo(CurrentCoordinate.Value, true);
            }
            else
#endif
            {
                for (var i = 0; i < ChaControl.cusClothesCmp.Length; i++)
                    ChaControl.ChangeCustomClothes(true, i, true, false, false, false, false);

                for (var i = 0; i < ChaControl.cusClothesSubCmp.Length; i++)
                    ChaControl.ChangeCustomClothes(false, i, true, false, false, false, false);
            }
        }

        public void RefreshTexture(string texType)
        {
            if (texType != null
#if KK
                && !KKAPI.Studio.StudioAPI.InsideStudio
#endif
                )
            {
                var i = Array.FindIndex(ChaControl.objClothes, x => x != null && x.name == texType);
                if (i >= 0)
                {
                    ChaControl.ChangeCustomClothes(true, i, true, false, false, false, false);
                    return;
                }

                i = Array.FindIndex(ChaControl.objParts, x => x != null && x.name == texType);
                if (i >= 0)
                {
                    ChaControl.ChangeCustomClothes(false, i, true, false, false, false, false);
                    return;
                }
            }

            // Fall back if the specific tex couldn't be refreshed
            RefreshAllTextures();
        }

        public void SetOverlayTex(ClothesTexData tex, string texType)
        {
            if (CurrentOverlayTextures.TryGetValue(texType, out var existing))
                existing?.Dispose();

            if (tex == null || tex.IsEmpty())
                CurrentOverlayTextures.Remove(texType);
            else
                CurrentOverlayTextures[texType] = tex;

            RefreshTexture(texType);
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            var data = new PluginData { version = 1 };

            CleanupTextureList();
            data.data.Add(OverlayDataKey, MessagePackSerializer.Serialize(_allOverlayTextures));

            SetExtendedData(data);
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            if (maintainState) return;

            var anyPrevious = _allOverlayTextures != null && _allOverlayTextures.Any();
            if (anyPrevious)
                RemoveAllOverlays();

            var pd = GetExtendedData();
            if (pd != null && pd.data.TryGetValue(OverlayDataKey, out var overlayData))
            {
                if (overlayData is byte[] overlayBytes)
                {
                    try
                    {
                        _allOverlayTextures = MessagePackSerializer.Deserialize<
                            Dictionary<CoordinateType, Dictionary<string, ClothesTexData>>>(
                            overlayBytes);
                    }
                    catch (Exception ex)
                    {
                        var logLevel = currentGameMode == GameMode.Maker ? LogLevel.Message | LogLevel.Warning : LogLevel.Warning;
                        Logger.Log(logLevel, "[KCOX] WARNING: Failed to load embedded overlay data for " + (ChaFileControl?.charaFileName ?? "?"));
                        Logger.Log(LogLevel.Debug, ex);
                    }
                }
            }

            if (_allOverlayTextures == null)
                _allOverlayTextures = new Dictionary<CoordinateType, Dictionary<string, ClothesTexData>>();

            if (anyPrevious || _allOverlayTextures.Any())
                StartCoroutine(RefreshAllTexturesCo());
        }

        protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
        {
            if (CurrentOverlayTextures == null || CurrentOverlayTextures.Count == 0) return;

            var data = new PluginData { version = 1 };
            data.data.Add(OverlayDataKey, MessagePackSerializer.Serialize(CurrentOverlayTextures));
            SetCoordinateExtendedData(coordinate, data);
        }

        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate, bool maintainState)
        {
            if (maintainState) return;

            var currentOverlayTextures = CurrentOverlayTextures;
            if (currentOverlayTextures == null) return;

            currentOverlayTextures.Clear();

            var data = GetCoordinateExtendedData(coordinate);
            if (data != null && data.data.TryGetValue(OverlayDataKey, out var bytes) && bytes is byte[] byteArr)
            {
                var dict = MessagePackSerializer.Deserialize<Dictionary<string, ClothesTexData>>(byteArr);
                if (dict != null)
                {
                    foreach (var texData in dict)
                        currentOverlayTextures.Add(texData.Key, texData.Value);
                }
            }

            StartCoroutine(RefreshAllTexturesCo());
        }

        private IEnumerator RefreshAllTexturesCo()
        {
            yield return null;
            RefreshAllTextures();
        }

        private void ApplyOverlays(ChaClothesComponent clothesCtrl)
        {
            if (CurrentOverlayTextures == null) return;

            var clothesName = clothesCtrl.name;
            var rendererArrs = GetRendererArrays(clothesCtrl);

            if (_dumpCallback != null && _dumpClothesId == clothesName)
            {
                DumpBaseTextureImpl(rendererArrs);
            }

            if (CurrentOverlayTextures.Count == 0) return;

            if (!CurrentOverlayTextures.TryGetValue(clothesName, out var overlay)) return;
            if (overlay == null || overlay.IsEmpty()) return;

            var applicableRenderers = GetApplicableRenderers(rendererArrs).ToList();
            if (applicableRenderers.Count == 0)
            {
                Logger.Log(MakerAPI.InsideMaker ? LogLevel.Warning | LogLevel.Message : LogLevel.Debug, $"[KCOX] Removing unused overlay for {clothesName}");

                overlay.Dispose();
                CurrentOverlayTextures.Remove(clothesName);
                return;
            }

            foreach (var renderer in applicableRenderers)
            {
                var mat = renderer.material;

                var mainTexture = (RenderTexture)mat.mainTexture;
                if (mainTexture == null) return;

                if (overlay.Override)
                {
                    var rta = RenderTexture.active;
                    RenderTexture.active = mainTexture;
                    GL.Clear(false, true, Color.clear);
                    RenderTexture.active = rta;
                }

                if (overlay.Texture != null)
                    KoiSkinOverlayController.ApplyOverlay(mainTexture, overlay.Texture);
            }
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

#if EC
            // Remove all overlays for clothes other than the main outfit since they can't be used in EC
            foreach (var group in _allOverlayTextures.Where(x => x.Key != CoordinateType.School01).ToList())
                _allOverlayTextures.Remove(group.Key);
#endif
        }

        private void DumpBaseTextureImpl(Renderer[][] rendererArrs)
        {
            var act = RenderTexture.active;
            try
            {
                var renderer = GetApplicableRenderers(rendererArrs).FirstOrDefault();
                var renderTexture = (RenderTexture)renderer?.material?.mainTexture;

                if (renderTexture == null)
                    throw new Exception("There are no renderers or textures to dump");

                RenderTexture.active = renderTexture;

                var tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
                tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);

                var png = tex.EncodeToPNG();

                Destroy(tex);

                _dumpCallback(png);
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Error | LogLevel.Message, "[KCOX] Dumping texture failed - " + e.Message);
                Logger.Log(LogLevel.Debug, e);
                RenderTexture.active = null;
            }
            finally
            {
                RenderTexture.active = act;
                _dumpCallback = null;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            RemoveAllOverlays();
        }

        private void RemoveAllOverlays()
        {
            if (_allOverlayTextures == null) return;

            foreach (var textures in _allOverlayTextures.SelectMany(x => x.Value))
                textures.Value.Dispose();

            _allOverlayTextures = null;
        }
    }
}
