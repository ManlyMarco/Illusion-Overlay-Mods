using AIChara;
using CharaCustom;
using HarmonyLib;
using UnityEngine;

namespace KoiClothesOverlayX
{
    public partial class KoiClothesOverlayGui
    {
        private static class Hooks
        {
            public static void Init()
            {
                Harmony.CreateAndPatchAll(typeof(Hooks), nameof(KoiClothesOverlayGui));
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsC_Clothes), nameof(CvsC_Clothes.RestrictClothesMenu))]
            public static void UpdateSelectClothesPost(CvsC_Clothes __instance)
            {
                if (!__instance.isActiveAndEnabled) return;

                RefreshInterface();
            }

            [HarmonyPrefix]
#if HS2
            [HarmonyPatch("IllusionFixes.MakerOptimizations+VirtualizeMakerLists+VirtualListData, HS2_Fix_MakerOptimizations", "GetThumbSprite")]
#elif AI
            [HarmonyPatch("IllusionFixes.MakerOptimizations+VirtualizeMakerLists+VirtualListData, AI_Fix_MakerOptimizations", "GetThumbSprite")]
#endif
            private static bool GetThumbSpritePreHook(ref Sprite __result, CustomSelectInfo item)
            {
                if (item.category == (int)ChaListDefine.CategoryNo.st_pattern && item.id == KoiClothesOverlayController.CustomPatternID)
                {
                    __result = KoiClothesOverlayController.GetPatternThumbnail();
                    return false;
                }
                return true;
            }
        }
    }
}
