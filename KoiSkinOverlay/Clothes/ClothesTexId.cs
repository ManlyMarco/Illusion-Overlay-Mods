using System.Collections.Generic;

namespace KoiSkinOverlayX.Clothes
{
    internal struct ClothesTexId
    {
        public readonly string ClothesName;
        public readonly ClothesRendererGroup RendererGroup;
        public readonly int RendererId;
        public ClothesTexId(string clothesName, ClothesRendererGroup rendererGroup, int rendererId)
        {
            ClothesName = clothesName;
            RendererId = rendererId;
            RendererGroup = rendererGroup;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ClothesTexId))
            {
                return false;
            }

            var id = (ClothesTexId)obj;
            return ClothesName == id.ClothesName &&
                   RendererGroup == id.RendererGroup &&
                   RendererId == id.RendererId;
        }

        public override int GetHashCode()
        {
            var hashCode = -743066404;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ClothesName);
            hashCode = hashCode * -1521134295 + RendererGroup.GetHashCode();
            hashCode = hashCode * -1521134295 + RendererId.GetHashCode();
            return hashCode;
        }
    }
}