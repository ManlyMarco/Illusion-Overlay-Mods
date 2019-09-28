using System;
using System.Collections.Generic;
using System.Linq;
using AIChara;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using UnityEngine;

namespace KoiSkinOverlayX
{
    public class KoiSkinOverlayController : CharaCustomFunctionController
    {
        /// <summary>
        /// Additional overlays to be applied over the KSOX overlay (if any).
        /// Drawn bottom to top based on index. Use <code>UpdateTexture</code> to refresh.
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
            foreach (TexType texType in Enum.GetValues(typeof(TexType)))
            {
                if (texType == TexType.Unknown) continue;

                if (data != null
                    && data.data.TryGetValue(texType.ToString(), out var texData)
                    && texData is byte[] bytes && bytes.Length > 0)
                {
                    _overlays.Add(texType, new OverlayTexture(bytes));
                    continue;
                }

                // Fall back to old-style overlays in a folder
                var oldTex = KoiSkinOverlayMgr.GetOldStyleOverlayTex(texType, ChaControl);
                if (oldTex != null)
                    _overlays.Add(texType, new OverlayTexture(oldTex));
            }

            if (needsUpdate || _overlays.Any())
                UpdateTexture(TexType.Unknown);
        }

        public void ApplyOverlayToRT(RenderTexture bodyTexture, TexType overlayType)
        {
            if (_overlays.TryGetValue(overlayType, out var tex))
                ApplyOverlay(bodyTexture, tex.Texture);

            foreach (var additionalTexture in AdditionalTextures)
            {
                if (additionalTexture.OverlayType == overlayType && additionalTexture.Texture != null)
                    ApplyOverlay(bodyTexture, additionalTexture.Texture);
            }
        }

        public OverlayTexture SetOverlayTex(byte[] overlayTex, TexType overlayType)
        {
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
                default:
                    cc.AddUpdateCMBodyTexFlags(true, true, true, true);
                    cc.CreateBodyTexture();
                    cc.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    cc.CreateFaceTexture();
                    break;
            }
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
            Graphics.Blit(mainTex, rtTemp, KoiSkinOverlayMgr.OverlayMat);
            Graphics.Blit(rtTemp, mainTex);

            RenderTexture.ReleaseTemporary(rtTemp);
        }
    }
}
