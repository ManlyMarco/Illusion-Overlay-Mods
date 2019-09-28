/*
    
    Based on original KoiSkinOverlay by essu, the poem too

    Yea,
    though I walk through the valley of the shadow of death, I will fear no takedown
    for Thou art with me; Thy praise and Thy frogposting they comfort me.
  
*/

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using AIChara;
using BepInEx.Harmony;
using HarmonyLib;
using UnityEngine;

namespace KoiSkinOverlayX
{
    internal static class Hooks
    {
        public static void Init()
        {
            HarmonyWrapper.PatchAll(typeof(Hooks));
        }

        private static void OverlayBlit(Texture source, RenderTexture dest, Material mat, int pass, CustomTextureCreate instance)
        {
            if (source == null)
            {
                KoiSkinOverlayMgr.Logger.LogError(instance.trfParent.name + " source texture is null, can't apply overlays!");
                return;
            }

            if (dest == null) throw new System.ArgumentNullException(nameof(dest));
            if (mat == null) throw new System.ArgumentNullException(nameof(mat));

            var overlay = instance.trfParent?.GetComponent<KoiSkinOverlayController>();
            if (overlay != null)
            {
                if (overlay.ChaControl.customTexCtrlFace?.createCustomTex?.Contains(instance) == true)
                {
                    OverlayBlitImpl(source, dest, mat, pass, overlay, TexType.FaceUnder);
                    overlay.ApplyOverlayToRT(dest, TexType.FaceOver);
                    return;
                }
                if (overlay.ChaControl.customTexCtrlBody?.createCustomTex?.Contains(instance) == true)
                {
                    OverlayBlitImpl(source, dest, mat, pass, overlay, TexType.BodyUnder);
                    overlay.ApplyOverlayToRT(dest, TexType.BodyOver);
                    return;
                }
            }

            // Fall back to original code
            Graphics.Blit(source, dest, mat, pass);
        }

        private static void OverlayBlitImpl(Texture source, RenderTexture dest, Material mat, int pass, KoiSkinOverlayController overlayController, TexType overlayType)
        {
            var trt = RenderTexture.GetTemporary(source.width, source.height, dest.depth, dest.format);
            Graphics.Blit(source, trt);
            overlayController.ApplyOverlayToRT(trt, overlayType);
            Graphics.Blit(trt, dest, mat, pass);
            RenderTexture.ReleaseTemporary(trt);
        }

        /*//private Texture2D GetTexture(ChaListDefine.CategoryNo type, int id, ChaListDefine.KeyType manifestKey, ChaListDefine.KeyType assetBundleKey, ChaListDefine.KeyType assetKey, string addStr = "")
        //public bool ChangeEyesKind(int lr)
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), "ChangeEyesKind", typeof(int))]
        public static void EyeTexHook(ChaControl __instance, int lr)
        {
            if (null == __instance.cmpFace) return;
            Renderer[] rendEyes = __instance.cmpFace.targetCustom.rendEyes;
            if (rendEyes == null) return;

            var overlay = __instance.GetComponent<KoiSkinOverlayController>();
            if (overlay != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    // todo left/right eye
                    if (lr == 2 || lr == i)
                    {
                        if (null != rendEyes[i])
                        {
                            var dest = RenderTexture.GetTemporary(__result.width, __result.height, 0, RenderTextureFormat.ARGB32);
                            OverlayBlitImpl(__result, dest, mat, pass, overlay, TexType.EyeUnder);
                            overlay.ApplyOverlayToRT(dest, TexType.EyeOver);
                        }
                    }
                }
            }
        }*/

        [HarmonyTranspiler, HarmonyPatch(typeof(CustomTextureCreate), nameof(CustomTextureCreate.RebuildTextureAndSetMaterial))]
        public static IEnumerable<CodeInstruction> tpl_CustomTextureCreate_RebuildTextureAndSetMaterial(IEnumerable<CodeInstruction> _instructions)
        {
            foreach (var instruction in _instructions)
            {
                if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo mi && mi.Name == "Blit")
                {
                    // Add the instance to the method call as last argument
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    // Call custom Blit instead
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Hooks), nameof(OverlayBlit)));
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }
}
