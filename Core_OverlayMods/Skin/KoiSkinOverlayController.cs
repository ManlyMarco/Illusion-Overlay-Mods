using System;
using System.Collections.Generic;
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

            if (needsUpdate || OverlayStorage.GetCount() > 0)
                UpdateTexture(TexType.Unknown);
        }

        private void ReadLegacyData(PluginData data)
        {
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
                default:
                    cc.AddUpdateCMBodyTexFlags(true, true, true, true, true);
                    cc.CreateBodyTexture();
                    cc.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    cc.CreateFaceTexture();
                    cc.ChangeSettingEye(true, true, true);
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
                default:
                    cc.AddUpdateCMBodyTexFlags(true, true, true, true);
                    cc.CreateBodyTexture();
                    cc.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    cc.CreateFaceTexture();
                    cc.ChangeEyesKind(2);
                    break;
            }
#endif
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
                            if (lastStatus < TexType.EyeUnder)
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
