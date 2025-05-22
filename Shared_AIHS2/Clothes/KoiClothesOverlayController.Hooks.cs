using System;
using System.Collections.Generic;
using System.Linq;
using AIChara;
using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
using KoiSkinOverlayX;
using UnityEngine;

namespace KoiClothesOverlayX
{
    public partial class KoiClothesOverlayController
    {
        internal static class Hooks
        {
            public static void Init()
            {
                Harmony.CreateAndPatchAll(typeof(Hooks), nameof(KoiClothesOverlayController));

                // bug: Something related to overlay mod causes body masks to get messed up in studio on scene load even if chara doesn't use overlays.
                //      It's hard to pinpoint what causes it, could be caused by another plugin. This is a band aid fix to the issue, seems to work fine.
                KKAPI.Studio.SaveLoad.StudioSaveLoadApi.SceneLoad += (sender, args) =>
                {
                    foreach (var cl in UnityEngine.Object.FindObjectsOfType<KoiClothesOverlayController>())
                    {
                        cl.ChaControl.updateAlphaMask = true;
                        cl.ChaControl.updateAlphaMask2 = true;
                    }
                };
            }

            #region Main tex overlays

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(AIChara.ChaControl.ChangeCustomClothes))]
            public static void ChangeCustomClothesPostHook(ChaControl __instance, int kind)
            {
                var controller = __instance.GetComponent<KoiClothesOverlayController>();
                if (controller == null) return;

                // Clean up no longer used textures after some clothes slots get disabled
                if (MakerAPI.InsideMaker && controller.CurrentOverlayTextures != null)
                {
                    var toRemoveList = controller.CurrentOverlayTextures.Where(x => !x.Value.IsEmpty() && controller.GetCustomClothesComponent(GetRealId(x.Key)) == null).ToList();

                    if (toRemoveList.Count > 0)
                    {
                        var removedTextures = toRemoveList.Count(x => x.Value.TextureBytes != null);
                        if (removedTextures > 0)
                            KoiSkinOverlayMgr.Logger.LogMessage($"Removing {removedTextures} no longer used overlay texture(s)");

                        foreach (var toRemove in toRemoveList)
                            controller.GetOverlayTex(toRemove.Key, false)?.Clear();

                        controller.CleanupTextureList();
                    }
                }

                var clothesCtrl = GetCustomClothesComponent(__instance, kind);
                if (clothesCtrl != null)
                    controller.ApplyOverlays(clothesCtrl);
            }

            private static CmpClothes GetCustomClothesComponent(ChaControl chaControl, int kind)
            {
                return chaControl.GetCustomClothesComponent(kind);
            }

            public static Texture GetMainTex(KoiClothesOverlayController controller, string clothesId)
            {
                if (GetKindIdsFromClothesId(clothesId, out int? kind, out int? subKind))
                {
                    var listInfo = controller.ChaControl.infoClothes[(int)kind];
                    var manifest = listInfo.GetInfo(ChaListDefine.KeyType.MainManifest);

                    var mainAb = listInfo.GetInfo(ChaListDefine.KeyType.MainAB);
                    var texString = listInfo.GetInfo(ChaListDefine.KeyType.MainTex03);

                    if (texString == "0")
                        texString = listInfo.GetInfo(ChaListDefine.KeyType.MainTex02);
                    if (texString == "0")
                        texString = listInfo.GetInfo(ChaListDefine.KeyType.MainTex);

                    return CommonLib.LoadAsset<Texture2D>(mainAb, texString, false, manifest);
                }
                throw new Exception($"Failed to get colormask with id:{clothesId}");
            }
            #endregion


