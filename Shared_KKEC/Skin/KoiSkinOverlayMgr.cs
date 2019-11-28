using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Utilities;
using OverlayMods;
using UnityEngine;

namespace KoiSkinOverlayX
{
    [BepInPlugin(GUID, "Skin Overlay Mod", Version)]
    [BepInDependency(ExtendedSave.GUID)]
    [BepInDependency(KoikatuAPI.GUID, "1.9.5")]
    public class KoiSkinOverlayMgr : BaseUnityPlugin
    {
        public const string GUID = Metadata.GUID_KSOX;
        internal const string Version = Metadata.Version;

        private static ConfigEntry<bool> CompressTextures { get; set; }
        private static ConfigEntry<string> ExportDirectory { get; set; }

        private static readonly string _defaultOverlayDirectory = Path.Combine(Paths.GameRootPath, "UserData\\Overlays");
        public static string OverlayDirectory
        {
            get
            {
                var path = ExportDirectory.Value;
                return Directory.Exists(path) ? path : _defaultOverlayDirectory;
            }
        }

        internal static Material OverlayMat { get; private set; }
        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;

            ExportDirectory = Config.AddSetting("Maker", "Overlay export/open folder", _defaultOverlayDirectory, "The value needs to be a valid full path to an existing folder. Default folder will be used if the value is invalid. Exported overlays will be saved there, and by default open overlay dialog will show this directory.");
            CompressTextures = Config.AddSetting("General", "Compress overlay textures in RAM", false, "Reduces RAM usage to about 1/4th at the cost of lower quality. Use when loading lots of characters with overlays if you're running out of memory.");

            var ab = AssetBundle.LoadFromMemory(ResourceUtils.GetEmbeddedResource("composite.unity3d"));
            OverlayMat = new Material(ab.LoadAsset<Shader>("assets/composite.shader"));
            DontDestroyOnLoad(OverlayMat);
            ab.Unload(false);

            Hooks.Init();
            CharacterApi.RegisterExtraBehaviour<KoiSkinOverlayController>(GUID);

            Directory.CreateDirectory(OverlayDirectory);
        }

        public static TextureFormat GetSelectedOverlayTexFormat(bool isMask)
        {
            if (isMask)
                return CompressTextures.Value ? TextureFormat.DXT1 : TextureFormat.RG16;
            return CompressTextures.Value ? TextureFormat.DXT5 : TextureFormat.ARGB32;
        }

#if KK
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

            var legacyDir = Path.Combine(Paths.PluginPath, "KoiSkinOverlay");
            var charFolder = $"{legacyDir}/{charFullname}";
            var texFilename = $"{charFolder}/{name}.png";
            return texFilename;
        }
#endif

        /// <summary>
        /// Old loading logic from folders
        /// </summary>
        internal static byte[] GetOldStyleOverlayTex(TexType texType, ChaControl chaControl)
        {
#if KK
            var charFullname = chaControl.fileParam?.fullname;
            if (!string.IsNullOrEmpty(charFullname))
            {
                var texFilename = GetTexFilename(charFullname, texType);

                if (File.Exists(texFilename))
                {
                    Logger.LogInfo($"Importing texture data for {charFullname} from file {texFilename}");

                    try
                    {
                        var fileTexBytes = File.ReadAllBytes(texFilename);
                        var overlayTex = Util.TextureFromBytes(fileTexBytes, TextureFormat.ARGB32);
                        if (overlayTex != null)
                            return overlayTex.EncodeToPNG();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("[KSOX] Failed to load texture from file - " + ex.Message);
                    }
                }
            }
#endif
            return null;
        }
    }
}
