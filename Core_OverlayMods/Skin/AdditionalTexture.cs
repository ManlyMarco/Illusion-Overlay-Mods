using UnityEngine;

namespace KoiSkinOverlayX
{
    /// <summary>
    /// An overlay texture that can be added to the overlay stack. 
    /// Add to <see cref="KoiSkinOverlayController.AdditionalTextures"/> after each character reload.
    /// Make sure to check if your texture doesn't already exist before adding!
    /// </summary>
    public class AdditionalTexture
    {
        /// <summary>
        /// Create a new additional overlay texture.
        /// </summary>
        /// <param name="texture">Overlay texture</param>
        /// <param name="overlayType">What part to apply the overlay to</param>
        /// <param name="tag">Tag for self use</param>
        public AdditionalTexture(Texture2D texture, TexType overlayType, object tag)
        {
            Texture = texture;
            Tag = tag;
            OverlayType = overlayType;
        }

        /// <summary>
        /// Create a new additional overlay texture.
        /// </summary>
        /// <param name="texture">Overlay texture</param>
        /// <param name="overlayType">What part to apply the overlay to</param>
        /// <param name="tag">Tag for self use</param>
        /// <param name="applyOrder">Order in which the overlay isapplied relative to other additional overlay textures. Lower number is applied earlier. Default is 0.</param>
        public AdditionalTexture(Texture2D texture, TexType overlayType, object tag, int applyOrder)
        {
            Texture = texture;
            Tag = tag;
            OverlayType = overlayType;
            ApplyOrder = applyOrder;
        }

        public Texture2D Texture { get; }
        public object Tag { get; }
        public TexType OverlayType { get; }
        public int ApplyOrder { get; }
    }
}