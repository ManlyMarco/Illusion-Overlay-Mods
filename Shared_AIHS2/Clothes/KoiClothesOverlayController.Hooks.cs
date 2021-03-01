using System.Linq;
using AIChara;
using BepInEx.Harmony;
using HarmonyLib;
using KKAPI.Maker;
using KoiSkinOverlayX;

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
            [HarmonyPatch(typeof(ChaControl), nameof(AIChara.ChaControl.ChangeCustomClothes))]
            public static void ChangeCustomClothesPostHook(ChaControl __instance, int kind)
            {
                var controller = __instance.GetComponent<KoiClothesOverlayController>();
                if (controller == null) return;

                // Clean up no longer used textures after some clothes slots get disabled
                if (MakerAPI.InsideMaker && controller.CurrentOverlayTextures != null)
                {
                    var toRemoveList = controller.CurrentOverlayTextures.Where(x => !x.Value.IsEmpty() && controller.GetCustomClothesComponent(x.Key) == null).ToList();

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

            #endregion
        }
    }
}
