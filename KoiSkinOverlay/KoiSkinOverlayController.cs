using System;
using System.Collections.Generic;
using System.Linq;
using ExtensibleSaveFormat;
using MakerAPI;
using MakerAPI.Chara;
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

        private readonly Dictionary<TexType, Texture2D> _overlays = new Dictionary<TexType, Texture2D>();

        public IEnumerable<KeyValuePair<TexType, Texture2D>> Overlays => _overlays.AsEnumerable();

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            var pd = new PluginData { version = 1 };

            foreach (var overlay in Overlays)
                pd.data.Add(overlay.Key.ToString(), overlay.Value.EncodeToPNG());

            SetExtendedData(pd);
        }

        protected override void OnReload(GameMode currentGameMode)
        {
            if (!KoiSkinOverlayGui.MakerLoadFromCharas) return;

            var needsUpdate = _overlays.Any();
            _overlays.Clear();

            var data = GetExtendedData();
            foreach (TexType texType in Enum.GetValues(typeof(TexType)))
            {
                if (texType == TexType.Unknown) continue;

                if (data != null
                    && data.data.TryGetValue(texType.ToString(), out var texData)
                    && texData is byte[] bytes)
                {
                    var tex = Util.TextureFromBytes(bytes);
                    if (tex != null)
                    {
                        _overlays.Add(texType, tex);
                        continue;
                    }
                }

                // Fall back to old-style overlays in a folder
                var oldTex = KoiSkinOverlayMgr.GetOldStyleOverlayTex(texType, ChaControl);
                if (oldTex != null)
                    _overlays.Add(texType, oldTex);
            }

            if (needsUpdate || _overlays.Any())
                UpdateTexture(TexType.Unknown);
        }

        public void ApplyOverlayToRT(RenderTexture bodyTexture, TexType overlayType)
        {
            if (_overlays.TryGetValue(overlayType, out var tex))
                ApplyOverlay(bodyTexture, tex);

            foreach (var additionalTexture in AdditionalTextures)
            {
                if (additionalTexture.OverlayType == overlayType && additionalTexture.Texture != null)
                    ApplyOverlay(bodyTexture, additionalTexture.Texture);
            }
        }

        public Texture ApplyOverlayToTex(Texture bodyTexture, TexType overlayType)
        {
            if (_overlays.TryGetValue(overlayType, out var tex))
                bodyTexture = ApplyOverlay(bodyTexture, KoiSkinOverlayMgr.GetOverlayRT(overlayType), tex);

            foreach (var additionalTexture in AdditionalTextures)
            {
                if (additionalTexture.OverlayType == overlayType && additionalTexture.Texture != null)
                    bodyTexture = ApplyOverlay(bodyTexture, KoiSkinOverlayMgr.GetOverlayRT(overlayType), additionalTexture.Texture);
            }

            return bodyTexture;
        }

        public void SetOverlayTex(Texture2D overlayTex, TexType overlayType)
        {
            _overlays[overlayType] = overlayTex;
            UpdateTexture(overlayType);
        }

        public void UpdateTexture(TexType type)
        {
            UpdateTexture(ChaControl, type);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            foreach (var kvp in _overlays)
                Destroy(kvp.Value);
            _overlays.Clear();
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
                    cc.AddUpdateCMBodyTexFlags(true, true, true, true, true);
                    cc.CreateBodyTexture();
                    break;
                case TexType.FaceOver:
                case TexType.FaceUnder:
                    cc.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    cc.CreateFaceTexture();
                    break;
                default:
                    cc.AddUpdateCMBodyTexFlags(true, true, true, true, true);
                    cc.CreateBodyTexture();
                    cc.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    cc.CreateFaceTexture();
                    break;
            }
        }

        public static Texture ApplyOverlay(Texture mainTex, RenderTexture destTex, Texture2D blitTex)
        {
            if (blitTex == null || destTex == null) return mainTex;

            KoiSkinOverlayMgr.OverlayMat.SetTexture("_Overlay", blitTex);
            Graphics.Blit(mainTex, destTex, KoiSkinOverlayMgr.OverlayMat);
            return destTex;
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
