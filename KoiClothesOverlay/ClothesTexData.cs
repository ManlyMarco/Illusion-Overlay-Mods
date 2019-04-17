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
        private byte[] _data;
        [IgnoreMember]
        private Texture2D _texture;

        [IgnoreMember]
        public Texture2D Texture
        {
            get
            {
                if (_texture == null)
                {
                    if (_data != null)
                        _texture = Util.TextureFromBytes(_data, TextureFormat.DXT5);
                }
                return _texture;
            }
        }

        [Key(0)]
        public byte[] TextureBytes
        {
            get => _data;
            set
            {
                Dispose();
                _data = value;
            }
        }

        [Key(1)]
        public bool Override;

        public void Dispose()
        {
            if (_texture != null)
            {
                Object.Destroy(_texture);
                _texture = null;
            }
        }

        public bool IsEmpty()
        {
            return !Override && TextureBytes == null;
        }
    }
}
