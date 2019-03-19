/*
    
    Based on original KoiSkinOverlay by essu, the poem too

    Yea,
    though I walk through the valley of the shadow of death, I will fear no takedown
    for Thou art with me; Thy praise and Thy frogposting they comfort me.
  
*/

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;

namespace KoiSkinOverlayX
{
    internal static class Hooks
    {
        public static void Init()
        {
            HarmonyInstance.Create(nameof(Hooks)).PatchAll(typeof(Hooks));
        }
        
        private static void OverlayBlit(Texture source, RenderTexture dest, Material mat, int pass, CustomTextureCreate instance)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            if (dest == null) throw new System.ArgumentNullException(nameof(dest));
            if (mat == null) throw new System.ArgumentNullException(nameof(mat));

            var overlay = instance.trfParent?.GetComponent<KoiSkinOverlayController>();
            if (overlay != null)
            {
                if (overlay.ChaControl.customTexCtrlFace == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, overlay, TexType.FaceUnder);
                    return;
                }
                if (overlay.ChaControl.customTexCtrlBody == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, overlay, TexType.BodyUnder);
                    return;
                }
                if (overlay.ChaControl.ctCreateEyeL == instance || overlay.ChaControl.ctCreateEyeR == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, overlay, TexType.EyeUnder);
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

        /// <summary>
        /// Underlay hook
        /// </summary>
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

        /// <summary>
        /// Overlay hook
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(CustomTextureCreate), nameof(CustomTextureCreate.RebuildTextureAndSetMaterial))]
        public static void post_CustomTextureCreate_RebuildTextureAndSetMaterial(CustomTextureCreate __instance, ref Texture __result)
        {
            var overlay = __instance.trfParent?.GetComponent<KoiSkinOverlayController>();
            if (overlay == null) return;

            var createTex = __result as RenderTexture;
            if (createTex == null) return;

            if (overlay.ChaControl.customTexCtrlFace == __instance)
                overlay.ApplyOverlayToRT(createTex, TexType.FaceOver);
            else if (overlay.ChaControl.customTexCtrlBody == __instance)
                overlay.ApplyOverlayToRT(createTex, TexType.BodyOver);
            else if (overlay.ChaControl.ctCreateEyeL == __instance || overlay.ChaControl.ctCreateEyeR == __instance)
                overlay.ApplyOverlayToRT(createTex, TexType.EyeOver);
        }
    }
}
