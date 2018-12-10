/*
    
    Original KoiSkinOverlay by essu, the poem too

    Yea,
    though I walk through the valley of the shadow of death, I will fear no takedown
    for Thou art with me; Thy praise and Thy frogposting they comfort me.
  
*/

using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using ChaCustom;
using ExtensibleSaveFormat;
using Harmony;
using Studio;
using UnityEngine;

namespace KoiSkinOverlayX
{
    internal static class Hooks
    {
        public static void Init()
        {
            HarmonyInstance.Create(nameof(Hooks)).PatchAll(typeof(Hooks));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChaFile), "CopyChaFile", null, null)]
        public static void CopyChaFile(ChaFile dst, ChaFile src)
        {
            var extendedData = ExtendedSave.GetExtendedDataById(src, KoiSkinOverlayMgr.GUID);
            if (extendedData != null)
                ExtendedSave.SetExtendedDataById(dst, KoiSkinOverlayMgr.GUID, extendedData);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CvsExit), "ExitSceneRestoreStatus", new[]
        {
            typeof(string)
        })]
        public static void CvsExit_ExitSceneRestoreStatus(string strInput, CvsExit __instance)
        {
            if (MakerAPI.MakerAPI.Instance.InsideMaker)
                KoiSkinOverlayGui.ExtendedSaveOnCardBeingSaved(Singleton<CustomBase>.Instance.chaCtrl.chaFile);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChaControl), "Initialize", new[]
            {
            typeof(byte),
            typeof(bool),
            typeof(GameObject),
            typeof(int),
            typeof(int),
            typeof(ChaFileControl)
        })]
        public static void ChaControl_InitializePostHook(byte _sex, bool _hiPoly, GameObject _objRoot, int _id, int _no,
                ChaFileControl _chaFile, ChaControl __instance)
        {
            if (!MakerAPI.MakerAPI.Instance.CharaListIsLoading)
                KoiSkinOverlayMgr.GetOrAttachController(__instance);
        }

        #region New method

        private static Texture RebuildTextureHook(CustomTextureCreate __instance, Texture texMain)
        {
            if (!(__instance is CustomTextureControl)) return texMain;
            var overlay = __instance.trfParent?.GetComponent<KoiSkinOverlayController>();
            if (overlay == null) return texMain;

            switch (texMain.name)
            {
                //ChaControl.InitBaseCustomTextureBody
                case "cf_body_00_t":
                    return overlay.ApplyOverlayToTex(texMain, TexType.BodyUnder);
                //ChaControl.InitBaseCustomTextureFace
                case "cf_face_00_t":
                    return overlay.ApplyOverlayToTex(texMain, TexType.FaceUnder);
                default:
                    return texMain;
            }
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
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Hooks), nameof(RebuildTextureHook))),
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

        #endregion

        #region Old method

        [HarmonyPostfix, HarmonyPatch(typeof(CustomTextureCreate), "Initialize")]
        public static void post_CustomTextureControl_Initialize(CustomTextureCreate __instance, ref string createMatName)
        {
            var t = __instance.GetCreateTexture();
            if (t == null) return;
            t.name = createMatName;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CustomTextureCreate), "RebuildTextureAndSetMaterial")]
        public static void post_CustomTextureCreate_RebuildTextureAndSetMaterial(CustomTextureCreate __instance, ref Texture __result)
        {
            if (!(__instance is CustomTextureControl)) return;
            var overlay = __instance.trfParent?.GetComponent<KoiSkinOverlayController>();
            if (overlay == null) return;

            var createTex = __result as RenderTexture;
            switch (createTex?.name)
            {
                //ChaControl.InitBaseCustomTextureBody
                case "cf_m_body_create":
                    overlay.ApplyOverlayToRT(createTex, TexType.BodyOver);
                    break;
                //ChaControl.InitBaseCustomTextureFace
                case "cf_m_face_create":
                    overlay.ApplyOverlayToRT(createTex, TexType.FaceOver);
                    break;
            }
        }

        #endregion

        [HarmonyPostfix]
        [HarmonyPatch(typeof(OCIChar), "ChangeChara", new[]
        {
            typeof(string)
        })]
        public static void OCIChar_ChangeCharaPostHook(string _path, OCIChar __instance)
        {
            var component = __instance.charInfo.gameObject.GetComponent<KoiSkinOverlayController>();
            if (component != null)
                component.StartCoroutine(DelayedLoad(component));
        }

        private static IEnumerator DelayedLoad(KoiSkinOverlayController controller)
        {
            yield return null;
            KoiSkinOverlayMgr.LoadAllOverlayTextures(controller);
        }
    }
}