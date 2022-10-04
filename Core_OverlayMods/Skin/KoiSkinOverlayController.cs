using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using UniRx;
using UnityEngine;
#if AI || HS2
using AIChara;
#endif
namespace KoiSkinOverlayX
{
    public class KoiSkinOverlayController : CharaCustomFunctionController
    {
        /// <summary>
        /// Additional overlays to be applied over the KSOX overlay (if any).
        /// Drawn bottom to top based on the <exception cref="AdditionalTexture.ApplyOrder"></exception> property.
        /// Use <code>UpdateTexture</code> to apply any changes done here.
        /// </summary>
        public List<AdditionalTexture> AdditionalTextures { get; } = new List<AdditionalTexture>();

        public OverlayStorage OverlayStorage { get; private set; }

#if !EC
        public bool EnableInStudioSkin { get; set; } = true;
        public bool EnableInStudioIris { get; set; } = true;
#endif

        protected override void Awake()
        {
            base.Awake();
            OverlayStorage = new OverlayStorage(this);
#if KK || KKS
            // this causes massive lag on overworld since charas change coords in background so avoid running this whenever possible
            CurrentCoordinate.Subscribe(v =>
            {
                if (OverlayStorage.IsPerCoord())
                    UpdateTexture(WhatTexturesNeedUpdating());
            });
#endif
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            var pd = new PluginData { version = 2 };

            OverlayStorage.Save(pd);

#if !EC
            if (!EnableInStudioSkin) pd.data[nameof(EnableInStudioSkin)] = EnableInStudioSkin;
            if (!EnableInStudioIris) pd.data[nameof(EnableInStudioIris)] = EnableInStudioIris;
#endif

            SetExtendedData(pd.data.Count > 0 ? pd : null);

#if KK || KKS
            ExtendedSave.SetExtendedDataById(ChaFileControl, "com.jim60105.kk.charaoverlaysbasedoncoordinate", null);
#endif
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            if (maintainState) return;

            var needsUpdate = OverlayStorage.GetCount() > 0;
            RemoveAllOverlays(false);

#if !EC
            EnableInStudioSkin = true;
            EnableInStudioIris = true;
#endif

            var data = GetExtendedData();
            if (data != null)
            {
                if (data.version <= 1)
                {
                    ReadLegacyData(data);
                }
                else
                {
                    OverlayStorage.Load(data);
#if !EC
                    EnableInStudioSkin = !data.data.TryGetValue(nameof(EnableInStudioSkin), out var val1) || !(val1 is bool) || (bool)val1;
                    EnableInStudioIris = !data.data.TryGetValue(nameof(EnableInStudioIris), out var val2) || !(val2 is bool) || (bool)val2;
#endif
                }
            }
            else
            {
                TryImportCOBOC();
            }

            if (needsUpdate || OverlayStorage.GetCount() > 0)
                UpdateTexture(TexType.Unknown);
        }

        private void ReadLegacyData(PluginData data)
        {
            if (TryImportCOBOC()) return;

            KoiSkinOverlayMgr.Logger.LogInfo("Reading legacy overlay data");
            foreach (TexType texType in Enum.GetValues(typeof(TexType)))
            {
                if (texType == TexType.Unknown) continue;

                if (data != null
                    && data.data.TryGetValue(texType.ToString(), out var texData)
                    && texData is byte[] bytes && bytes.Length > 0)
                {
                    if (texType == TexType.EyeOver)
                    {
                        OverlayStorage.SetTexture(TexType.EyeOverL, bytes);
                        OverlayStorage.SetTexture(TexType.EyeOverR, bytes);
                    }
                    else if (texType == TexType.EyeUnder)
                    {
                        OverlayStorage.SetTexture(TexType.EyeUnderL, bytes);
                        OverlayStorage.SetTexture(TexType.EyeUnderR, bytes);
                    }
                    else
                    {
                        OverlayStorage.SetTexture(texType, bytes);
                    }
                }
            }
#if KK || KKS
            OverlayStorage.CopyToOtherCoords();
#endif
        }

