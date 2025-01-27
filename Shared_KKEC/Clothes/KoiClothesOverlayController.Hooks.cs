using System;
using System.Collections.Generic;
using System.Linq;
using ChaCustom;
using ExtensibleSaveFormat;
using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Utilities;
using KoiSkinOverlayX;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KoiClothesOverlayX
{
    public partial class KoiClothesOverlayController
    {
        internal static class Hooks
        {
            public static void Init()
            {
                Harmony.CreateAndPatchAll(typeof(Hooks), nameof(KoiClothesOverlayController));

#if EC
                ExtendedSave.CardBeingImported += importedData =>
                {
                    if (importedData.TryGetValue(KoiClothesOverlayMgr.GUID, out var pluginData) && pluginData != null)
                    {
                        var dic = ReadOverlayExtData(pluginData);

                        // Only keep 1st coord
                        foreach (var coordKey in dic.Keys.ToList())
                        {
                            if (coordKey != 0)
                            {
#if DEBUG
                                UnityEngine.Debug.Log("Removing coord " + coordKey);
#endif
                                dic.Remove(coordKey);
                            }
                            else
                            {
                                var group = dic[coordKey];
                                if (group == null)
                                {
                                    dic.Remove(coordKey);
                                    continue;
                                }
#if EC
                                // Convert shoe overlays to EC format (1 pair instead of 2)
                                if (group.TryGetValue("ct_shoes_outer", out var data))
                                {
                                    group["ct_shoes"] = data;
                                    group.Remove("ct_shoes_outer");
                                }
#endif
                                // Neither EC or KKS use inner shoes
                                group.Remove("ct_shoes_inner");
                            }
                        }

                        CleanupTextureList(dic);

                        if (dic.Count == 0)
                            importedData.Remove(KoiClothesOverlayMgr.GUID);
                        else
                            SetOverlayExtData(dic, pluginData);
                    }
                };
#elif KKS
                ExtendedSave.CardBeingImported += (data, mapping) =>
                {
                    if (data.TryGetValue(KoiClothesOverlayMgr.GUID, out var pluginData) && pluginData != null)
                    {
                        var dic = ReadOverlayExtData(pluginData);
                        // Map coords into a new dictionary based on the mapping
                        var outDic = new Dictionary<ChaFileDefine.CoordinateType, Dictionary<string, ClothesTexData>>(dic.Count);
                        foreach (var map in mapping)
                        {
                            // Discard unused
                            if (map.Value == null) continue;

                            dic.TryGetValue((ChaFileDefine.CoordinateType) map.Key, out var value);
                            if (value != null)
                            {
                                // KKS doesn't have inner shoes
                                value.Remove("ct_shoes_inner");
                                outDic[(ChaFileDefine.CoordinateType) map.Value.Value] = value;
                            }
                        }

                        CleanupTextureList(outDic);

                        // Overwrite with the new dictionary
                        if (outDic.Count == 0)
                            data.Remove(KoiClothesOverlayMgr.GUID);
                        else
                            SetOverlayExtData(outDic, pluginData);
                    }
                };
#endif
            }

            #region Main tex overlays

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(global::ChaControl.ChangeCustomClothes))]
            public static void ChangeCustomClothesPostHook(ChaControl __instance, bool main, int kind)
            {
                var controller = __instance.GetComponent<KoiClothesOverlayController>();
                if (controller == null) return;

                var clothesCtrl = GetCustomClothesComponent(__instance, main, kind);
                if (clothesCtrl == null) return;

                // Clean up no longer used textures when switching between top clothes with 3 parts and 1 part
                if (MakerAPI.InsideMaker && controller.CurrentOverlayTextures != null)
                {
                    List<KeyValuePair<string, ClothesTexData>> toRemoveList = null;
                    if (main && kind == 0)
                        toRemoveList = controller.CurrentOverlayTextures.Where(x => KoiClothesOverlayMgr.SubClothesNames.Contains(x.Key) && x.Value.IsEmpty()).ToList();
                    else if (!main)
                        toRemoveList = controller.CurrentOverlayTextures.Where(x => KoiClothesOverlayMgr.MainClothesNames[0] == x.Key && x.Value.IsEmpty()).ToList();

                    if (toRemoveList != null && toRemoveList.Count > 0)
                    {
                        var removedTextures = toRemoveList.Count(x => x.Value.TextureBytes != null);
                        if (removedTextures > 0)
                            KoiSkinOverlayMgr.Logger.LogMessage($"Removing {removedTextures} no longer used overlay texture(s)");

                        foreach (var toRemove in toRemoveList)
                            controller.GetOverlayTex(toRemove.Key, false)?.Clear();

                        controller.CleanupTextureList();
                    }
                }

                controller.ApplyOverlays(clothesCtrl);
            }

            private static ChaClothesComponent GetCustomClothesComponent(ChaControl chaControl, bool main, int kind)
            {
                // for top clothes it fires once at start with first bool true (main load), then for each subpart with bool false
                // if true, objClothes are used, if false objParts                
                return main ? chaControl.GetCustomClothesComponent(kind) : chaControl.GetCustomClothesSubComponent(kind);
            }

            #endregion

            #region Body masks

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Material), nameof(Material.SetTexture), new[] { typeof(int), typeof(Texture) })]
            public static void BodyMaskHook(Material __instance, int nameID, ref Texture value)
            {
                if (nameID == ChaShader._AlphaMask)
                {
                    var tex = FindBodyMask(__instance);
                    if (tex != null)
                        value = tex;
                }
            }

            private static Texture2D FindBodyMask(Material mat)
            {
                if (mat == null) return null;

                var registration = CharacterApi.GetRegisteredBehaviour(typeof(KoiClothesOverlayController));
                if (registration == null) throw new ArgumentNullException(nameof(registration));
                foreach (var controller in registration.Instances.Cast<KoiClothesOverlayController>())
                {
                    // No clothes on
                    if (controller.ChaControl.objClothes[0] == null)
                        continue;

                    if (controller.ChaControl.customMatBody == mat)
                        return GetBodyMask(controller, MaskKind.BodyMask);

                    if (controller.ChaControl.rendBra != null && controller.ChaControl.rendBra.Any(r => r != null && r.material == mat))
                        return GetBodyMask(controller, MaskKind.BraMask);

                    if (controller.ChaControl.rendInner != null && controller.ChaControl.rendInner.Any(r => r != null && r.material == mat))
                        return GetBodyMask(controller, MaskKind.InnerMask);
                }

                return null;
            }

            private static Texture2D GetBodyMask(KoiClothesOverlayController controller, MaskKind kind)
            {
                var newMask = controller.GetOverlayTex(kind.ToString(), false)?.Texture;
                if (newMask != null)
                {
                    // the field is needed for dumping, doesn't seem to be necessary to overwrite with the custom tex
                    //Traverse maskField = GetMaskField(controller, kind);
                    //maskField.SetValue(newMask);
                    return newMask;
                }
                return null;
            }

            public static Texture GetMask(KoiClothesOverlayController controller, MaskKind kind)
            {
                switch (kind)
                {
                    case MaskKind.BodyMask:
                        return controller.ChaControl.texBodyAlphaMask;
                    case MaskKind.InnerMask:
                        return controller.ChaControl.texInnerAlphaMask;
                    case MaskKind.BraMask:
                        return controller.ChaControl.texBraAlphaMask;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
                }
            }

            #endregion

            #region Colormasks
            [HarmonyPostfix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.InitBaseCustomTextureClothes))]
            public static void ColormaskHook(ChaControl __instance, bool main, int parts)
            {
                var clothesId = GetClothesIdFromKind(main, parts);
                clothesId = main ? GetColormaskId(clothesId, parts) : GetColormaskId(clothesId, 0, parts);

                var registration = CharacterApi.GetRegisteredBehaviour(typeof(KoiClothesOverlayController));
                if (registration == null) throw new ArgumentNullException(nameof(registration));
                foreach (var controller in registration.Instances.Cast<KoiClothesOverlayController>()) {
                    for (int i = 0; i < 3; i++)
                    {
                        var tex = controller.GetOverlayTex(clothesId, false)?.Texture;
                        if (tex != null)
                        {
                            if (main && parts < __instance.ctCreateClothes.GetLength(0) && i < __instance.ctCreateClothes.GetLength(1) && __instance.ctCreateClothes[parts, i] != null)
                            {
                                __instance.ctCreateClothes[parts, i].SetTexture(ChaShader._ColorMask, tex);

                                __instance.GetCustomClothesComponent(parts).useColorN01 = true;
                                __instance.GetCustomClothesComponent(parts).useColorN02 = true;
                                __instance.GetCustomClothesComponent(parts).useColorN03 = true;
                            }
                            else if (parts < __instance.ctCreateClothesSub.GetLength(0) && i < __instance.ctCreateClothesSub.GetLength(1) && __instance.ctCreateClothesSub[parts, i] != null)
                            {
                                __instance.ctCreateClothesSub[parts, i].SetTexture(ChaShader._ColorMask, tex);

                                foreach (var clothesComponent in __instance.cusClothesSubCmp)
                                {
                                    if (clothesComponent != null)
                                    {
                                        clothesComponent.useColorN01 = true;
                                        clothesComponent.useColorN02 = true;
                                        clothesComponent.useColorN03 = true;
                                    }
                                }
                            }
                            KoiClothesOverlayGui.RefreshMenuColors(parts);
                        }
                    }
                }
            }

            public static Texture GetColormask(KoiClothesOverlayController controller, string clothesId)
            {
                var listInfo = controller.ChaControl.infoClothes[GetKindIdsFromColormask(clothesId)[0]];
                var manifest = listInfo.GetInfo(ChaListDefine.KeyType.MainManifest);
                var texString = listInfo.GetInfo(ChaListDefine.KeyType.ColorMaskTex);
                var ab = listInfo.GetInfo(ChaListDefine.KeyType.ColorMaskAB);
                ab = ab == "0" ? listInfo.GetInfo(ChaListDefine.KeyType.MainAB) : ab;
                return CommonLib.LoadAsset<Texture2D>(ab, texString, false, manifest);
            }
            #endregion

