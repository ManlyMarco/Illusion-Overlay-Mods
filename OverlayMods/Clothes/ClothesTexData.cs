using KoiSkinOverlayX;
using MessagePack;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KoiClothesOverlayX
{
    [MessagePackObject]
    public class ClothesTexData
    {
        [IgnoreMember]
        private byte[] _textureBytes;
        [IgnoreMember]
        private Texture2D _texture;

        [IgnoreMember]
        public Texture2D Texture
        {
            get
            {
                if (_texture == null)
                {
                    if (_textureBytes != null)
                        _texture = Util.TextureFromBytes(_textureBytes, KoiSkinOverlayMgr.GetSelectedOverlayTexFormat(false));
                }
                return _texture;
            }
            set
            {
                if (_texture != null)
                    Object.Destroy(_texture);
                _texture = value;
                _textureBytes = value?.EncodeToPNG();
            }
        }

        [Key(0)]
        public byte[] TextureBytes
        {
            get => _textureBytes;
            set
            {
                Texture = null;
                _textureBytes = value;
            }
        }

        [Key(1)]
        public bool Override;

        public void Dispose()
        {
            Texture = null;
        }

        public void Clear()
        {
            Dispose();
            TextureBytes = null;
        }

        public bool IsEmpty()
        {
            return !Override && TextureBytes == null;
        }
    }
}
