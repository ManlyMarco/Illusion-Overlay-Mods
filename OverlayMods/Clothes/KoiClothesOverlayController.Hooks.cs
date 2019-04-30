using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Harmony;
using KKAPI.Maker;
using OverlayMods;
using Logger = KoiClothesOverlayX.KoiClothesOverlayMgr;

namespace KoiClothesOverlayX
{
    public partial class KoiClothesOverlayController
    {
        internal static class Hooks
        {
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
                        toRemoveList = controller.CurrentOverlayTextures.Where(x => KoiClothesOverlayMgr.SubClothesNames.Contains(x.Key)).ToList();
                    else if (!main)
                        toRemoveList = controller.CurrentOverlayTextures.Where(x => KoiClothesOverlayMgr.MainClothesNames[0] == x.Key).ToList();

                    if (toRemoveList != null && toRemoveList.Count > 0)
                    {
                        Logger.Log(LogLevel.Warning | LogLevel.Message, $"[KCOX] Removing {toRemoveList.Count} no longer used Top overlay(s)");
                        foreach (var toRemove in toRemoveList)
                            controller.SetOverlayTex(null, toRemove.Key);
                    }
                }

                controller.ApplyOverlays(clothesCtrl);
            }

            public static void Init(string guid)
            {
                HarmonyPatcher.PatchAll(typeof(Hooks));
            }

            private static ChaClothesComponent GetCustomClothesComponent(ChaControl chaControl, bool main, int kind)
            {
                // for top clothes it fires once at start with first bool true (main load), then for each subpart with bool false
                // if true, objClothes are used, if false objParts                
                return main ? chaControl.GetCustomClothesComponent(kind) : chaControl.GetCustomClothesSubComponent(kind);
            }
        }
    }
}
