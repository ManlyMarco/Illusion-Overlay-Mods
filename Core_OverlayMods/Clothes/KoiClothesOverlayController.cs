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
        private const string SizeOverrideDataKey = "TextureSizeOverride";
        private const string ColorMaskPrefix = "Colormask_";
        private const string PatternPrefix = "Pattern_";
        private const string OverridePrefix = "Override_";
        public const int CustomPatternID = 58947543;

        private Action<byte[]> _dumpCallback;
        private string _dumpClothesId;

#if !EC
        public bool EnableInStudio { get; set; } = true;
#endif

        private Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> _allOverlayTextures;
        internal Dictionary<string, ClothesTexData> CurrentOverlayTextures
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
        private Dictionary<CoordinateType, Dictionary<string, int>> _allTextureSizeOverrides;
        internal Dictionary<string, int> CurrentTextureSizeOverrides
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
                return GetClothingSizeOverrides(coordinateType);
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

        private Dictionary<string, int> GetClothingSizeOverrides(CoordinateType coordinateType)
        {
            if (_allTextureSizeOverrides == null) return null;

            _allTextureSizeOverrides.TryGetValue(coordinateType, out var dict);

            if (dict == null)
            {
                dict = new Dictionary<string, int>();
                _allTextureSizeOverrides.Add(coordinateType, dict);
            }

            return dict;
        }

        public void DumpBaseTexture(string clothesId, Action<byte[]> callback)
        {
            try
            {
                Texture tex = null;
                if (IsColormask(clothesId)) tex = GetOriginalColormask(clothesId);
                else if (IsPattern(clothesId)) tex = GetOriginalPattern(clothesId);
#if KK || KKS || EC
                else if (IsMaskKind(clothesId)) tex = GetOriginalMask((MaskKind)Enum.Parse(typeof(MaskKind), clothesId));
#endif
                else if (IsOverride(clothesId)) tex = GetOriginalMainTex(clothesId);
                else
                {
                    _dumpCallback = callback;
                    _dumpClothesId = clothesId;

                    // Force redraw to trigger the dump
                    RefreshTexture(clothesId);
                    return;
                }

                if (tex == null)
                    throw new Exception("There is no texture to dump");

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


        public static bool IsMaskKind(string clothesId)
        {
#if KK || KKS || EC
            return Enum.GetNames(typeof(MaskKind)).Contains(clothesId);
#else
            return false;
#endif
        }

        public static string MakeColormaskId(string clothesId)
        {
            return ColorMaskPrefix + clothesId;
        }

        public static string MakePatternId(string clothesId, int color)
        {
            return $"{PatternPrefix}{color}_{clothesId}";
        }

        public static string MakeOverrideId(string clothesId)
        {
            return OverridePrefix + clothesId;
        }

        public static bool IsColormask(string clothesId)
        {
            return clothesId.StartsWith(ColorMaskPrefix);
        }

        public static bool IsPattern(string clothesId)
        {
            return clothesId.StartsWith(PatternPrefix);
        }

        public static bool IsOverride(string clothesId)
        {
            return clothesId.StartsWith(OverridePrefix);
        }

        [Obsolete]
        public static bool GetKindIdsFromColormask(string clothesId, out int? kindId, out int? subKindId) => GetKindIdsFromClothesId(clothesId, out kindId, out subKindId);

        public static bool GetKindIdsFromClothesId(string clothesId, out int? kindId, out int? subKindId)
        {
            kindId = null;
            subKindId = null;

            switch (GetRealId(clothesId))
            {
                case "ct_top_parts_A":
                    kindId = 0;
                    subKindId = 0;
                    return true;
                case "ct_top_parts_B":
                    kindId = 0;
                    subKindId = 1;
                    return true;
                case "ct_top_parts_C":
                    kindId = 0;
                    subKindId = 2;
                    return true;
                case "ct_clothesTop":
                    kindId = 0;
                    return true;
                case "ct_clothesBot":
                    kindId = 1;
                    return true;
#if KK || KKS || EC
                case "ct_bra":
                    kindId = 2;
                    return true;
                case "ct_shorts":
                    kindId = 3;
                    return true;
#else
                case "ct_inner_t":
                    kindId = 2;
                    return true;
                case "ct_inner_b":
                    kindId = 3;
                    return true;
#endif
                case "ct_gloves":
                    kindId = 4;
                    return true;
                case "ct_panst":
                    kindId = 5;
                    return true;
                case "ct_socks":
                    kindId = 6;
                    return true;
#if KK || KKS
                case "ct_shoes_inner":
                    kindId = 7;
                    return true;
                case "ct_shoes_outer":
                    kindId = 8;
                    return true;
#else
                case "ct_shoes":
                    kindId = 7;
                    return true;
#endif
                default:
                    KoiSkinOverlayMgr.Logger.LogError("Unknown clothing type");
                    return false;
            }
        }

        public static int GetColorFromPattern(string clothesId)
        {
            if (!IsPattern(clothesId)) return -1;
            if(Int32.TryParse(clothesId.Substring(PatternPrefix.Length, 1), out var color))
                return color;
            return -1;
        }

        public static string GetRealId(string clothesId)
        {
            if (IsColormask(clothesId))
                return clothesId.Substring(ColorMaskPrefix.Length);
            else if (IsPattern(clothesId))
                return clothesId.Substring(PatternPrefix.Length + 2);
            else if (IsOverride(clothesId))
                return clothesId.Substring(OverridePrefix.Length);
            return clothesId;
        }

        public static string GetClothesIdFromKind(bool main, int kind)
        {
            if (main)
                switch (kind)
                {
                    case 0: return "ct_clothesTop";
                    case 1: return "ct_clothesBot";
#if KK || KKS || EC
                    case 2: return "ct_bra";
                    case 3: return "ct_shorts";
#else
                    case 2: return "ct_inner_t";
                    case 3: return "ct_inner_b";
#endif
                    case 4: return "ct_gloves";
                    case 5: return "ct_panst";
                    case 6: return "ct_socks";
#if KK || KKS
                    case 7: return "ct_shoes_inner";
                    case 8: return "ct_shoes_outer";
#else
                    case 7: return "ct_shoes";
#endif
                }
            else
                switch (kind)
                {
                    case 0: return "ct_top_parts_A";
                    case 1: return "ct_top_parts_B";
                    case 2: return "ct_top_parts_C";
                }
            return null;
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
        internal Texture GetOriginalColormask(string clothesId)
        {
            return Hooks.GetColormask(this, clothesId);
        }

        internal Texture GetOriginalPattern(string clothesId)
        {
            return Hooks.GetPattern(this, clothesId);
        }

        internal Texture GetOriginalMainTex(string clothesId)
        {
            return Hooks.GetMainTex(this, clothesId);
        }

        public ClothesTexData GetOverlayTex(string clothesId, bool createNew)
        {
            // TODO allow setting of different blend modes
            if (CurrentOverlayTextures != null)
            {
                CurrentOverlayTextures.TryGetValue(clothesId, out var tex);
                if (tex == null && createNew)
                {
                    tex = new ClothesTexData();
                    tex.BlendingMode = OverlayBlendingMode.LinearAlpha;
                    CurrentOverlayTextures[clothesId] = tex;
                }
                return tex;
            }
            return null;
        }

        public int GetTextureSizeOverride(string clothesId)
        {
            if (CurrentTextureSizeOverrides != null && CurrentTextureSizeOverrides.TryGetValue(clothesId, out var size))
                return size;
            return 0;
        }
        public int SetTextureSizeOverride(string clothesId, int newSize)
        {
            if (CurrentTextureSizeOverrides != null)
            {
                if (newSize > 0)
                {
                    CurrentTextureSizeOverrides[clothesId] = newSize;
                    return newSize;
                }
                else if (CurrentTextureSizeOverrides.ContainsKey(clothesId))
                    CurrentTextureSizeOverrides.Remove(clothesId);
            }
            return 0;
        }

        public static Sprite GetPatternThumbnail()
        {
            var tex = new Texture2D(1, 1);
            tex.LoadImage(ResourceUtils.GetEmbeddedResource("OverlayPatternThumbnail.png"));
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        public static Texture2D GetPatternPlaceholder()
        {
            var tex = new Texture2D(1, 1);
            tex.LoadImage(ResourceUtils.GetEmbeddedResource("OverlayPatternPlaceholder.png"));
            return tex;
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
            var isColormask = IsColormask(texType);
            var color = GetColorFromPattern(texType);
            texType = GetRealId(texType);
            if (IsMaskKind(texType))
            {
                RefreshAllTextures(true);
                return;
            }

            if (texType != null)
            {
                var i = Array.FindIndex(ChaControl.objClothes, x => x != null && x.name == texType);
                if (i >= 0)
                {
                    // Needed in studio to trigger anything at all
                    // Needed for color masks to reinitialize them 
                    if (Util.InsideStudio() || isColormask || color >= 0)
                        ChaControl.InitBaseCustomTextureClothes(true, i);

                    ChaControl.ChangeCustomClothes(
                        true,
                        i,
                        true,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[0].pattern > 0 || color == 0,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[1].pattern > 0 || color == 1,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[2].pattern > 0 || color == 2,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[3].pattern > 0 || color == 3
                    );
                    return;
                }

                i = Array.FindIndex(ChaControl.objParts, x => x != null && x.name == texType);
                if (i >= 0)
                {
                    if (Util.InsideStudio() || isColormask || color >= 0)
                        ChaControl.InitBaseCustomTextureClothes(false, i);

                    ChaControl.ChangeCustomClothes(
                        false,
                        i,
                        true,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[0].pattern > 0 || color == 0,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[1].pattern > 0 || color == 1,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[2].pattern > 0 || color == 2,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[3].pattern > 0 || color == 3
                    );
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
            var isColormask = IsColormask(texType);
            texType = GetRealId(texType);
            if (texType != null && KoikatuAPI.GetCurrentGameMode() != GameMode.Studio)
            {
                var i = Array.FindIndex(ChaControl.objClothes, x => x != null && x.name == texType);
                if (i >= 0)
                {
                    if (isColormask)
                        ChaControl.InitBaseCustomTextureClothes(i);
                    ChaControl.ChangeCustomClothes(
                        i,
                        true,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[0].pattern > 0,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[1].pattern > 0,
                        ChaControl.nowCoordinate.clothes.parts[i].colorInfo[2].pattern > 0
                    );
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
            var data = new PluginData { version = 2 };

            CleanupTextureList();

            SetOverlayExtData(_allOverlayTextures, data);
            SetTextureSizeOverrideExtData(_allTextureSizeOverrides, data);

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
                _allTextureSizeOverrides = ReadTextureSizeOverrideExtData(pd);
#if !EC
                EnableInStudio = !pd.data.TryGetValue(nameof(EnableInStudio), out var val1) || !(val1 is bool) || (bool)val1;
#endif
            }

            if (_allOverlayTextures == null)
                _allOverlayTextures = new Dictionary<CoordinateType, Dictionary<string, ClothesTexData>>();
            if (_allTextureSizeOverrides == null)
                _allTextureSizeOverrides = new Dictionary<CoordinateType, Dictionary<string, int>>();

            if (anyPrevious || _allOverlayTextures.Any() || _allTextureSizeOverrides.Any())
                StartCoroutine(RefreshAllTexturesCo());
        }

        private static void SetOverlayExtData(Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> allOverlayTextures, PluginData data)
        {
            if (allOverlayTextures.Count > 0)
                data.data[OverlayDataKey] = MessagePackSerializer.Serialize(allOverlayTextures);
            else
                data.data.Remove(OverlayDataKey);
        }
        private static void SetTextureSizeOverrideExtData(Dictionary<CoordinateType, Dictionary<string, int>> allTextureSizeOverrides, PluginData data)
        {
            if (allTextureSizeOverrides.Count > 0)
                data.data[SizeOverrideDataKey] = MessagePackSerializer.Serialize(allTextureSizeOverrides);
            else
                data.data.Remove(SizeOverrideDataKey);
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

        private static Dictionary<CoordinateType, Dictionary<string, int>> ReadTextureSizeOverrideExtData(PluginData pd)
        {
            if (pd.data.TryGetValue(SizeOverrideDataKey, out var sizeOverrideData))
            {
                if (sizeOverrideData is byte[] bytes)
                    return ReadTextureSizeOverrideExtData(bytes);
            }

            return null;
        }
        private static Dictionary<CoordinateType, Dictionary<string, int>> ReadTextureSizeOverrideExtData(byte[] bytes)
        {
            try
            {
                return MessagePackSerializer.Deserialize<Dictionary<CoordinateType, Dictionary<string, int>>>(bytes);
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
            data = new PluginData { version = 2 };
            if (CurrentOverlayTextures != null && CurrentOverlayTextures.Count != 0)
                data.data.Add(OverlayDataKey, MessagePackSerializer.Serialize(CurrentOverlayTextures));
            if (CurrentTextureSizeOverrides != null &&  CurrentTextureSizeOverrides.Count != 0)
                data.data.Add(SizeOverrideDataKey, MessagePackSerializer.Serialize(CurrentTextureSizeOverrides));

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
            if (data != null && data.data.TryGetValue(SizeOverrideDataKey, out bytes) && bytes is byte[] byteArr2)
            {
                var dict = MessagePackSerializer.Deserialize<Dictionary<string, int>>(byteArr2);
                if (dict != null)
                {
                    foreach (var overrideData in dict)
                        CurrentTextureSizeOverrides.Add(overrideData.Key, overrideData.Value);
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
                CurrentOverlayTextures.Remove(MakeColormaskId(clothesName));
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
                    KoiSkinOverlayController.ApplyOverlay(mainTexture, overlay.Texture, overlay.Override, overlay.BlendingMode);
            }
        }

        private void CleanupTextureList()
        {
#if KK || KKS
            CleanupTextureList(_allOverlayTextures, ChaControl.chaFile.coordinate.Length);
            CleanupTextureList(_allTextureSizeOverrides, ChaControl.chaFile.coordinate.Length);
#else
            CleanupTextureList(_allOverlayTextures);
            CleanupTextureList(_allTextureSizeOverrides);
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

        private static void CleanupTextureList(Dictionary<CoordinateType, Dictionary<string, int>> allOverlayTextures, int coordinateCount = 999)
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
                        if (texture.Value == 0)
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
                var texture = renderer?.material?.mainTexture;

                if (texture == null)
                    throw new Exception("There are no renderers or textures to dump");

                // Fix being unable to save some texture formats with EncodeToPNG
                var tex = texture.ToTexture2D();
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
