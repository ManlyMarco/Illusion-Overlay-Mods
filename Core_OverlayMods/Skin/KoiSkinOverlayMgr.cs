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
#if AI || HS2
using AIChara;
#endif

namespace KoiSkinOverlayX
{
    [BepInPlugin(GUID, "Skin Overlay Mod", Version)]
    [BepInDependency(ExtendedSave.GUID)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
#if KK || KKS
    [BepInIncompatibility("com.jim60105.kk.irisoverlaybycoordinate")]
    [BepInIncompatibility("com.jim60105.kk.charaoverlaysbasedoncoordinate")]
#endif
    public class KoiSkinOverlayMgr : BaseUnityPlugin
    {
        public const string GUID = Metadata.GUID_KSOX;
        public const string Version = Metadata.Version;

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

        private static Shader LoadShader(string assetName)
        {
            Logger.LogDebug($"Loading shader {assetName} from resources");
            var ab = AssetBundle.LoadFromMemory(ResourceUtils.GetEmbeddedResource("composite.unity3d"));
            var s = ab.LoadAsset<Shader>(assetName) ?? throw new MissingMemberException(assetName + " shader is missing");
            ab.Unload(false);
            return s;
        }

        private static Material _overlayMat;
        internal static Material OverlayMat
        {
            get
            {
                if (_overlayMat == null) _overlayMat = new Material(LoadShader("composite"));
                return _overlayMat;
            }
        }
#if AI || HS2
        private static Shader _eyeOverShader;
        internal static Shader EyeOverShader
        {
            get
            {
                if (_eyeOverShader == null) _eyeOverShader = LoadShader("Eye");
                return _eyeOverShader;
            }
        }
#endif
        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;

            ExportDirectory = Config.AddSetting("Maker", "Overlay export/open folder", _defaultOverlayDirectory, "The value needs to be a valid full path to an existing folder. Default folder will be used if the value is invalid. Exported overlays will be saved there, and by default open overlay dialog will show this directory.");
            CompressTextures = Config.AddSetting("General", "Compress overlay textures in RAM", false, "Reduces RAM usage to about 1/4th at the cost of lower quality. Use when loading lots of characters with overlays if you're running out of memory.");

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
    }
}
