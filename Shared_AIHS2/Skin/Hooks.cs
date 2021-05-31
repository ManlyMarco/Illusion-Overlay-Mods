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

        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), "ChangeTexture", typeof(Renderer), typeof(ChaListDefine.CategoryNo), typeof(int), typeof(ChaListDefine.KeyType), typeof(ChaListDefine.KeyType), typeof(ChaListDefine.KeyType), typeof(int), typeof(string))]
        public static void ChangeTextureHook(ChaControl __instance, Renderer rend, ChaListDefine.CategoryNo type)
        {
            if (type == ChaListDefine.CategoryNo.st_eye)
            {
                var controller = __instance.GetComponent<KoiSkinOverlayController>();
                if (controller == null)
                {
                    KoiSkinOverlayMgr.Logger.LogWarning("No KoiSkinOverlayController found on character " + __instance.fileParam.fullname);
                    return;
                }

                for (int i = 0; i < 2; i++)
                {
                    if (__instance.cmpFace.targetCustom.rendEyes[i] == rend)
                    {
                        var underlays = controller.GetOverlayTextures(i == 0 ? TexType.EyeUnderL : TexType.EyeUnderR).ToList();
                        if (underlays.Count > 0)
                        {
                            var orig = rend.material.GetTexture(ChaShader.PupilTex);
                            var rt = Util.CreateRT(orig.width, orig.height);
                            KoiSkinOverlayController.ApplyOverlays(rt, underlays);
                            // Never destroy the original texture because game caches it, only overwrite
                            // bug memory leak, rt will be replaced next time iris is updated, will be cleaned up on next unloadunusedassets
                            rend.material.SetTexture(ChaShader.PupilTex, rt);
                        }

                        var overlays = controller.GetOverlayTextures(i == 0 ? TexType.EyeOverL : TexType.EyeOverR).ToList();
                        var shaderName = KoiSkinOverlayMgr.EyeOverShader.name;
                        var mat = rend.materials.LastOrDefault(x => x.shader.name == shaderName);
                        if (overlays.Count == 0)
                        {
                            if (mat != null)
                            {
                                rend.materials = rend.materials.Where(x => x != mat).ToArray();
                                Object.Destroy(mat.mainTexture);
                                Object.Destroy(mat);
                            }
                        }
                        else
                        {
                            if (mat == null)
                            {
                                KoiSkinOverlayMgr.Logger.LogDebug($"Adding eye overlay material to {rend.name} on {__instance.fileParam.fullname}");
                                mat = new Material(KoiSkinOverlayMgr.EyeOverShader);
                                rend.materials = rend.materials.AddItem(mat).ToArray();
                            }
                            else
                            {
                                // Clean up previous texture since it's no longer needed
                                Object.Destroy(mat.mainTexture);
                            }

                            var size = Util.GetRecommendedTexSize(TexType.EyeOver);
                            var rt = Util.CreateRT(size, size);
                            KoiSkinOverlayController.ApplyOverlays(rt, overlays);
                            mat.mainTexture = rt;
                        }

                        break;
                    }
                }
            }
            //else if (type == ChaListDefine.CategoryNo.st_eyebrow)
        }
    }
}
