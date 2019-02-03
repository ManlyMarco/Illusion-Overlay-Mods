using KoiSkinOverlayX;
using MessagePack;
using UnityEngine;

namespace KoiClothesOverlayX {
    [MessagePackObject]
    public class ClothesTexData
    {
        [IgnoreMember]
        public Texture2D Texture;

        [Key(1)]
        public bool Override;

        [Key(0)]
        public byte[] TextureBytes
        {
            get => Texture?.EncodeToPNG();
            set => Texture = Util.TextureFromBytes(value);
        }
    }
}