        /// <summary>
        /// Attempt to import old KK_CharaOverlaysBasedOnCoordinate data.
        /// Based on code from https://github.com/jim60105/KK/blob/99dd9a055679cea8bf2c7d85a357ca53c4233636/KK_CharaOverlaysBasedOnCoordinate/KK_CharaOverlaysBasedOnCoordinate.cs
        /// </summary>
        private bool TryImportCOBOC()
        {
#if KK || KKS
            var data = ExtendedSave.GetExtendedDataById(ChaFileControl, "com.jim60105.kk.charaoverlaysbasedoncoordinate");
            if (data == null) return false;

            KoiSkinOverlayMgr.Logger.LogInfo("[Import] Trying to import KK_CharaOverlaysBasedOnCoordinate data.");

            Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(object self)
            {
                if (!(self is IDictionary dictionary))
                {
                    KoiSkinOverlayMgr.Logger.LogWarning($"[Import] Failed to cast to Dictionary! Likely invalid data.");
                    return null;
                }

                return CastDict(dictionary).ToDictionary(entry => (TKey)entry.Key, entry => (TValue)entry.Value);

                IEnumerable<DictionaryEntry> CastDict(IDictionary dic)
                {
                    foreach (DictionaryEntry entry in dic)
                        yield return entry;
                }
            }

            if ((!data.data.TryGetValue("AllCharaOverlayTable", out var tmpOverlayTable) || tmpOverlayTable == null) ||
                (!data.data.TryGetValue("AllCharaResources", out var tmpResources) || null == tmpResources))
            {
                KoiSkinOverlayMgr.Logger.LogWarning("[Import] Wrong PluginData version, can't import.");
            }
            else
            {
                var overlays = new Dictionary<ChaFileDefine.CoordinateType, Dictionary<TexType, byte[]>>();
                var resourceList = ToDictionary<int, byte[]>(tmpResources).Select(x => x.Value).ToList();
                Dictionary<TexType, byte[]> firstCoord = null;
                foreach (var kvp in ToDictionary<ChaFileDefine.CoordinateType, object>(tmpOverlayTable))
                {
                    var coordinate = new Dictionary<TexType, byte[]>();
                    foreach (var kvp2 in ToDictionary<TexType, int>(kvp.Value))
                    {
                        coordinate.Add(kvp2.Key, resourceList[kvp2.Value]);
                        if (kvp2.Value != 0) KoiSkinOverlayMgr.Logger.LogDebug($"[Import] Add overlay ->{kvp.Key}: {kvp2.Key}, {kvp2.Value}");
                    }

                    if (firstCoord == null)
                    {
                        firstCoord = coordinate;
                    }
                    else
                    {
                        foreach (var missing in firstCoord.Where(x => x.Value != null && (!coordinate.TryGetValue(x.Key, out var val) || val == null)).ToList())
                        {
                            coordinate[missing.Key] = missing.Value;
                            KoiSkinOverlayMgr.Logger.LogDebug($"[Import] Fill in missing overlay ->{kvp.Key}: {missing.Key}");
                        }
                    }

                    overlays.Add(kvp.Key, coordinate);
                }

                OverlayStorage.Load(overlays);

                KoiSkinOverlayMgr.Logger.LogInfo("[Import] Imported KK_CharaOverlaysBasedOnCoordinate data successfully. Save the card to migrate it to new data format.");

                return true;
            }
#endif
            return false;
        }

        public void ApplyOverlayToRT(RenderTexture bodyTexture, TexType overlayType)
        {
            foreach (var overlayTexture in GetOverlayTextures(overlayType))
                ApplyOverlay(bodyTexture, overlayTexture);
        }

