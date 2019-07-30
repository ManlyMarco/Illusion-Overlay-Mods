using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Harmony;
using KKAPI.Chara;
using KKAPI.Maker;
using OverlayMods;
using UnityEngine;
using Logger = KoiClothesOverlayX.KoiClothesOverlayMgr;

namespace KoiClothesOverlayX
{
    public partial class KoiClothesOverlayController
    {
        internal static class Hooks
        {
            public static void Init()
            {
                HarmonyPatcher.PatchAll(typeof(Hooks));
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
                        Logger.Log(LogLevel.Warning | LogLevel.Message, $"[KCOX] Removing {toRemoveList.Count} no longer used Top overlay(s)");

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

            internal static readonly Dictionary<MaskKind, Texture> OrigAlphaMasks = new Dictionary<MaskKind, Texture>();

            private static Texture2D FindBodyMask(Material mat)
            {
                var registration = CharacterApi.GetRegisteredBehaviour(typeof(KoiClothesOverlayController));
                if (registration == null) throw new ArgumentNullException(nameof(registration));
                foreach (var controller in registration.Instances.Cast<KoiClothesOverlayController>())
                {
                    // No clothes on
                    if (controller.ChaControl.objClothes[0] == null)
                        continue;

                    if (controller.ChaControl.customMatBody == mat)
                        return GetBodyMask(controller, MaskKind.BodyMask);

                    if (controller.ChaControl.rendBra.Any(r => r?.material == mat))
                        return GetBodyMask(controller, MaskKind.BraMask);

                    if (controller.ChaControl.rendInner.Any(r => r?.material == mat))
                        return GetBodyMask(controller, MaskKind.InnerMask);
                }

                return null;
            }

            private static Texture2D GetBodyMask(KoiClothesOverlayController controller, MaskKind kind)
            {
                string fieldName;
                switch (kind)
                {
                    case MaskKind.BodyMask:
                        fieldName = "texBodyAlphaMask";
                        break;
                    case MaskKind.InnerMask:
                        fieldName = "texInnerAlphaMask";
                        break;
                    case MaskKind.BraMask:
                        fieldName = "texBraAlphaMask";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
                }

                var mask = Traverse.Create(controller.ChaControl).Property(fieldName);
                OrigAlphaMasks[kind] = mask.GetValue<Texture>();

                var newMask = controller.GetMask(kind);
                if (newMask != null)
                {
                    mask.SetValue(newMask);
                    return newMask;
                }
                return null;
            }

            #endregion
        }
    }
}
