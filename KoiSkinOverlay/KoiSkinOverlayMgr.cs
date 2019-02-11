using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using MakerAPI.Chara;
using UnityEngine;
using Logger = BepInEx.Logger;
using Resources = KoiSkinOverlayX.Properties.Resources;

namespace KoiSkinOverlayX
{
    [BepInPlugin(GUID, "KSOX (KoiSkinOverlay)", Version)]
    [BepInDependency("com.bepis.bepinex.extendedsave")]
    [BepInDependency(MakerAPI.MakerAPI.GUID)]
    public class KoiSkinOverlayMgr : BaseUnityPlugin
    {
        public const string GUID = "KSOX";
        public const string Version = "3.0";
        public static readonly string OverlayDirectory = Path.Combine(Paths.PluginPath, "KoiSkinOverlay");
        internal static Material OverlayMat { get; private set; }
        private static RenderTexture rt_Face;
        private static RenderTexture rt_Body;

        private void Awake()
        {
            var ab = AssetBundle.LoadFromMemory(Resources.composite);
            OverlayMat = new Material(ab.LoadAsset<Shader>("assets/composite.shader"));
            DontDestroyOnLoad(OverlayMat);
            ab.Unload(false);

            rt_Face = new RenderTexture(1024, 1024, 8);
            DontDestroyOnLoad(rt_Face);

            rt_Body = new RenderTexture(2048, 2048, 8);
            DontDestroyOnLoad(rt_Body);

            Hooks.Init();
            CharacterApi.RegisterExtraBehaviour<KoiSkinOverlayController>(GUID);
        }

#if DEBUG
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightControl))
            {
                foreach (var cc in FindObjectsOfType<KoiSkinOverlayController>())
                    cc.UpdateTexture(TexType.Unknown);
            }
        }
#endif

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
                default:
                    return null;
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

        internal static TexType ParseTexStr(string texName)
        {
            try
            {
                return (TexType)Enum.Parse(typeof(TexType), texName);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error | LogLevel.Message, $"[KSOX] Failed to load embedded texture {texName} - {ex.Message}");
                return TexType.Unknown;
            }
        }

        internal static RenderTexture GetOverlayRT(TexType overlayType)
        {
            switch (overlayType)
            {
                case TexType.BodyOver:
                case TexType.BodyUnder:
                    return rt_Body;
                case TexType.FaceUnder:
                case TexType.FaceOver:
                    return rt_Face;
                default:
                    return null;
            }
        }
    }
}
