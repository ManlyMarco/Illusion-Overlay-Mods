using System.Diagnostics;
using UnityEngine;

namespace KoiSkinOverlayX
{
    public static class Util
    {
        public static Texture2D TextureFromBytes(byte[] texBytes)
        {
            if (texBytes == null || texBytes.Length == 0) return null;

            var tex = new Texture2D(2, 2);
            tex.LoadImage(texBytes);
            return tex;
        }

        public static void OpenFileInExplorer(string filename)
        {
            Process.Start("explorer.exe", $"/select, \"{filename}\"");
        }
    }
}