            [HarmonyPostfix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(ChaControl), nameof(AIChara.ChaControl.InitBaseCustomTextureClothes))]
            public static void InitBaseCustomTextureClothesPostHook(ChaControl __instance, int parts)
            {
                var updated = false;
                var clothesId = GetClothesIdFromKind(true, parts);
                clothesId = MakeColormaskId(clothesId);

                var controller = __instance.GetComponent<KoiClothesOverlayController>();

                for (int i = 0; i < 3; i++)
                {
                    var tex = controller.GetOverlayTex(clothesId, false)?.Texture;
                    if (tex != null)
                    {
                        if (parts < __instance.ctCreateClothes.GetLength(0) && i < __instance.ctCreateClothes.GetLength(1) && __instance.ctCreateClothes[parts, i] != null)
                        {
                            updated = true;
                            __instance.ctCreateClothes[parts, i].SetTexture(ChaShader.ColorMask, tex);
                        }
                        if (parts < __instance.ctCreateClothesGloss.GetLength(0) && i < __instance.ctCreateClothesGloss.GetLength(1) && __instance.ctCreateClothesGloss[parts, i] != null)
                        {
                            updated = true;
                            __instance.ctCreateClothesGloss[parts, i].SetTexture(ChaShader.ColorMask, tex);
                        }
                    }
                }

                if (updated)
                {
                    // Make sure any patterns are applied again
                    if (__instance.nowCoordinate.clothes.parts[parts].colorInfo.Any(x => x.pattern > 0))
                        __instance.ChangeCustomClothes(
                            parts,
                            false,
                            __instance.nowCoordinate.clothes.parts[parts].colorInfo[0].pattern > 0,
                            __instance.nowCoordinate.clothes.parts[parts].colorInfo[1].pattern > 0,
                            __instance.nowCoordinate.clothes.parts[parts].colorInfo[2].pattern > 0
                        );
                    // Since a custom color mask is now used, enable all color fields to actually make full use of it.
                    var clothesComponent = __instance.GetCustomClothesComponent(parts);
                    clothesComponent.useColorN01 = true;
                    clothesComponent.useColorN02 = true;
                    clothesComponent.useColorN03 = true;
                    // Reflect changed UseColors 
                    KoiClothesOverlayGui.RefreshMenuColors();
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CustomTextureCreate), nameof(CustomTextureCreate.SetTexture), new Type[] { typeof(int), typeof(Texture) })]
            public static bool CustomTextureCreateSetTexturePrefix(CustomTextureCreate __instance, int propertyID)
            {
                int color = -1;
                if (propertyID == ChaShader.PatternMask1)
                    color = 0;
                else if (propertyID == ChaShader.PatternMask2)
                    color = 1;
                else if (propertyID == ChaShader.PatternMask3)
                    color = 2;

                var controller = __instance.trfParent.GetComponent<KoiClothesOverlayController>();
                if (
                    controller == null
                    || color < 0
                ) return true;

                int kind = -1;
                var main = true;

                for (int i = 0; i < controller.ChaControl.ctCreateClothes.GetLength(0); i++)
                    for (int j = 0; j < controller.ChaControl.ctCreateClothes.GetLength(1); j++)
                        if (controller.ChaControl.ctCreateClothes[i, j] == __instance)
                        {
                            kind = i;
                            goto End;
                        }
            End:

                if (kind < 0) return true;

                var clothesId = GetClothesIdFromKind(main, kind);
                clothesId = MakePatternId(clothesId, color);

                var tex = controller.GetOverlayTex(clothesId, false)?.Texture ?? GetPatternPlaceholder();
                if (tex != null && controller.ChaControl.nowCoordinate.clothes.parts[kind].colorInfo[color].pattern == CustomPatternID)
                {
                    __instance.matCreate.SetTexture(propertyID, tex);
                    return false;
                }

                return true;
            }

            public static Texture GetColormask(KoiClothesOverlayController controller, string clothesId)
            {
                if(GetKindIdsFromClothesId(clothesId, out int? kind, out int? subKind))
                {
                    var listInfo = controller.ChaControl.infoClothes[(int)kind];
                    var manifest = listInfo.GetInfo(ChaListDefine.KeyType.MainManifest);

                    var mainAb = listInfo.GetInfo(ChaListDefine.KeyType.MainAB);
                    var texString = listInfo.GetInfo(ChaListDefine.KeyType.ColorMask03Tex);

                    if (texString == "0")
                        texString = listInfo.GetInfo(ChaListDefine.KeyType.ColorMask02Tex);
                    if (texString == "0")
                        texString = listInfo.GetInfo(ChaListDefine.KeyType.ColorMaskTex);

                    return CommonLib.LoadAsset<Texture2D>(mainAb, texString, false, manifest);
                }
                throw new Exception($"Failed to get colormask with id:{clothesId}");
            }

            public static Texture GetPattern(KoiClothesOverlayController controller, string clothesId)
            {
                GetKindIdsFromClothesId(clothesId, out int? kindId, out int? subKindId);
                var color = GetColorFromPattern(clothesId);
                if (kindId != null && color >= 0)
                {
                    if (controller.ChaControl.nowCoordinate.clothes.parts[(int)kindId].colorInfo[color].pattern == CustomPatternID)
                        return GetPatternPlaceholder();

                    var pattern = controller.ChaControl.nowCoordinate.clothes.parts[(int)kindId].colorInfo[color].pattern;
                    var listInfo = controller.ChaControl.lstCtrl.GetListInfo(ChaListDefine.CategoryNo.st_pattern, pattern);
                    if (listInfo != null)
                    {
                        string bundle = listInfo.GetInfo(ChaListDefine.KeyType.MainTexAB);
                        string asset = listInfo.GetInfo(ChaListDefine.KeyType.MainTex);

                        if ("0" != bundle && "0" != asset)
                            return CommonLib.LoadAsset<Texture2D>(bundle, asset);
                        else if (pattern == 0) return null;
                    }
                }
                throw new Exception($"Failed to get colormask with id:{clothesId}");
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaListControl), nameof(ChaListControl.GetCategoryInfo))]
            private static void GetCategoryInfoPostHook(ref Dictionary<int, ListInfoBase> __result, ChaListDefine.CategoryNo type)
            {
                if (type != ChaListDefine.CategoryNo.st_pattern) return;

                var listInfo = new ListInfoBase();
                listInfo.Set(
                    0,
                    (int)type,
                    0,
                    new List<string>() { ChaListDefine.KeyType.Name.ToString(), ChaListDefine.KeyType.ID.ToString() },
                    new List<string>() { "Custom Overlay Pattern", CustomPatternID.ToString() });
                __result.Add(CustomPatternID, listInfo);
                // Ensure the None pattern is first, followed by our overlay pattern. Then just sort like normal
                __result = __result
                    .OrderBy(x => x.Key != 0)
                    .ThenBy(x => x.Key != CustomPatternID)
                    .ThenBy(x => x.Key)
                    .ToDictionary(x => x.Key, x => x.Value);
            }
        }
    }
}
