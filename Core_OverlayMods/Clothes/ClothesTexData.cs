using KoiSkinOverlayX;
using MessagePack;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KoiClothesOverlayX
{
    /// <summary>
    /// A clothes overlay texture holder.
    /// </summary>
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
                if (value != null && value == _texture) return;
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

        [Key(2)]
        public BlendingMode BlendingMode;

        public void Dispose()
        {
            Object.Destroy(_texture);
            _texture = null;
        }

        public void Clear()
        {
            TextureBytes = null;
        }

        public bool IsEmpty()
        {
            return !Override && TextureBytes == null;
        }
    }

    public enum BlendingMode
    {
        Default = 0,
        LinearAlpha = 1,
    }
}