        internal IEnumerable<Texture2D> GetOverlayTextures(TexType overlayType)
        {
            if (IsShown(overlayType))
            {
                var tex = OverlayStorage.GetTexture(overlayType);
                if (tex) yield return tex;
            }

            foreach (var additionalTexture in AdditionalTextures.Where(x => x.OverlayType == overlayType && x.Texture != null).OrderBy(x => x.ApplyOrder))
                yield return additionalTexture.Texture;
        }

        private bool IsShown(TexType overlayType)
        {
#if !EC
            if (!KKAPI.Studio.StudioAPI.InsideStudio) return true;
            return EnableInStudioSkin && overlayType <= TexType.FaceUnder ||
                   EnableInStudioIris && overlayType > TexType.FaceUnder;
#else
            return true;
#endif
        }

        internal static void ApplyOverlays(RenderTexture targetTexture, IEnumerable<Texture2D> overlays)
        {
            foreach (var overlay in overlays)
                ApplyOverlay(targetTexture, overlay);
        }

        public Texture2D SetOverlayTex(byte[] overlayTex, TexType overlayType)
        {
            if (overlayType == TexType.EyeOver || overlayType == TexType.EyeUnder)
            {
                SetOverlayTex(overlayTex, overlayType + 2);
                return SetOverlayTex(overlayTex, overlayType + 4);
            }

            OverlayStorage.SetTexture(overlayType, overlayTex);

            UpdateTexture(overlayType);

            return OverlayStorage.GetTexture(overlayType);
        }

        public void UpdateTexture(TexType type)
        {
            UpdateTexture(ChaControl, type);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            RemoveAllOverlays(true);
        }

        private void RemoveAllOverlays(bool removeAdditional)
        {
            OverlayStorage.Clear();

            if (removeAdditional)
                AdditionalTextures.Clear();
        }

        public static void UpdateTexture(ChaControl cc, TexType type)
        {
            if (cc == null) return;
            if (cc.customTexCtrlBody == null || cc.customTexCtrlFace == null) return;

            // Prevent lag when reloading textures at the cost of extra memory usage for the no longer used textures (until something else collects garbage)
            var prevGcClear = Util.EnableCharaLoadGC;
            Util.EnableCharaLoadGC = false;

#if KK || KKS || EC
            switch (type)
            {
                case TexType.BodyOver:
                case TexType.BodyUnder:
                    cc.AddUpdateCMBodyTexFlags(true, true, true, true, true);
                    cc.CreateBodyTexture();
                    break;
                case TexType.FaceOver:
                case TexType.FaceUnder:
                    cc.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    cc.CreateFaceTexture();
                    break;
                case TexType.EyeUnder:
                case TexType.EyeOver:
                case TexType.EyeUnderL:
                case TexType.EyeOverL:
                case TexType.EyeUnderR:
                case TexType.EyeOverR:
                    cc.ChangeSettingEye(true, true, true);
                    break;
                case TexType.EyebrowUnder:
                    cc.ChangeSettingEyebrow();
                    break;
                case TexType.EyelineUnder:
                    cc.ChangeSettingEyelineUp();
                    break;
                default:
                    cc.AddUpdateCMBodyTexFlags(true, true, true, true, true);
                    cc.CreateBodyTexture();
                    cc.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    cc.CreateFaceTexture();
                    cc.ChangeSettingEye(true, true, true);
                    cc.ChangeSettingEyebrow();
                    cc.ChangeSettingEyelineUp();
                    //cc.ChangeSettingEyelineDown();
                    break;
            }
#elif AI || HS2
            switch (type)
            {
                case TexType.BodyOver:
                case TexType.BodyUnder:
                    cc.AddUpdateCMBodyTexFlags(true, true, true, true);
                    cc.CreateBodyTexture();
                    break;
                case TexType.FaceOver:
                case TexType.FaceUnder:
                    cc.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    cc.CreateFaceTexture();
                    break;
                case TexType.EyeUnderL:
                case TexType.EyeOverL:
                    cc.ChangeEyesKind(0); //todo test sides
                    break;
                case TexType.EyeUnderR:
                case TexType.EyeOverR:
                    cc.ChangeEyesKind(1);
                    break;
                case TexType.EyeUnder:
                case TexType.EyeOver:
                    cc.ChangeEyesKind(2);
                    break;
                case TexType.EyebrowUnder:
                    cc.ChangeEyebrowKind();
                    break;
                case TexType.EyelineUnder:
                    cc.ChangeEyelashesKind();
                    break;
                default:
                    cc.AddUpdateCMBodyTexFlags(true, true, true, true);
                    cc.CreateBodyTexture();
                    cc.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    cc.CreateFaceTexture();
                    cc.ChangeEyesKind(2);
                    cc.ChangeEyebrowKind();
                    cc.ChangeEyelashesKind();
                    break;
            }
#endif

            Util.EnableCharaLoadGC = prevGcClear;
        }

