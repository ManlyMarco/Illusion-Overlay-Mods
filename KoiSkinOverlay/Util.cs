using System.Diagnostics;
using UnityEngine;

namespace KoiSkinOverlayX
{
    internal static class Util
    {
        public static int CombineHashCodes(int h1, int h2)
        {
            return (((h1 << 5) + h1) ^ h2);
        }

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