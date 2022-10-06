using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KoiSkinOverlayX;
using MessagePack;
using UnityEngine;
using ExtensibleSaveFormat;
using KKAPI.Utilities;
#if KK || KKS
using CoordinateType = ChaFileDefine.CoordinateType;
using KKAPI.Studio;
using Studio;
#elif EC
using CoordinateType = KoikatsuCharaFile.ChaFileDefine.CoordinateType;
#elif AI || HS2
using AIChara;
using KKAPI.Studio;
using Studio;
#endif

namespace KoiClothesOverlayX
{
#if AI || HS2
    public enum CoordinateType
    {
        Unknown = 0
    }
#endif

    public partial class KoiClothesOverlayController : CharaCustomFunctionController
    {
        private const string OverlayDataKey = "Overlays";

        private Action<byte[]> _dumpCallback;
        private string _dumpClothesId;

#if !EC
        public bool EnableInStudio { get; set; } = true;
#endif

        private Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> _allOverlayTextures;
        private Dictionary<string, ClothesTexData> CurrentOverlayTextures
        {
            get
            {
#if KK || KKS
                // Need to do this instead of polling the CurrentCoordinate prop because it's updated too late
                var coordinateType = (CoordinateType)ChaControl.fileStatus.coordinateType;
#elif EC
                var coordinateType = CoordinateType.School01;
#else
                var coordinateType = CoordinateType.Unknown;
#endif
                return GetOverlayTextures(coordinateType);
            }
        }

        private Dictionary<string, ClothesTexData> GetOverlayTextures(CoordinateType coordinateType)
        {
            if (_allOverlayTextures == null) return null;

            _allOverlayTextures.TryGetValue(coordinateType, out var dict);

            if (dict == null)
            {
                dict = new Dictionary<string, ClothesTexData>();
                _allOverlayTextures.Add(coordinateType, dict);
            }

            return dict;
        }

        public void DumpBaseTexture(string clothesId, Action<byte[]> callback)
        {
#if KK || KKS || EC
            if (IsMaskKind(clothesId))
            {
                try
                {
                    var tex = GetOriginalMask((MaskKind)Enum.Parse(typeof(MaskKind), clothesId));

                    if (tex == null)
                        throw new Exception("There is no texture to dump");

                    // Fix being unable to save some texture formats with EncodeToPNG
                    var t = tex.ToTexture2D();
                    var bytes = t.EncodeToPNG();
                    Destroy(t);
                    callback(bytes);
                }
                catch (Exception e)
                {
                    KoiSkinOverlayMgr.Logger.LogMessage("Dumping texture failed - " + e.Message);
                    KoiSkinOverlayMgr.Logger.LogDebug(e);
                }
            }
            else
#endif
            {
                _dumpCallback = callback;
                _dumpClothesId = clothesId;

                // Force redraw to trigger the dump
                RefreshTexture(clothesId);
            }
        }


        public static bool IsMaskKind(string clothesId)
        {
#if KK || KKS || EC
            return Enum.GetNames(typeof(MaskKind)).Contains(clothesId);
#else
            return false;
#endif
        }
#if KK || KKS || EC
        public ChaClothesComponent GetCustomClothesComponent(string clothesObjectName)
        {
            return ChaControl.cusClothesCmp.Concat(ChaControl.cusClothesSubCmp).FirstOrDefault(x => x != null && x.gameObject.name == clothesObjectName);
        }

        internal Texture GetOriginalMask(MaskKind kind)
        {
            return Hooks.GetMask(this, kind);
        }
#else
        public CmpClothes GetCustomClothesComponent(string clothesObjectName)
        {
            return ChaControl.cmpClothes.FirstOrDefault(x => x != null && x.gameObject.name == clothesObjectName);
        }
#endif

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
#if KK || KKS || EC
            if (IsMaskKind(clothesId))
            {
                var toCheck = KoiClothesOverlayMgr.SubClothesNames.Concat(new[] { KoiClothesOverlayMgr.MainClothesNames[0] });

                return toCheck
                    .Select(GetCustomClothesComponent)
                    .Where(x => x != null)
                    .Select(GetRendererArrays)
                    .SelectMany(GetApplicableRenderers)
                    .Distinct();
            }
#endif

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

#if KK || KKS || EC
        public static Renderer[][] GetRendererArrays(ChaClothesComponent clothesCtrl)
        {
            return new[] {
                clothesCtrl.rendNormal01,
                clothesCtrl.rendNormal02,
                clothesCtrl.rendAlpha01,
#if KK || KKS
                clothesCtrl.rendAlpha02
#endif
            };
        }
#else
        public static Renderer[][] GetRendererArrays(CmpClothes clothesCtrl)
        {
            return new[] {
                clothesCtrl.rendNormal01,
                clothesCtrl.rendNormal02,
                clothesCtrl.rendNormal03,
            };
        }
#endif

#if KK || KKS || EC
        public void RefreshAllTextures()
        {
            RefreshAllTextures(false);
        }

