using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using KKAPI.Utilities;
using Manager;
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
            return texBytes.LoadTexture(format);
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

        public struct TexSize
        {
            public readonly int Width;
            public readonly int Height;
            public TexSize(int width, int height)
            {
                Width = width;
                Height = height;
            }
            public TexSize(int size)
            {
                Width = size;
                Height = size;
            }
            public override string ToString()
            {
                return Width + "x" + Height;
            }
        }

        public static TexSize GetRecommendedTexSize(TexType texType)
        {
            switch (texType)
            {
#if KK || KKS || EC
                case TexType.BodyOver:
                case TexType.BodyUnder:
                    return new TexSize(2048);
                case TexType.FaceOver:
                case TexType.FaceUnder:
                    return new TexSize(1024);
                case TexType.EyeUnder:
                case TexType.EyeOver:
                case TexType.EyeUnderL:
                case TexType.EyeOverL:
                case TexType.EyeUnderR:
                case TexType.EyeOverR:
                case TexType.EyebrowUnder:
                case TexType.EyelineUnder:
                    return new TexSize(512);
#elif AI || HS2
                case TexType.BodyOver:
                case TexType.BodyUnder:
                case TexType.FaceOver:
                case TexType.FaceUnder:
                    return new TexSize(4096);
                case TexType.EyeUnder:
                case TexType.EyeOver:
                case TexType.EyeUnderL:
                case TexType.EyeOverL:
                case TexType.EyeUnderR:
                case TexType.EyeOverR:
                    return new TexSize(512);
                case TexType.EyebrowUnder:
                    return new TexSize(1024);
                case TexType.EyelineUnder:
                    return new TexSize(1024, 512);
#endif
                default:
                    throw new ArgumentOutOfRangeException(nameof(texType), texType, null);
            }
        }

        public static RenderTexture CreateRT(TexSize size) => CreateRT(size.Width, size.Height);
        public static RenderTexture CreateRT(int origWidth, int origHeight)
        {
            var rt = new RenderTexture(origWidth, origHeight, 0);
            var rta = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = rta;
            return rt;
        }

        public static bool EnableCharaLoadGC
        {
#if KKS
            get => Character.enableCharaLoadGCClear;
            set => Character.enableCharaLoadGCClear = value;
#else
            get
            {
                if (Character.IsInstance())
                    return Character.Instance.enableCharaLoadGCClear;
                else
                    return true;
            }
            set
            {
                if (Character.IsInstance())
                    Character.Instance.enableCharaLoadGCClear = value;
            }
#endif
        }
    }
}