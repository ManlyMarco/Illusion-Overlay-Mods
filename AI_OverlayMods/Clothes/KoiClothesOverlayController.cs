using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AIChara;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KoiSkinOverlayX;
using MessagePack;
using UnityEngine;
using ExtensibleSaveFormat;

namespace KoiClothesOverlayX
{
    public enum CoordinateType
    {
        Unknown = 0
    }

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

                // todo 
                // Need to do this instead of polling the CurrentCoordinate prop because it's updated too late
                // var coordinateType = (CoordinateType)ChaControl.fileStatus.coordinateType;
                var coordinateType = CoordinateType.Unknown;

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

        public CmpClothes GetCustomClothesComponent(string clothesObjectName)
        {
            return ChaControl.cmpClothes.FirstOrDefault(x => x != null && x.gameObject.name == clothesObjectName);
        }

        public ClothesTexData GetOverlayTex(string clothesId, bool createNew)
        {
            if (CurrentOverlayTextures != null)
            {
                CurrentOverlayTextures.TryGetValue(clothesId, out var tex);
                if (tex == null && createNew)
                {
                    tex = new ClothesTexData();
                    CurrentOverlayTextures[clothesId] = tex;
                }
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

        public static Renderer[][] GetRendererArrays(CmpClothes clothesCtrl)
        {
            return new[] {
                clothesCtrl.rendNormal01,
                clothesCtrl.rendNormal02,
                clothesCtrl.rendNormal03,
            };
        }

        public void RefreshAllTextures()
        {
            ChaControl.ChangeClothes(true);
        }

        public void RefreshTexture(string texType)
        {
            if (texType != null && KoikatuAPI.GetCurrentGameMode() != GameMode.Studio)
            {
                var i = Array.FindIndex(ChaControl.objClothes, x => x != null && x.name == texType);
                if (i >= 0)
                {
                    ChaControl.ChangeCustomClothes(i, true, false, false, false);
                    return;
                }
            }

            // Fall back if the specific tex couldn't be refreshed
            RefreshAllTextures();
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
                        if (MakerAPI.InsideMaker)
                            KoiSkinOverlayMgr.Logger.LogMessage("WARNING: Failed to load embedded overlay data for " + (ChaFileControl?.charaFileName ?? "?"));
                        else
                            KoiSkinOverlayMgr.Logger.LogDebug("WARNING: Failed to load embedded overlay data for " + (ChaFileControl?.charaFileName ?? "?"));
                        KoiSkinOverlayMgr.Logger.LogError(ex);
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
            PluginData data = null;

            CleanupTextureList();
            if (CurrentOverlayTextures != null && CurrentOverlayTextures.Count != 0)
            {
                data = new PluginData { version = 1 };
                data.data.Add(OverlayDataKey, MessagePackSerializer.Serialize(CurrentOverlayTextures));
            }

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

        private void ApplyOverlays(CmpClothes clothesCtrl)
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
                if (MakerAPI.InsideMaker)
                    KoiSkinOverlayMgr.Logger.LogMessage($"Removing unused overlay for {clothesName}");
                else
                    KoiSkinOverlayMgr.Logger.LogDebug($"Removing unused overlay for {clothesName}");

                overlay.Dispose();
                CurrentOverlayTextures.Remove(clothesName);
                return;
            }

            foreach (var renderer in applicableRenderers)
            {
                var mat = renderer.material;

                var mainTexture = mat.mainTexture as RenderTexture;
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
            if (_allOverlayTextures == null) return;

            foreach (var group in _allOverlayTextures.Values)
            {
                foreach (var texture in group.Where(x => x.Value.IsEmpty()).ToList())
                    group.Remove(texture.Key);
            }

            foreach (var group in _allOverlayTextures.Where(x => !x.Value.Any()).ToList())
                _allOverlayTextures.Remove(group.Key);
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
                KoiSkinOverlayMgr.Logger.LogMessage("Dumping texture failed - " + e.Message);
                KoiSkinOverlayMgr.Logger.LogError(e);
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

            foreach (var textures in _allOverlayTextures.SelectMany(x => x.Value))
                textures.Value.Dispose();
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
