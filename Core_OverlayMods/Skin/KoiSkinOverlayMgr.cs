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
        internal static ConfigEntry<TextureSizeLimit> SizeLimit { get; private set; }
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

            ExportDirectory = Config.Bind("Maker", "Overlay export/open folder", _defaultOverlayDirectory, "The value needs to be a valid full path to an existing folder. Default folder will be used if the value is invalid. Exported overlays will be saved there, and by default open overlay dialog will show this directory.");
            CompressTextures = Config.Bind("General", "Compress overlay textures in RAM", false, "Reduces RAM usage to about 1/4th at the cost of lower quality. Use when loading lots of characters with overlays if you're running out of memory.");
            SizeLimit = Config.Bind("General", "Texture size limit", TextureSizeLimit.OriginalX2, "If an overlay has a higher resolution than the base image, allow the finished texture to be this much larger than the base image size. Original will force the same resolution as the original texture, OriginalX2 will allow sizes up to twice as large. Increases quality at the cost of higher memory usage, turn off if you're running out of memory. If using Unlimited, consider enabling texture compression. Changes take effect after a scene reload.");

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

        public static Util.TexSize GetOutputSize(TexType type, Texture original, int maxWidth, int maxHeight)
        {
            var origSize = original != null ? new Util.TexSize(original.width, original.height) : Util.GetRecommendedTexSize(type);
            switch (SizeLimit.Value)
            {
                case TextureSizeLimit.Original:
                    return origSize;
                case TextureSizeLimit.OriginalX2:
                case TextureSizeLimit.Unlimited:
                    // To keep the original aspect ratio, find the edge that got increased the most and then calculate the shorter edge based on the size ratio
                    var hDiff = maxHeight / (float)origSize.Height;
                    var wDiff = maxWidth / (float)origSize.Width;
                    var topDiff = Mathf.Max(hDiff, wDiff);
                    if (topDiff <= 1 + float.Epsilon)
                        return origSize;

                    if (SizeLimit.Value == TextureSizeLimit.OriginalX2 && topDiff > 2)
                        topDiff = 2;

                    var maxSize = SystemInfo.maxTextureSize;
                    var result = new Util.TexSize(Mathf.Min(Mathf.RoundToInt(origSize.Width * topDiff), maxSize),
                                                  Mathf.Min(Mathf.RoundToInt(origSize.Height * topDiff), maxSize));
                    //Logger.LogDebug($"old {origSize} new {result} diffs {hDiff} {wDiff} {topDiff} max {maxSize}");
                    return result;

                default:
                    throw new ArgumentOutOfRangeException("value", SizeLimit.Value, "Invalid value");
            }
        }

        public enum TextureSizeLimit
        {
            Original = 0,
            OriginalX2 = 1,
            Unlimited = 10
        }
    }
}
