/*
    
    Original KoiSkinOverlay by essu, the poem too

    Yea,
    though I walk through the valley of the shadow of death, I will fear no takedown
    for Thou art with me; Thy praise and Thy frogposting they comfort me.
  
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using Harmony;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace KoiSkinOverlayX
{
    [BepInPlugin(GUID, "KoiSkinOverlayX", Version)]
    [BepInDependency("com.bepis.bepinex.extendedsave")]
    [BepInDependency(MakerAPI.MakerAPI.GUID)]
    public class KoiSkinOverlayX : BaseUnityPlugin
    {
        public const string GUID = "KoiSkinOverlay";
        public const string Version = "2.0";

        public static readonly string OverlayDirectory = Path.Combine(Paths.PluginPath, "KoiSkinOverlay");
        private static Material overlayMat = null;
        private static RenderTexture rt_Face;
        private static RenderTexture rt_Body;

        private static readonly Dictionary<int, Texture2D> BlitTextures = new Dictionary<int, Texture2D>();

        private void Awake()
        {
            HarmonyInstance.Create(nameof(KoiSkinOverlayX)).PatchAll(typeof(KoiSkinOverlayX));
            
            var ab = AssetBundle.LoadFromMemory(Properties.Resources.composite);
            overlayMat = new Material(ab.LoadAsset<Shader>("assets/composite.shader"));
            DontDestroyOnLoad(overlayMat);
            ab.Unload(false);

            rt_Face = new RenderTexture(512, 512, 8);
            DontDestroyOnLoad(rt_Face);

            rt_Body = new RenderTexture(2048, 2048, 8);
            DontDestroyOnLoad(rt_Body);
        }

        private static Texture CompositeStep(CustomTextureCreate __instance, Texture texMain)
        {
            if (!(__instance is CustomTextureControl)) return texMain;
            var cc = __instance.trfParent?.GetComponent<ChaControl>();
            if (cc == null) return texMain;

            switch (texMain.name)
            {
                //ChaControl.InitBaseCustomTextureBody
                case "cf_body_00_t":
                    return ApplyOverlay(texMain, rt_Body, GetOverlayTex(cc, TexType.Body));
                //ChaControl.InitBaseCustomTextureFace
                case "cf_face_00_t":
                    return ApplyOverlay(texMain, rt_Face, GetOverlayTex(cc, TexType.Face));

                default:
                    return texMain;
            }
        }

        private static Texture ApplyOverlay(Texture mainTex, RenderTexture destTex, Texture2D blitTex)
        {
            if (blitTex == null) return mainTex;
            overlayMat.SetTexture("_Overlay", blitTex);
            Graphics.Blit(mainTex, destTex, overlayMat);
            return destTex;
        }

        [HarmonyTranspiler, HarmonyPatch(typeof(CustomTextureCreate), "RebuildTextureAndSetMaterial")]
        public static IEnumerable<CodeInstruction> tpl_CustomTextureCreate_RebuildTextureAndSetMaterial(IEnumerable<CodeInstruction> _instructions)
        {
            /*
                 After SetRenderTarget, before Graphics.Blit
                 Composite texMain + overlay, using cache if available.
             */

            var instructions = new List<CodeInstruction>(_instructions);
            var texMain = AccessTools.Field(typeof(CustomTextureCreate), "texMain");

            var inserts = new CodeInstruction[] {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, texMain),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(KoiSkinOverlayX), nameof(CompositeStep))),
                new CodeInstruction(OpCodes.Stfld, texMain),
            };

            OpCode prev = OpCodes.Add;

            for (var i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                if (instruction.opcode == OpCodes.Call)
                {
                    if (instruction.ToString() != "call Void SetRenderTarget(UnityEngine.RenderTexture)") continue;
                    if (prev != OpCodes.Ldnull) continue;
                    instructions.InsertRange(i, inserts);
                    break;
                }
                prev = instruction.opcode;
            }

            return instructions;
        }

        private void Update()
        {
            if (BlitTextures.Count > 0)
            {
                foreach (var blitTexture in BlitTextures)
                    Destroy(blitTexture.Value);

                BlitTextures.Clear();
            }

#if DEBUG
            if (Input.GetKeyDown(KeyCode.RightControl))
            {
                foreach (var cc in FindObjectsOfType<ChaControl>())
                {
                    UpdateTexture(cc, TexType.Body);
                    UpdateTexture(cc, TexType.Face);
                }
            }
#endif
        }

        private static Texture2D GetOverlayTex(ChaInfo cc, TexType texType)
        {
            var cacheId = Util.CombineHashCodes(cc.GetHashCode(), texType.GetHashCode());
            if (BlitTextures.TryGetValue(cacheId, out Texture2D t)) return t;

            void CacheTex(Texture2D tex)
            {
                if (tex != null)
                {
                    DontDestroyOnLoad(tex);
                    BlitTextures[cacheId] = tex;
                }
            }

            // New loading logic from extended data
            var chaFile = MakerAPI.MakerAPI.Instance.InsideMaker ? MakerAPI.MakerAPI.Instance.CurrentChaFile : cc.chaFile;
            var embeddedTex = GetTexExtData(chaFile, texType);
            if (embeddedTex != null)
            {
                Logger.Log(LogLevel.Info, $"[OverlayX] Loading embedded overlay texture data {texType} from card: {cc.fileParam?.fullname ?? "?"}");
                CacheTex(embeddedTex);
                return embeddedTex;
            }

            // Fall back to old loading logic
            var charFullname = cc.fileParam?.fullname;
            if (!string.IsNullOrEmpty(charFullname))
            {
                var texFilename = GetTexFilename(charFullname, texType);

                if (File.Exists(texFilename))
                {
                    Logger.Log(LogLevel.Info, $"[OverlayX] Importing texture data for {cc.fileParam.fullname} from file {texFilename}");

                    try
                    {
                        var fileTexBytes = File.ReadAllBytes(texFilename);
                        var overlayTex = Util.TextureFromBytes(fileTexBytes);

                        if (overlayTex != null)
                        {
                            CacheTex(overlayTex);
                            SetTexExtData(chaFile, overlayTex, texType);
                            return overlayTex;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error, "[OverlayX] Failed to load texture from file - " + ex.Message);
                        return null;
                    }
                }
            }

            return null;
        }

        public static string GetTexFilename(string charFullname, TexType texType)
        {
            var charFolder = $"{OverlayDirectory}/{charFullname}";
            var texFilename = $"{charFolder}/{texType.ToString()}.png";
            return texFilename;
        }

        public static void UpdateTexture(ChaControl cc, TexType type)
        {
            switch (type)
            {
                case TexType.Body:
                    cc.AddUpdateCMBodyTexFlags(true, true, true, true, true);
                    cc.CreateBodyTexture();
                    break;
                case TexType.Face:
                    cc.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    cc.CreateFaceTexture();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static void SetTexExtData(ChaFile chaFile, Texture2D tex, TexType texType)
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

        public static Texture2D GetTexExtData(ChaFile chaFile, TexType texType)
        {
            var data = ExtendedSave.GetExtendedDataById(chaFile, GUID);
            if (data != null && data.data.TryGetValue(texType.ToString(), out var texData))
            {

                if (texData is byte[] texBytes)
                {
                    var loadedTex = Util.TextureFromBytes(texBytes);
                    if (loadedTex != null) return loadedTex;
                }

                Logger.Log(LogLevel.Debug, $"[OverlayX] Embedded overlay texture data {texType.ToString()} is empty or invalid in card {chaFile.charaFileName}");
            }
            return null;
        }
    }
}
