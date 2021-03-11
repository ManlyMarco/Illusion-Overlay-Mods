using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Harmony;
using ChaCustom;
using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
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
                HarmonyWrapper.PatchAll(typeof(Hooks));
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

            public static Traverse GetMaskField(KoiClothesOverlayController controller, MaskKind kind)
            {
                return Traverse.Create(controller.ChaControl).Property(GetMaskFieldName(kind));
            }

            private static string GetMaskFieldName(MaskKind kind)
            {
                switch (kind)
                {
                    case MaskKind.BodyMask:
                        return "texBodyAlphaMask";
                    case MaskKind.InnerMask:
                        return "texInnerAlphaMask";
                    case MaskKind.BraMask:
                        return "texBraAlphaMask";
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
                }
            }

            #endregion

#if KK
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

                // SubClothesNames is the same as ChaFileDefine.ClothesSubKind so index lines up
                if (___tglKind[0].isOn)
                {
                    // If Top is on, check which parts of it should be copied
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
