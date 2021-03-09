using System;
using System.Collections.Generic;
using System.Linq;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
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

        private readonly Dictionary<TexType, OverlayTexture> _overlays = new Dictionary<TexType, OverlayTexture>();

        public IEnumerable<KeyValuePair<TexType, OverlayTexture>> Overlays => _overlays.AsEnumerable();

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            var pd = new PluginData { version = 1 };

            foreach (var overlay in Overlays)
            {
                if (overlay.Value != null)
                    pd.data.Add(overlay.Key.ToString(), overlay.Value.Data);
            }

            SetExtendedData(pd);
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            if (maintainState) return;

            var needsUpdate = _overlays.Any();
            RemoveAllOverlays(false);

            var data = GetExtendedData();
            if (data != null)
            {
                if (data.version <= 1)
                    ReadLegacyData(data);
                else
                    ReadData(data);
            }

            if (needsUpdate || _overlays.Any())
                UpdateTexture(TexType.Unknown);
        }

        private void ReadData(PluginData data)
        {
            throw new NotImplementedException();
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
                        _overlays.Add(TexType.EyeOverL, new OverlayTexture(bytes));
                        _overlays.Add(TexType.EyeOverR, new OverlayTexture(bytes));
                    }
                    else if (texType == TexType.EyeUnder)
                    {
                        _overlays.Add(TexType.EyeUnderL, new OverlayTexture(bytes));
                        _overlays.Add(TexType.EyeUnderR, new OverlayTexture(bytes));
                    }
                    else
                    {
                        _overlays.Add(texType, new OverlayTexture(bytes));
                    }
                }
            }
        }

        public void ApplyOverlayToRT(RenderTexture bodyTexture, TexType overlayType)
        {
            foreach (var overlayTexture in GetOverlayTextures(overlayType))
                ApplyOverlay(bodyTexture, overlayTexture);
        }

        internal IEnumerable<Texture2D> GetOverlayTextures(TexType overlayType)
        {
            if (_overlays.TryGetValue(overlayType, out var tex))
                yield return tex.Texture;

            foreach (var additionalTexture in AdditionalTextures.Where(x => x.OverlayType == overlayType && x.Texture != null).OrderBy(x => x.ApplyOrder))
                yield return additionalTexture.Texture;
        }

        internal static void ApplyOverlays(RenderTexture targetTexture, IEnumerable<Texture2D> overlays)
        {
            foreach (var overlay in overlays)
                ApplyOverlay(targetTexture, overlay);
        }

        public OverlayTexture SetOverlayTex(byte[] overlayTex, TexType overlayType)
        {
            if (overlayType == TexType.EyeOver || overlayType == TexType.EyeUnder)
            {
                SetOverlayTex(overlayTex, overlayType + 2);
                return SetOverlayTex(overlayTex, overlayType + 4); //todo return the correct thing
            }

            _overlays.TryGetValue(overlayType, out var existing);

            if (overlayTex == null)
            {
                // Remove the overlay
                existing?.Dispose();
                _overlays.Remove(overlayType);
                existing = null;
            }
            else
            {
                // Update or add
                if (existing == null)
                {
                    existing = new OverlayTexture(overlayTex);
                    _overlays.Add(overlayType, existing);
                }
                else
                {
                    existing.Data = overlayTex;
                }
            }

            UpdateTexture(overlayType);

            return existing;
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
            foreach (var kvp in _overlays)
                kvp.Value.Dispose();
            _overlays.Clear();

            if (removeAdditional)
                AdditionalTextures.Clear();
        }

        public static void UpdateTexture(ChaControl cc, TexType type)
        {
            if (cc == null) return;
            if (cc.customTexCtrlBody == null || cc.customTexCtrlFace == null) return;
#if KK || EC
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
#if KK || EC //todo the same?
            Graphics.Blit(mainTex, rtTemp, KoiSkinOverlayMgr.OverlayMat);
            Graphics.Blit(rtTemp, mainTex);
#else
            Graphics.Blit(mainTex, rtTemp);
            // Need to use the material on the second blit or body tex gets messed up in AI/HS2
            Graphics.Blit(rtTemp, mainTex, KoiSkinOverlayMgr.OverlayMat);
#endif

            RenderTexture.ReleaseTemporary(rtTemp);
        }
    }
}
