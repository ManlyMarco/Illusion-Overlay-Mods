using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KoiSkinOverlayX
{
    public sealed class OverlayTexture : IDisposable
    {
        private byte[] _data;
        private Texture2D _texture;

        public OverlayTexture(byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public byte[] Data
        {
            get => _data;
            set
            {
                Dispose();
                _data = value;
            }
        }

        public Texture2D Texture
        {
            get
            {
                if (_texture == null)
                {
                    if (_data != null)
                        _texture = Util.TextureFromBytes(_data, KoiSkinOverlayMgr.GetSelectedOverlayTexFormat());
                }
                return _texture;
            }
        }

        public void Dispose()
        {
            if (_texture != null)
            {
                Object.Destroy(_texture);
                _texture = null;
            }
        }
    }
}
