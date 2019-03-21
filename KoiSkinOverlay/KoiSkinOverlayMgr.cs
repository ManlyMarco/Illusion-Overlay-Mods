using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using Logger = BepInEx.Logger;
using Resources = KoiSkinOverlayX.Properties.Resources;

namespace KoiSkinOverlayX
{
    [BepInPlugin(GUID, "KSOX (KoiSkinOverlay)", Version)]
    [BepInDependency("com.bepis.bepinex.extendedsave")]
    [BepInDependency(KoikatuAPI.GUID)]
    public class KoiSkinOverlayMgr : BaseUnityPlugin
    {
        public const string GUID = "KSOX";
        internal const string Version = "4.1.3.1";

        public static readonly string OverlayDirectory = Path.Combine(Paths.PluginPath, "KoiSkinOverlay");

        private static RenderTexture _rtBody;
        private static RenderTexture _rtFace;
        private static RenderTexture _rtEye;
        internal static Material OverlayMat { get; private set; }

        private void Awake()
        {
            KoikatuAPI.CheckRequiredPlugin(this, KoikatuAPI.GUID, new Version(KoikatuAPI.VersionConst));

            var ab = AssetBundle.LoadFromMemory(Resources.composite);
            OverlayMat = new Material(ab.LoadAsset<Shader>("assets/composite.shader"));
            DontDestroyOnLoad(OverlayMat);
            ab.Unload(false);

            _rtFace = new RenderTexture(1024, 1024, 8);
            DontDestroyOnLoad(_rtFace);

            _rtBody = new RenderTexture(2048, 2048, 8);
            DontDestroyOnLoad(_rtBody);

            _rtEye = new RenderTexture(512, 512, 8);
            DontDestroyOnLoad(_rtEye);

            Hooks.Init();
            CharacterApi.RegisterExtraBehaviour<KoiSkinOverlayController>(GUID);
        }

        internal static string GetTexFilename(string charFullname, TexType texType)
        {
            string name;

            switch (texType)
            {
                case TexType.BodyOver:
                    name = "Body";
                    break;
                case TexType.FaceOver:
                    name = "Face";
                    break;
                case TexType.Unknown:
                    return null;
                default:
                    name = texType.ToString();
                    break;
            }

            var charFolder = $"{OverlayDirectory}/{charFullname}";
            var texFilename = $"{charFolder}/{name}.png";
            return texFilename;
        }

        /// <summary>
        /// Old loading logic from folders
        /// </summary>
        internal static Texture2D GetOldStyleOverlayTex(TexType texType, ChaControl chaControl)
        {
            var charFullname = chaControl.fileParam?.fullname;
            if (!string.IsNullOrEmpty(charFullname))
            {
                var texFilename = GetTexFilename(charFullname, texType);

                if (File.Exists(texFilename))
                {
                    Logger.Log(LogLevel.Info, $"[KSOX] Importing texture data for {charFullname} from file {texFilename}");

                    try
                    {
                        var fileTexBytes = File.ReadAllBytes(texFilename);
                        var overlayTex = Util.TextureFromBytes(fileTexBytes);

                        if (overlayTex != null)
                            return overlayTex;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error, "[KSOX] Failed to load texture from file - " + ex.Message);
                    }
                }
            }
            return null;
        }

        internal static RenderTexture GetOverlayRT(TexType overlayType)
        {
            switch (overlayType)
            {
                case TexType.BodyOver:
                case TexType.BodyUnder:
                    return _rtBody;
                case TexType.FaceUnder:
                case TexType.FaceOver:
                    return _rtFace;
                case TexType.EyeUnder:
                case TexType.EyeOver:
                    return _rtEye;
                default:
                    return null;
            }
        }
    }
}
