using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace KoiSkinOverlayX
{
    public static class Util
    {
        public static bool InsideStudio()
        {
#if EC
            return false;
#else
            return KKAPI.Studio.StudioAPI.InsideStudio;
#endif
        }

        public static Texture2D TextureFromBytes(byte[] texBytes, TextureFormat format)
        {
            if (texBytes == null || texBytes.Length == 0) return null;

            var tex = new Texture2D(2, 2, format, false);
            tex.LoadImage(texBytes);
            return tex;
        }

        /// <summary>
        /// Open explorer focused on the specified file or directory
        /// </summary>
        public static void OpenFileInExplorer(string filename)
        {
            if (filename == null)
                throw new ArgumentNullException(nameof(filename));

            try { NativeMethods.OpenFolderAndSelectFile(filename); }
            catch (Exception) { Process.Start("explorer.exe", $"/select, \"{filename}\""); }
        }

        public static Texture2D TextureToTexture2D(this Texture tex)
        {
            var rt = RenderTexture.GetTemporary(tex.width, tex.height);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            GL.Clear(true, true, Color.clear);

            Graphics.Blit(tex, rt);

            var t = new Texture2D(tex.width, tex.height, TextureFormat.ARGB32, false);
            t.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            t.Apply(false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        private static class NativeMethods
        {
            /// <summary>
            /// Open explorer focused on item. Reuses already opened explorer windows unlike Process.Start
            /// </summary>
            public static void OpenFolderAndSelectFile(string filename)
            {
                var pidl = ILCreateFromPathW(filename);
                SHOpenFolderAndSelectItems(pidl, 0, IntPtr.Zero, 0);
                ILFree(pidl);
            }

            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr ILCreateFromPathW(string pszPath);

            [DllImport("shell32.dll")]
            private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, int cild, IntPtr apidl, int dwFlags);

            [DllImport("shell32.dll")]
            private static extern void ILFree(IntPtr pidl);
        }

        public static int GetRecommendedTexSize(TexType texType)
        {
            switch (texType)
            {
#if KK || EC
                case TexType.BodyOver:
                case TexType.BodyUnder:
                    return 2048;
                case TexType.FaceOver:
                case TexType.FaceUnder:
                    return 1024;
                case TexType.EyeUnder:
                case TexType.EyeOver:
                    return 512;
#elif AI || HS2
                case TexType.BodyOver:
                case TexType.BodyUnder:
                case TexType.FaceOver:
                case TexType.FaceUnder:
                    return 4096;
#endif
                default:
                    throw new ArgumentOutOfRangeException(nameof(texType), texType, null);
            }
        }
    }
}