#if KK || KKS
            /// <summary>
            /// Handle copying clothes between coordinates
            /// </summary>
            [HarmonyPostfix, HarmonyPatch(typeof(CvsClothesCopy), "CopyClothes")]
            private static void CopyClothesPostfix(TMP_Dropdown[] ___ddCoordeType, Toggle[] ___tglKind, Toggle[] ___tglSubKind)
            {
                var controller = MakerAPI.GetCharacterControl().GetComponent<KoiClothesOverlayController>();
                if (controller == null) return;

                // MainClothesNames is the same as ChaFileDefine.ClothesKind so index lines up
                var copySlots = KoiClothesOverlayMgr.MainClothesNames.Where((x, i) => ___tglKind[i].isOn).ToList();

                // Top clothes are on
                if (___tglKind[0].isOn)
                {
                    // Copy body/bra masks
                    copySlots.AddRange(Enum.GetNames(typeof(MaskKind)));

                    // Check which sub parts of top should be copied
                    // SubClothesNames is the same as ChaFileDefine.ClothesSubKind so index lines up
                    // bug? These toggles don't seem to be doing anything in the game, if top is selected then everything is always copied regardless of these toggles
                    // copySlots.AddRange(KoiClothesOverlayMgr.SubClothesNames.Where((x, i) => ___tglSubKind[i].isOn));
                    copySlots.AddRange(KoiClothesOverlayMgr.SubClothesNames);
                }

                var copySource = (ChaFileDefine.CoordinateType)___ddCoordeType[1].value;
                var copyDestination = (ChaFileDefine.CoordinateType)___ddCoordeType[0].value;

                var sourceDic = controller.GetOverlayTextures(copySource);
                var destinationDic = controller.GetOverlayTextures(copyDestination);

                foreach (var copySlot in copySlots)
                {
                    destinationDic.Remove(copySlot);
                    if (sourceDic.TryGetValue(copySlot, out var val))
                        destinationDic[copySlot] = val;
                }

                if (copyDestination == controller.CurrentCoordinate.Value)
                    controller.RefreshAllTextures();
            }
#endif
        }
    }
}
