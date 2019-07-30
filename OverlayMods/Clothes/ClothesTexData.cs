using System;
using System.Collections.Generic;
using System.Linq;
using KoiSkinOverlayX;
using MessagePack;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KoiClothesOverlayX
{
    [MessagePackObject]
    public class ClothesTexData
    {
        #region Main overlay tex

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

        #endregion

        #region Body masks

        /// <summary>
        /// Needs to be public and not readonly for serializing, do not use otherwise
        /// </summary>
        [Key(5)]
        public Dictionary<MaskKind, byte[]> MaskBytes = new Dictionary<MaskKind, byte[]>();

        [IgnoreMember]
        private readonly Dictionary<MaskKind, Texture2D> _maskTextures = new Dictionary<MaskKind, Texture2D>();

        public Texture2D GetMask(MaskKind kind)
        {
            _maskTextures.TryGetValue(kind, out var tex);
            if (tex == null)
            {
                MaskBytes.TryGetValue(kind, out var bytes);
                if (bytes != null)
                {
                    tex = Util.TextureFromBytes(bytes, KoiSkinOverlayMgr.GetSelectedOverlayTexFormat(true));
                    _maskTextures[kind] = tex;
                }
            }

            return tex;
        }

        public void SetMask(MaskKind kind, Texture2D tex)
        {
            MaskBytes[kind] = tex?.EncodeToPNG();
            _maskTextures[kind] = tex;
        }

        #endregion

        public void Dispose()
        {
            Texture = null;

            foreach (MaskKind maskKind in Enum.GetValues(typeof(MaskKind)))
            {
                _maskTextures.TryGetValue(maskKind, out var tex);
                if (tex != null) GameObject.Destroy(tex);
                _maskTextures.Remove(maskKind);
            }
        }

        public void Clear()
        {
            Dispose();
            TextureBytes = null;
            _maskTextures.Clear();
            MaskBytes.Clear();
        }

        public bool IsEmpty()
        {
            return !Override && TextureBytes == null && MaskBytes.Values.Any(x => x != null);
        }
    }
}