        public void RefreshAllTextures(bool onlyMasks)
        {
#if KK || KKS
            if (KKAPI.Studio.StudioAPI.InsideStudio)
            {
                // Studio needs a more aggresive refresh to update the textures
                // Refresh needs to happen through OCIChar or dynamic bones get messed up
                Studio.Studio.Instance.dicInfo.Values.OfType<Studio.OCIChar>()
                    .FirstOrDefault(x => x.charInfo == ChaControl)
                    ?.SetCoordinateInfo(CurrentCoordinate.Value, true);
            }
            else
            {
                // Needed for body masks
                var forceNeededParts = new[] { ChaFileDefine.ClothesKind.top, ChaFileDefine.ClothesKind.bra };
                foreach (var clothesKind in forceNeededParts)
                    ForceClothesReload(clothesKind);

                if (onlyMasks) return;

                var allParts = Enum.GetValues(typeof(ChaFileDefine.ClothesKind)).Cast<ChaFileDefine.ClothesKind>();
                foreach (var clothesKind in allParts.Except(forceNeededParts))
                    ChaControl.ChangeCustomClothes(true, (int)clothesKind, true, false, false, false, false);

                // Triggered by ForceClothesReload on top so not necessary
                //for (var i = 0; i < ChaControl.cusClothesSubCmp.Length; i++)
                //    ChaControl.ChangeCustomClothes(false, i, true, false, false, false, false);
            }
#elif EC
            if (MakerAPI.InsideMaker && onlyMasks)
            {
                // Need to do the more aggresive version in maker to allow for clearing the mask without a character reload
                var forceNeededParts = new[] { ChaFileDefine.ClothesKind.top, ChaFileDefine.ClothesKind.bra };
                foreach (var clothesKind in forceNeededParts)
                    ForceClothesReload(clothesKind);
            }
            else
            {
                // Need to manually set the textures because calling ChangeClothesAsync (through ForceClothesReload)
                // to make the game do it results in a crash when editing nodes in a scene
                if (ChaControl.customMatBody)
                {
                    Texture overlayTex = GetOverlayTex(MaskKind.BodyMask.ToString(), false)?.Texture;
                    if (overlayTex != null)
                        ChaControl.customMatBody.SetTexture(ChaShader._AlphaMask, overlayTex);
                }
                if (ChaControl.rendBra != null)
                {
                    Texture overlayTex = GetOverlayTex(MaskKind.BraMask.ToString(), false)?.Texture;
                    if (overlayTex != null)
                    {
                        if (ChaControl.rendBra[0]) ChaControl.rendBra[0].material.SetTexture(ChaShader._AlphaMask, overlayTex);
                        if (ChaControl.rendBra[1]) ChaControl.rendBra[1].material.SetTexture(ChaShader._AlphaMask, overlayTex);
                    }
                }
                if (ChaControl.rendInner != null)
                {
                    Texture overlayTex = GetOverlayTex(MaskKind.InnerMask.ToString(), false)?.Texture;
                    if (overlayTex != null)
                    {
                        if (ChaControl.rendInner[0]) ChaControl.rendInner[0].material.SetTexture(ChaShader._AlphaMask, overlayTex);
                        if (ChaControl.rendInner[1]) ChaControl.rendInner[1].material.SetTexture(ChaShader._AlphaMask, overlayTex);
                    }
                }
            }

            if (onlyMasks) return;

            var allParts = Enum.GetValues(typeof(ChaFileDefine.ClothesKind)).Cast<ChaFileDefine.ClothesKind>();
            foreach (var clothesKind in allParts)
                ChaControl.ChangeCustomClothes(true, (int)clothesKind, true, false, false, false, false);

            for (var i = 0; i < ChaControl.cusClothesSubCmp.Length; i++)
                ChaControl.ChangeCustomClothes(false, i, true, false, false, false, false);
#endif
        }

        private void ForceClothesReload(ChaFileDefine.ClothesKind kind)
        {
            if (ChaControl.rendBody == null) return;

            var num = (int)kind;
            ChaControl.StartCoroutine(
                ChaControl.ChangeClothesAsync(
                    num,
                    ChaControl.nowCoordinate.clothes.parts[num].id,
                    ChaControl.nowCoordinate.clothes.subPartsId[0],
                    ChaControl.nowCoordinate.clothes.subPartsId[1],
                    ChaControl.nowCoordinate.clothes.subPartsId[2],
                    true,
                    false
                ));
        }

