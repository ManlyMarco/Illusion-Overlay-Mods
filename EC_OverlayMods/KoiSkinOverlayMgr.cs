using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using OverlayMods;
using UnityEngine;
using Resources = OverlayMods.Properties.Resources;

namespace KoiSkinOverlayX
{
    [BepInPlugin(GUID, "ECSOX (EC SkinOverlay)", Version)]
    [BepInDependency(ExtendedSave.GUID)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
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
        internal new static ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;

            ExportDirectory = Config.AddSetting("Maker", "Overlay export/open folder", _defaultOverlayDirectory, "The value needs to be a valid full path to an existing folder. Default folder will be used if the value is invalid. Exported overlays will be saved there, and by default open overlay dialog will show this directory.");
            CompressTextures = Config.AddSetting("General", "Compress overlay textures in RAM", false, "Reduces RAM usage to about 1/4th at the cost of lower quality. Use when loading lots of characters with overlays if you're running out of memory.");

            var ab = AssetBundle.LoadFromMemory(Resources.composite);
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

        /// <summary>
        /// Old loading logic from folders. Not used in EC
        /// </summary>
        internal static byte[] GetOldStyleOverlayTex(TexType texType, ChaControl chaControl)
        {
            return null;
        }
    }
}
