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
                        var dicResize = ReadTextureSizeOverrideExtData(pluginData);

                        // Only keep 1st coord
                        foreach (var coordKey in dic.Keys.ToList())
                        {
                            if (coordKey != 0)
                            {
#if DEBUG
                                UnityEngine.Debug.Log("Removing coord " + coordKey);
#endif
                                dic.Remove(coordKey);
                                dicResize.Remove(coordKey);
                            }
                            else
                            {
                                var group = dic[coordKey];
                                var groupResize = dicResize[coordKey];
                                if (group == null)
                                {
                                    dic.Remove(coordKey);
                                    continue;
                                }
                                if (groupResize == null)
                                {
                                    dicResize.Remove(coordKey);
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
                                groupResize.Remove("ct_shoes_inner");
                            }
                        }

                        CleanupTextureList(dic);
                        CleanupTextureList(dicResize);

                        if (dic.Count > 0)
                            SetOverlayExtData(dic, pluginData);
                        if (dicResize.Count > 0)
                            SetTextureSizeOverrideExtData(dicResize, pluginData);
                        if (dic.Count == 0 && dicResize.Count == 0)
                            importedData.Remove(KoiClothesOverlayMgr.GUID);
                    }
                };
#elif KKS
                ExtendedSave.CardBeingImported += (data, mapping) =>
                {
                    if (data.TryGetValue(KoiClothesOverlayMgr.GUID, out var pluginData) && pluginData != null)
                    {
                        var dic = ReadOverlayExtData(pluginData);
                        var dicResize = ReadTextureSizeOverrideExtData(pluginData);
                        // Map coords into a new dictionary based on the mapping
                        var outDic = new Dictionary<ChaFileDefine.CoordinateType, Dictionary<string, ClothesTexData>>(dic.Count);
                        var outDicResize = new Dictionary<ChaFileDefine.CoordinateType, Dictionary<string, int>>(dicResize.Count);
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
                            dicResize.TryGetValue((ChaFileDefine.CoordinateType)map.Key, out var valueResize);
                            if (value != null)
                            {
                                // KKS doesn't have inner shoes
                                valueResize.Remove("ct_shoes_inner");
                                outDicResize[(ChaFileDefine.CoordinateType)map.Value.Value] = valueResize;
                            }
                        }

                        CleanupTextureList(outDic);
                        CleanupTextureList(outDicResize);

                        // Overwrite with the new dictionary
                        if (outDic.Count > 0)
                            SetOverlayExtData(outDic, pluginData);
                        if (outDicResize.Count > 0)
                            SetTextureSizeOverrideExtData(outDicResize, pluginData);
                        if(outDic.Count == 0 && outDicResize.Count == 0)
                            data.Remove(KoiClothesOverlayMgr.GUID);
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
                        toRemoveList = controller.CurrentOverlayTextures.Where(x => KoiClothesOverlayMgr.SubClothesNames.Contains(GetRealId(x.Key)) && !x.Value.IsEmpty()).ToList();
                    else if (!main)
                        toRemoveList = controller.CurrentOverlayTextures.Where(x => KoiClothesOverlayMgr.MainClothesNames[0] == GetRealId(x.Key) && !x.Value.IsEmpty()).ToList();

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
            public static void InitBaseCustomTextureClothesPostHook(ChaControl __instance, bool main, int parts)
            {
                var updated = false;
                var clothesId = GetClothesIdFromKind(main, parts);
                clothesId = main ? MakeColormaskId(clothesId) : MakeColormaskId(clothesId);

                var controller = __instance.GetComponent<KoiClothesOverlayController>();

                for (int i = 0; i < 3; i++)
                {
                    var tex = controller.GetOverlayTex(clothesId, false)?.Texture;
                    if (tex != null)
                    {
                        if (main && parts < __instance.ctCreateClothes.GetLength(0) && i < __instance.ctCreateClothes.GetLength(1) && __instance.ctCreateClothes[parts, i] != null)
                        {
                            updated = true;
                            __instance.ctCreateClothes[parts, i].SetTexture(ChaShader._ColorMask, tex);
                        }
                        else if (parts < __instance.ctCreateClothesSub.GetLength(0) && i < __instance.ctCreateClothesSub.GetLength(1) && __instance.ctCreateClothesSub[parts, i] != null)
                        {
                            updated = true;
                            __instance.ctCreateClothesSub[parts, i].SetTexture(ChaShader._ColorMask, tex);
                        }
                    }
                }

                if (updated)
                {
                    // Make sure any patterns are applied again
                    if (__instance.nowCoordinate.clothes.parts[parts].colorInfo.Any(x => x.pattern > 0))
                        __instance.ChangeCustomClothes(
                                main,
                                parts,
                                false,
                                __instance.nowCoordinate.clothes.parts[parts].colorInfo[0].pattern > 0,
                                __instance.nowCoordinate.clothes.parts[parts].colorInfo[1].pattern > 0,
                                __instance.nowCoordinate.clothes.parts[parts].colorInfo[2].pattern > 0,
                                __instance.nowCoordinate.clothes.parts[parts].colorInfo[3].pattern > 0
                            );
                    if (main)
                    {
                        // Since a custom color mask is now used, enable all color fields to actually make full use of it.
                        var clothesComponent = __instance.GetCustomClothesComponent(parts);
                        clothesComponent.useColorN01 = true;
                        clothesComponent.useColorN02 = true;
                        clothesComponent.useColorN03 = true;
                    }
                    else
                    {
                        foreach (var clothesComponent in __instance.cusClothesSubCmp)
                            if (clothesComponent != null)
                            {
                                clothesComponent.useColorN01 = true;
                                clothesComponent.useColorN02 = true;
                                clothesComponent.useColorN03 = true;
                            }
                    }
                    // Reflect changed UseColors 
                    KoiClothesOverlayGui.RefreshMenuColors();
                }
            }

            public static Texture GetColormask(KoiClothesOverlayController controller, string clothesId)
            {
                if (GetKindIdsFromColormask(clothesId, out int? part, out int? subPart))
                {
                    var listInfo = subPart == null ? controller.ChaControl.infoClothes[(int)part] : controller.ChaControl.infoParts[(int)subPart];
                    var manifest = listInfo.GetInfo(ChaListDefine.KeyType.MainManifest);

                    var mainAb = listInfo.GetInfo(ChaListDefine.KeyType.MainAB);
                    var ab = listInfo.GetInfo(ChaListDefine.KeyType.ColorMask03AB);
                    var texString = listInfo.GetInfo(ChaListDefine.KeyType.ColorMask03Tex);

                    if (texString == "0")
                    {
                        ab = listInfo.GetInfo(ChaListDefine.KeyType.ColorMask02AB);
                        texString = listInfo.GetInfo(ChaListDefine.KeyType.ColorMask02Tex);
                    }
                    if (texString == "0")
                    {
                        ab = listInfo.GetInfo(ChaListDefine.KeyType.ColorMaskAB);
                        texString = listInfo.GetInfo(ChaListDefine.KeyType.ColorMaskTex);
                    }
                    ab = ab == "0" ? mainAb : ab;

                    return CommonLib.LoadAsset<Texture2D>(ab, texString, false, manifest);
                }
                throw new Exception($"Failed to get colormask with id:{clothesId}");
            }
            #endregion

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CustomTextureCreate), nameof(CustomTextureCreate.SetTexture), new Type[] { typeof(int), typeof(Texture) })]
            public static bool CustomTextureCreateSetTexturePrefix(CustomTextureCreate __instance, int propertyID)
            {
                int color = -1;
                if (propertyID == ChaShader._PatternMask1)
                    color = 0;
                else if (propertyID == ChaShader._PatternMask2)
                    color = 1;
                else if (propertyID == ChaShader._PatternMask2)
                    color = 2;

                var controller = __instance.trfParent.GetComponent<KoiClothesOverlayController>();
                if (
                    controller == null
                    || !__instance.CreateInitEnd
                    || color < 0
                ) return true;

                int kind = -1;
                var main = true;

                for (int i = 0; i < 9; i++)
                    for (int j = 0; j < 3; j++)
                        if (controller.ChaControl.ctCreateClothes[i, j] == __instance)
                        {
                            kind = i;
                            goto End;
                        }

                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                        if (controller.ChaControl.ctCreateClothesSub[i, j] == __instance)
                        {
                            kind = 0;
                            main = false;
                            goto End;
                        }
                }
                End:
                if (kind < 0) return true;

                var clothesId = GetClothesIdFromKind(main, kind);
                clothesId = MakePatternId(clothesId, color);

                var tex = controller.GetOverlayTex(clothesId, false)?.Texture;
                if (tex != null)
                {
                    __instance.matCreate.SetTexture(propertyID, tex);
                    return false;
                }

                return true;
            }

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
