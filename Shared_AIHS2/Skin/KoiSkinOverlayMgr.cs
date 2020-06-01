﻿using System.IO;
using AIChara;
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
        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;

            ExportDirectory = Config.AddSetting("Maker", "Overlay export/open folder", _defaultOverlayDirectory, "The value needs to be a valid full path to an existing folder. Default folder will be used if the value is invalid. Exported overlays will be saved there, and by default open overlay dialog will show this directory.");
            CompressTextures = Config.AddSetting("General", "Compress overlay textures in RAM", false, "Reduces RAM usage to about 1/4th at the cost of lower quality. Use when loading lots of characters with overlays if you're running out of memory.");

            var ab = AssetBundle.LoadFromMemory(ResourceUtils.GetEmbeddedResource("composite.unity3d"));
            OverlayMat = new Material(ab.LoadAsset<Shader>("composite"));
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
        /// Old loading logic from folders
        /// </summary>
        internal static byte[] GetOldStyleOverlayTex(TexType texType, ChaControl chaControl)
        {
            return null;
        }
    }
}
