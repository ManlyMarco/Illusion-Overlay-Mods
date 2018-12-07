using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtensibleSaveFormat;
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
        public const string Version = "2.0";

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

            rt_Face = new RenderTexture(512, 512, 8);
            DontDestroyOnLoad(rt_Face);

            rt_Body = new RenderTexture(2048, 2048, 8);
            DontDestroyOnLoad(rt_Body);

            Hooks.Init();
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

        private static Texture2D GetOverlayTex(ChaInfo cc, TexType texType)
        {
            // Old loading logic, import if possible
            var charFullname = cc.fileParam?.fullname;
            if (!string.IsNullOrEmpty(charFullname))
            {
                var texFilename = GetTexFilename(charFullname, texType);

                if (File.Exists(texFilename))
                {
                    Logger.Log(LogLevel.Info, $"[KSOX] Importing texture data for {cc.fileParam.fullname} from file {texFilename}");

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

            // New loading logic from extended data
            var chaFile = MakerAPI.MakerAPI.Instance.InsideMaker ? MakerAPI.MakerAPI.Instance.LastLoadedChaFile : cc.chaFile;
            var embeddedTex = GetTexExtData(chaFile, texType);
            if (embeddedTex != null)
            {
                Logger.Log(LogLevel.Info, $"[KSOX] Loading embedded overlay texture data {texType} from card: {cc.fileParam?.fullname ?? "?"}");
                return embeddedTex;
            }

            return null;
        }

        public static string GetTexFilename(string charFullname, TexType texType)
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

        private static void SetTexExtData(ChaFile chaFile, Texture2D tex, TexType texType)
        {
            var data = ExtendedSave.GetExtendedDataById(chaFile, GUID);
            if (data == null)
            {
                if (tex == null) return;
                data = new PluginData { version = 1 };
                ExtendedSave.SetExtendedDataById(chaFile, GUID, data);
            }

            if (tex != null)
                data.data[texType.ToString()] = tex.EncodeToPNG();
            else
                data.data.Remove(texType.ToString());
        }

        private static Texture2D GetTexExtData(ChaFile chaFile, TexType texType)
        {
            var data = ExtendedSave.GetExtendedDataById(chaFile, GUID);
            if (data != null && data.data.TryGetValue(texType.ToString(), out var texData))
            {

                if (texData is byte[] texBytes)
                {
                    var loadedTex = Util.TextureFromBytes(texBytes);
                    if (loadedTex != null) return loadedTex;
                }

                Logger.Log(LogLevel.Debug, $"[KSOX] Embedded overlay texture data {texType.ToString()} is empty or invalid in card {chaFile.charaFileName}");
            }
            return null;
        }

        public static KoiSkinOverlayController GetOrAttachController(ChaControl charInfo)
        {
            if (charInfo == null)
                return null;

            var existing = charInfo.gameObject.GetComponent<KoiSkinOverlayController>();
            return existing ?? charInfo.gameObject.AddComponent<KoiSkinOverlayController>();
        }

        public static RenderTexture GetOverlayRT(TexType overlayType)
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

        internal static void LoadAllOverlayTextures(KoiSkinOverlayController controller)
        {
            foreach (var texType in new[] { TexType.BodyOver, TexType.BodyUnder, TexType.FaceOver, TexType.FaceUnder })
            {
                var tex = GetOverlayTex(controller.ChaControl, texType);
                controller.SetOverlayTex(tex, texType);
            }
        }

        internal static void SaveAllOverlayTextures(KoiSkinOverlayController controller, ChaFile chaFile)
        {
            foreach (var controllerOverlay in controller.Overlays)
                SetTexExtData(chaFile, controllerOverlay.Value, controllerOverlay.Key);
        }
    }
}