        public static void ApplyOverlay(RenderTexture mainTex, Texture2D blitTex)
        {
            if (blitTex == null) return;

            var rtTemp = RenderTexture.GetTemporary(mainTex.width, mainTex.height, 0, mainTex.format);
            var rta = RenderTexture.active;
            RenderTexture.active = rtTemp;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = rta;

            KoiSkinOverlayMgr.OverlayMat.SetTexture("_Overlay", blitTex);
#if KK || EC
            Graphics.Blit(mainTex, rtTemp, KoiSkinOverlayMgr.OverlayMat);
            Graphics.Blit(rtTemp, mainTex);
#else
            Graphics.Blit(mainTex, rtTemp);
            // Need to use the material on the second blit or body tex gets messed up in AI/HS2/KKS
            Graphics.Blit(rtTemp, mainTex, KoiSkinOverlayMgr.OverlayMat);
#endif

            RenderTexture.ReleaseTemporary(rtTemp);
        }

        /// <summary>
        /// For use with UpdateTexture, returns the most restrictive (fastest) update type that will cover all overlays
        /// </summary>
        private TexType WhatTexturesNeedUpdating()
        {
            var lastStatus = TexType.Unknown;
            foreach (var texType in OverlayStorage.GetAllTypes())
            {
                if (lastStatus != TexType.Unknown)
                {
                    switch (texType)
                    {
                        case TexType.BodyOver:
                        case TexType.BodyUnder:
                            if (lastStatus != TexType.BodyUnder && lastStatus != TexType.BodyOver)
                            {
                                lastStatus = TexType.Unknown;
                                goto ExitLoop;
                            }
                            break;
                        case TexType.FaceOver:
                        case TexType.FaceUnder:
                            if (lastStatus != TexType.FaceUnder && lastStatus != TexType.FaceOver)
                            {
                                lastStatus = TexType.Unknown;
                                goto ExitLoop;
                            }
                            break;
                        case TexType.EyeUnder:
                        case TexType.EyeOver:
                        case TexType.EyeUnderL:
                        case TexType.EyeOverL:
                        case TexType.EyeUnderR:
                        case TexType.EyeOverR:
                            if (lastStatus < TexType.EyeUnder || lastStatus > TexType.EyeOverR)
                            {
                                lastStatus = TexType.Unknown;
                                goto ExitLoop;
                            }
                            break;
                        case TexType.EyebrowUnder:
                            if (lastStatus != TexType.EyebrowUnder)
                            {
                                lastStatus = TexType.Unknown;
                                goto ExitLoop;
                            }
                            break;
                        case TexType.EyelineUnder: //todo kk shadow overlay goes here if ever added
                            if (lastStatus != TexType.EyelineUnder)
                            {
                                lastStatus = TexType.Unknown;
                                goto ExitLoop;
                            }
                            break;
                    }
                }

                lastStatus = texType;
            }

        ExitLoop:
            return lastStatus;
        }
    }
}
