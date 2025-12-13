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
        private ulong? _hash;

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
                if (value != null)
                    _textureBytes = value.EncodeToPNG();
                _hash = null;
            }
        }

        [IgnoreMember]
        public ulong Hash
        {
            get
            {
                if (!_hash.HasValue)
                    _hash = CRC64Calculator.CalculateCRC64(_textureBytes, 2 << 11, 2 << 9, true);
                return _hash.Value;
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
                _hash = null;
            }
        }

        [Key(1)]
        public bool Override;

        [Key(2)]
        public OverlayBlendingMode BlendingMode;

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

    public enum OverlayBlendingMode
    {
        Default = 0,
        LinearAlpha = 1,
    }
}
