using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KoiSkinOverlayX
{
    [RequireComponent(typeof(ChaControl))]
    public class KoiSkinOverlayController : MonoBehaviour
    {
        private readonly Dictionary<TexType, Texture2D> _overlays = new Dictionary<TexType, Texture2D>();

        public IEnumerable<KeyValuePair<TexType, Texture2D>> Overlays => _overlays.AsEnumerable();

        internal ChaControl ChaControl { get; private set; }

        public void ApplyOverlayToRT(RenderTexture bodyTexture, TexType overlayType)
        {
            if (_overlays.TryGetValue(overlayType, out var tex))
                ApplyOverlay(bodyTexture, tex);
        }

        public Texture ApplyOverlayToTex(Texture bodyTexture, TexType overlayType)
        {
            if (_overlays.TryGetValue(overlayType, out var tex))
                return ApplyOverlay(bodyTexture, KoiSkinOverlayMgr.GetOverlayRT(overlayType), tex);

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

        private void OnDestroy()
        {
            foreach (var kvp in _overlays)
                Destroy(kvp.Value);
            _overlays.Clear();
        }

        private void Start()
        {
            ChaControl = gameObject.GetComponent<ChaControl>();
            KoiSkinOverlayMgr.LoadAllOverlayTextures(this);
        }

        public static void UpdateTexture(ChaControl cc, TexType type)
        {
            if (cc == null) return;
            if(cc.customTexCtrlBody == null || cc.customTexCtrlFace == null) return;

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

        private static Texture ApplyOverlay(Texture mainTex, RenderTexture destTex, Texture2D blitTex)
        {
            if (blitTex == null || destTex == null) return mainTex;

            KoiSkinOverlayMgr.OverlayMat.SetTexture("_Overlay", blitTex);
            Graphics.Blit(mainTex, destTex, KoiSkinOverlayMgr.OverlayMat);
            return destTex;
        }

        private static void ApplyOverlay(RenderTexture mainTex, Texture2D blitTex)
        {
            if (blitTex == null) return;

            KoiSkinOverlayMgr.OverlayMat.SetTexture("_Overlay", blitTex);
            Graphics.Blit(mainTex, mainTex, KoiSkinOverlayMgr.OverlayMat);
        }
    }
}
