using System.Collections.Generic;
using MessagePack;

namespace KoiClothesOverlayX
{
    [MessagePackObject]
    public class ClothesTexId
    {
        [Key(0)]
        public readonly string ClothesName;
        [Key(1)]
        public readonly ClothesRendererGroup RendererGroup;
        [Key(2)]
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
                return false;

            var id = (ClothesTexId) obj;
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
