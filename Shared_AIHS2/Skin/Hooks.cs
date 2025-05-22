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
using KoiClothesOverlayX;
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

            var controller = instance.trfParent?.GetComponent<KoiSkinOverlayController>();
            if (controller != null)
            {
                // The albedo CustomTextureCreate will always be the first element in a CustomTextureControl's createCustomTex array
                if (controller.ChaControl.customTexCtrlFace?.createCustomTex[0] == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, controller, TexType.FaceUnder, TexType.FaceOver);
                    return;
                }
                if (controller.ChaControl.customTexCtrlBody?.createCustomTex[0] == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, controller, TexType.BodyUnder, TexType.BodyOver);
                    return;
                }

                // The metallic/gloss CustomTextureCreate will always be the second element in a CustomTextureControl's createCustomTex array
                if (controller.ChaControl.customTexCtrlFace?.createCustomTex[1] == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, controller, TexType.FaceDetailUnder, TexType.FaceDetailOver);
                    return;
                }
                if (controller.ChaControl.customTexCtrlBody?.createCustomTex[1] == instance)
                {
                    OverlayBlitImpl(source, dest, mat, pass, controller, TexType.BodyDetailUnder, TexType.BodyDetailOver);
                    return;
                }
            }

            var controllerClothes = instance.trfParent?.GetComponent<KoiClothesOverlayController>();
            if (controllerClothes != null && KoiSkinOverlayMgr.SizeLimit.Value != KoiSkinOverlayMgr.TextureSizeLimit.Original)
            {
                string clothesId = null;

                if (controller.ChaControl?.ctCreateClothes != null)
                    for (int kind = 0; kind < controller.ChaControl.ctCreateClothes.GetLength(0); kind++)
                        if (Enumerable.Range(0, controller.ChaControl.ctCreateClothes.GetLength(1)).Any(x => controller.ChaControl.ctCreateClothes[kind, x] == instance))
                        {
                            clothesId = KoiClothesOverlayMgr.MainClothesNames[kind];
                            break;
                        }

                if (clothesId != null)
                {
                    OverlayBlitImpl(source, dest, mat, pass, controllerClothes, clothesId);
                    return;
                }
            }

            // Fall back to original code
            Graphics.Blit(source, dest, mat, pass);
        }

        private static void OverlayBlitImpl(Texture source, RenderTexture dest, Material mat, int pass, KoiClothesOverlayController controller, string clothesId)
        {
            var tex = controller.GetOverlayTex(clothesId, false);
            var texOverride = controller.GetOverlayTex(KoiClothesOverlayController.MakeOverrideId(clothesId), false);
            var texColor = controller.GetOverlayTex(KoiClothesOverlayController.MakeColormaskId(clothesId), false);
            var newSize = controller.GetTextureSizeOverride(clothesId);

            // Increase/decrease output texture size if needed to accomodate large overlays
            if (KoiSkinOverlayMgr.SizeLimit.Value != KoiSkinOverlayMgr.TextureSizeLimit.Original)
            {
                var outSize = KoiSkinOverlayMgr.GetOutputSize(type: TexType.Unknown,
                                                              original: dest,
                                                              maxWidth: Mathf.Max(texColor?.Texture?.width ?? 0, Mathf.Max(tex?.Texture?.width ?? 0, newSize, texOverride?.Texture?.width ?? 0)),
                                                              maxHeight: Mathf.Max(texColor?.Texture?.width ?? 0, Mathf.Max(tex?.Texture?.height ?? 0, newSize, texOverride?.Texture?.height ?? 0)));
                if (dest.width != outSize.Width)
                {
                    KoiSkinOverlayMgr.Logger.LogDebug($"Changing dest texture size from {dest.width}x{dest.height} to {outSize}");
                    dest.Release();
                    dest.width = outSize.Width;
                    dest.height = outSize.Height;
                    dest.Create();
                    Graphics.SetRenderTarget(dest);
                    GL.Clear(false, true, Color.clear);
                    Graphics.SetRenderTarget(null);
                }
            }

            if (texOverride != null && texOverride.Texture != null)
            {
                var trt = RenderTexture.GetTemporary(source.width, source.height, dest.depth, dest.format);
                KoiSkinOverlayController.ApplyOverlay(trt, texOverride.Texture);
                Graphics.Blit(trt, dest, mat, pass);
                RenderTexture.ReleaseTemporary(trt);
            }
            else Graphics.Blit(source, dest, mat, pass);
        }

        private static void OverlayBlitImpl(Texture source, RenderTexture dest, Material mat, int pass, KoiSkinOverlayController controller, TexType underlayType, TexType overlayType)
        {
            var underlays = controller.GetOverlayTextures(underlayType).ToList();
            var overlays = controller.GetOverlayTextures(overlayType).ToList();
            var anyUnderlays = underlays.Count > 0;
            var anyOverlays = overlays.Count > 0;

            // Increase/decrease output texture size if needed to accomodate large overlays
            if (KoiSkinOverlayMgr.SizeLimit.Value != KoiSkinOverlayMgr.TextureSizeLimit.Original && (anyUnderlays || anyOverlays))
            {
                var outSize = KoiSkinOverlayMgr.GetOutputSize(type: underlayType,
                                                              original: dest,
                                                              maxWidth: underlays.Concat(overlays).Max(x => x.width),
                                                              maxHeight: underlays.Concat(overlays).Max(x => x.height));
                if (dest.width != outSize.Width)
                {
                    KoiSkinOverlayMgr.Logger.LogDebug($"Changing dest texture size from {dest.width}x{dest.height} to {outSize}");
                    dest.Release();
                    dest.width = outSize.Width;
                    dest.height = outSize.Height;
                    dest.Create();
                    Graphics.SetRenderTarget(dest);
                    GL.Clear(false, true, Color.clear);
                    Graphics.SetRenderTarget(null);
                }
            }

            if (anyUnderlays)
            {
                //todo get size, set RT size as needed, might need a gl clear if size did change
                var trt = RenderTexture.GetTemporary(source.width, source.height, dest.depth, dest.format);
                Graphics.Blit(source, trt);
                KoiSkinOverlayController.ApplyOverlays(trt, underlays);
                Graphics.Blit(trt, dest, mat, pass);
                RenderTexture.ReleaseTemporary(trt);
            }
            else
            {
                // Fall back to original code
                Graphics.Blit(source, dest, mat, pass);
            }

            if (anyOverlays) 
                KoiSkinOverlayController.ApplyOverlays(dest, overlays);
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
                var size = KoiSkinOverlayMgr.GetOutputSize(texType, orig, underlays.Max(x => x.width), underlays.Max(x => x.height));
                var rt = Util.CreateRT(size);
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

                var size = KoiSkinOverlayMgr.GetOutputSize(texType, null, overlays.Max(x => x.width), overlays.Max(x => x.height));
                var rt = Util.CreateRT(size);
                KoiSkinOverlayController.ApplyOverlays(rt, overlays);
                ourMat.mainTexture = rt;
            }
        }
    }
}
