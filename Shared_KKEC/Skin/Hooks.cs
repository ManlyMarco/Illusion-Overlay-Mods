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
using ExtensibleSaveFormat;
using HarmonyLib;
using UnityEngine;

namespace KoiSkinOverlayX
{
    internal static class Hooks
    {
        public static void Init()
        {
            Harmony.CreateAndPatchAll(typeof(Hooks), nameof(KoiSkinOverlayMgr.GUID));

#if KKS
            ExtendedSave.CardBeingImported += (data, mapping) =>
            {
                if (data.TryGetValue(KoiSkinOverlayMgr.GUID, out var pluginData) && pluginData != null)
                    OverlayStorage.ImportFromKK(pluginData, mapping);
            };
#endif
        }

        private static void OverlayBlit(Texture source, RenderTexture dest, Material mat, int pass, CustomTextureCreate instance)
        {
            if (!instance.CreateInitEnd) return;

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
                if (overlay.ChaControl.customTexCtrlFace == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, overlay, TexType.FaceUnder);
                    var overlays = overlay.GetOverlayTextures(TexType.FaceOver).ToList();
                    if (overlays.Count > 0) KoiSkinOverlayController.ApplyOverlays(dest, overlays);
                    return;
                }
                if (overlay.ChaControl.customTexCtrlBody == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, overlay, TexType.BodyUnder);
                    var overlays = overlay.GetOverlayTextures(TexType.BodyOver).ToList();
                    if (overlays.Count > 0) KoiSkinOverlayController.ApplyOverlays(dest, overlays);
                    return;
                }
                if (overlay.ChaControl.ctCreateEyeL == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, overlay, TexType.EyeUnderL);
                    var overlays = overlay.GetOverlayTextures(TexType.EyeOverL).ToList();
                    if (overlays.Count > 0) KoiSkinOverlayController.ApplyOverlays(dest, overlays);
                    return;
                }
                if (overlay.ChaControl.ctCreateEyeR == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, overlay, TexType.EyeUnderR);
                    var overlays = overlay.GetOverlayTextures(TexType.EyeOverR).ToList();
                    if (overlays.Count > 0) KoiSkinOverlayController.ApplyOverlays(dest, overlays);
                    return;
                }
            }

            // Fall back to original code
            Graphics.Blit(source, dest, mat, pass);
        }

        private static void OverlayBlitImpl(Texture source, RenderTexture dest, Material mat, int pass, KoiSkinOverlayController overlayController, TexType overlayType)
        {
            var overlays = overlayController.GetOverlayTextures(overlayType).ToList();
            if (overlays.Count > 0)
            {
            var trt = RenderTexture.GetTemporary(source.width, source.height, dest.depth, dest.format);
            Graphics.Blit(source, trt);
                KoiSkinOverlayController.ApplyOverlays(trt, overlays);
            Graphics.Blit(trt, dest, mat, pass);
            RenderTexture.ReleaseTemporary(trt);
        }
            else
            {
                // Fall back to original code
                Graphics.Blit(source, dest, mat, pass);
            }
        }

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
