using UnityEngine;

namespace KoiSkinOverlayX
{
    public class AdditionalTexture
    {
        public AdditionalTexture(Texture2D texture, TexType overlayType, object tag)
        {
            Texture = texture;
            Tag = tag;
            OverlayType = overlayType;
        }

        public Texture2D Texture { get; }
        public object Tag { get; }
        public TexType OverlayType { get; }
    }
}