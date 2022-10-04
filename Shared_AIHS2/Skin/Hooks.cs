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
using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KoiSkinOverlayX
{
    internal static class Hooks
    {
        public static void Init()
        {
            Harmony.CreateAndPatchAll(typeof(Hooks), nameof(KoiSkinOverlayMgr.GUID));
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
                    var overlays = overlay.GetOverlayTextures(TexType.FaceOver).ToList();
                    if (overlays.Count > 0) KoiSkinOverlayController.ApplyOverlays(dest, overlays);
                    return;
                }
                if (overlay.ChaControl.customTexCtrlBody?.createCustomTex?.Contains(instance) == true)
                {
                    OverlayBlitImpl(source, dest, mat, pass, overlay, TexType.BodyUnder);
                    var overlays = overlay.GetOverlayTextures(TexType.BodyOver).ToList();
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

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeTexture),
                      typeof(Material), typeof(ChaListDefine.CategoryNo), typeof(int), typeof(ChaListDefine.KeyType), typeof(ChaListDefine.KeyType), typeof(ChaListDefine.KeyType), typeof(int), typeof(string))]
        public static void ChangeTextureHook(ChaControl __instance, Material mat, ChaListDefine.CategoryNo type, int propertyID)
        {
            if (type == ChaListDefine.CategoryNo.st_eye)
            {
                for (int i = 0; i < 2; i++)
                {
                    var rend = __instance.cmpFace.targetCustom.rendEyes[i];
                    if (rend != null && rend.material == mat)
                    {
                        var controller = GetController(__instance);
                        ApplyEyeUnderlays(controller, i == 0 ? TexType.EyeUnderL : TexType.EyeUnderR, mat, propertyID);
                        ApplyEyeOverlays(controller, i == 0 ? TexType.EyeOverL : TexType.EyeOverR, rend);
                        break;
                    }
                }
            }
            else if (type == ChaListDefine.CategoryNo.st_eyebrow)
            {
                if (__instance.customTexCtrlFace.matDraw == mat)
                {
                    var controller = GetController(__instance);
                    ApplyEyeUnderlays(controller, TexType.EyebrowUnder, mat, propertyID);
                }
            }
            else if (type == ChaListDefine.CategoryNo.st_eyelash)
            {
                if (__instance.cmpFace != null &&
                    __instance.cmpFace.targetCustom.rendEyelashes != null &&
                    __instance.cmpFace.targetCustom.rendEyelashes.material == mat)
                {
                    var controller = GetController(__instance);
                    ApplyEyeUnderlays(controller, TexType.EyelineUnder, mat, propertyID);
                }
            }
        }

        private static KoiSkinOverlayController GetController(ChaControl instance)
        {
            var controller = instance.GetComponent<KoiSkinOverlayController>();
            if (controller == null)
            {
                KoiSkinOverlayMgr.Logger.LogWarning("No KoiSkinOverlayController found on character " + instance.fileParam.fullname);
                return null;
            }

            return controller;
        }

        private static void ApplyEyeUnderlays(KoiSkinOverlayController controller, TexType texType, Material material, int propertyID)
        {
            var underlays = controller.GetOverlayTextures(texType).ToList();
            if (underlays.Count > 0)
            {
                var orig = material.GetTexture(propertyID);
                int width, height;
                if (orig != null)
                {
                    width = orig.width;
                    height = orig.height;
                }
                else
                {
                    var recommendedTexSize = Util.GetRecommendedTexSize(texType);
                    width = recommendedTexSize.Width;
                    height = recommendedTexSize.Height;
                }

                var rt = Util.CreateRT(width, height);
                KoiSkinOverlayController.ApplyOverlays(rt, underlays);
                // Never destroy the original texture because game caches it, only overwrite
                // bug memory leak, rt will be replaced next time iris is updated, will be cleaned up on next unloadunusedassets
                material.SetTexture(propertyID, rt);
            }
        }

        private static void ApplyEyeOverlays(KoiSkinOverlayController controller, TexType texType, Renderer rend)
        {
            var overlays = controller.GetOverlayTextures(texType).ToList();
            var shaderName = KoiSkinOverlayMgr.EyeOverShader.name;
            var ourMat = rend.materials.LastOrDefault(x => x.shader.name == shaderName);
            if (overlays.Count == 0)
            {
                if (ourMat != null)
                {
                    rend.materials = rend.materials.Where(x => x != ourMat).ToArray();
                    Object.Destroy(ourMat.mainTexture);
                    Object.Destroy(ourMat);
                }
            }
            else
            {
                if (ourMat == null)
                {
                    KoiSkinOverlayMgr.Logger.LogDebug($"Adding eye overlay material to {rend.name} on {controller.ChaControl.fileParam.fullname}");
                    ourMat = new Material(KoiSkinOverlayMgr.EyeOverShader);
                    rend.materials = rend.materials.AddItem(ourMat).ToArray();
                }
                else
                {
                    // Clean up previous texture since it's no longer needed
                    Object.Destroy(ourMat.mainTexture);
                }

                var size = Util.GetRecommendedTexSize(texType);
                var rt = Util.CreateRT(size.Width, size.Height);
                KoiSkinOverlayController.ApplyOverlays(rt, overlays);
                ourMat.mainTexture = rt;
            }
        }
    }
}
