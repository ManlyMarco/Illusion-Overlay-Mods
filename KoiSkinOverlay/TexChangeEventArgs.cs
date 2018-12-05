using System;
using UnityEngine;

namespace KoiSkinOverlayX
{
    public sealed class TexChangeEventArgs : EventArgs
    {
        public TexChangeEventArgs(Texture2D texture, TexType type)
        {
            Texture = texture;
            Type = type;
        }

        public Texture2D Texture { get; }
        public TexType Type { get; }
    }
}