        public void RefreshTexture(string texType)
        {
            if (IsMaskKind(texType))
            {
                RefreshAllTextures(true);
                return;
            }

            if (texType != null && !Util.InsideStudio())
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
#else
        public void RefreshAllTextures()
        {
            ChaControl.ChangeClothes(true);

            if (StudioAPI.InsideStudio) FixSkirtFk();
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

        private void FixSkirtFk()
        {
            var ocic = ChaControl.GetOCIChar();
            //ocic.female.UpdateBustSoftnessAndGravity();
            var active = ocic.oiCharInfo.activeFK[6];
            ocic.ActiveFK(OIBoneInfo.BoneGroup.Skirt, false, ocic.oiCharInfo.enableFK);
            ocic.fkCtrl.ResetUsedBone(ocic);
            ocic.skirtDynamic = AddObjectFemale.GetSkirtDynamic(ocic.charInfo.objClothes);
            ocic.ActiveFK(OIBoneInfo.BoneGroup.Skirt, active, ocic.oiCharInfo.enableFK);
        }
#endif

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            var data = new PluginData { version = 1 };

            CleanupTextureList();

            SetOverlayExtData(_allOverlayTextures, data);

#if !EC
            if (!EnableInStudio) data.data[nameof(EnableInStudio)] = EnableInStudio;
#endif

            SetExtendedData(data.data.Count > 0 ? data : null);
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            if (maintainState) return;

            var anyPrevious = _allOverlayTextures != null && _allOverlayTextures.Any();
            if (anyPrevious)
                RemoveAllOverlays();

#if !EC
            EnableInStudio = true;
#endif

            var pd = GetExtendedData();
            if (pd != null)
            {
                _allOverlayTextures = ReadOverlayExtData(pd);
#if !EC
                EnableInStudio = !pd.data.TryGetValue(nameof(EnableInStudio), out var val1) || !(val1 is bool) || (bool)val1;
#endif
            }

            if (_allOverlayTextures == null)
                _allOverlayTextures = new Dictionary<CoordinateType, Dictionary<string, ClothesTexData>>();

            if (anyPrevious || _allOverlayTextures.Any())
                StartCoroutine(RefreshAllTexturesCo());
        }

        private static void SetOverlayExtData(Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> allOverlayTextures, PluginData data)
        {
            if (allOverlayTextures.Count > 0)
                data.data[OverlayDataKey] = MessagePackSerializer.Serialize(allOverlayTextures);
            else
                data.data.Remove(OverlayDataKey);
        }

        private static Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> ReadOverlayExtData(PluginData pd)
        {
            if (pd.data.TryGetValue(OverlayDataKey, out var overlayData))
            {
                if (overlayData is byte[] overlayBytes)
                    return ReadOverlayExtData(overlayBytes);
            }

            return null;
        }

        private static Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> ReadOverlayExtData(byte[] overlayBytes)
        {
            try
            {
                return MessagePackSerializer.Deserialize<
                    Dictionary<CoordinateType, Dictionary<string, ClothesTexData>>>(
                    overlayBytes);
            }
            catch (Exception ex)
            {
                if (MakerAPI.InsideMaker)
                    KoiSkinOverlayMgr.Logger.LogMessage("WARNING: Failed to load clothes overlay data");
                else
                    KoiSkinOverlayMgr.Logger.LogDebug("WARNING: Failed to load clothes overlay data");
                KoiSkinOverlayMgr.Logger.LogError(ex);

                return null;
            }
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

#if KK || KKS || EC
        private void ApplyOverlays(ChaClothesComponent clothesCtrl)
#else
        private void ApplyOverlays(CmpClothes clothesCtrl)
#endif
        {
            if (CurrentOverlayTextures == null) return;

            var clothesName = clothesCtrl.name;
            var rendererArrs = GetRendererArrays(clothesCtrl);

            if (_dumpCallback != null && _dumpClothesId == clothesName)
            {
                DumpBaseTextureImpl(rendererArrs);
            }

            if (CurrentOverlayTextures.Count == 0) return;

#if !EC
            if (KKAPI.Studio.StudioAPI.InsideStudio && !EnableInStudio) return;
#endif

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
#if KK || KKS
            CleanupTextureList(_allOverlayTextures, ChaControl.chaFile.coordinate.Length);
#else
            CleanupTextureList(_allOverlayTextures);
#endif
        }

        private static void CleanupTextureList(Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> allOverlayTextures, int coordinateCount = 999)
        {
            if (allOverlayTextures == null) return;

            foreach (var group in allOverlayTextures.ToList())
            {
#if KK || KKS
                // Handle coords being added and removed
                if ((int)group.Key >= coordinateCount)
                {
                    allOverlayTextures.Remove(group.Key);
                }
                else
#endif
                {
                    foreach (var texture in group.Value.ToList())
                    {
                        if (texture.Value.IsEmpty()) 
                            group.Value.Remove(texture.Key);
                    }

                    if (group.Value.Count == 0)
                        allOverlayTextures.Remove(group.Key);
                }
            }
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

            if (_allOverlayTextures == null) return;

            foreach (var textures in _allOverlayTextures.SelectMany(x => x.Value))
                textures.Value?.Dispose();
        }

        private void RemoveAllOverlays()
        {
            if (_allOverlayTextures == null) return;

            foreach (var textures in _allOverlayTextures.SelectMany(x => x.Value))
                textures.Value?.Dispose();

            _allOverlayTextures = null;
        }
    }
}
