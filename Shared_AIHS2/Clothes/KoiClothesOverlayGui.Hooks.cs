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

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CustomSelectScrollViewInfo), nameof(CustomSelectScrollViewInfo.SetData))]
            private static void SetDataPreHook(CustomSelectScrollViewInfo __instance, int _index, CustomSelectScrollController.ScrollData _data)
            {
                if (
                    _data?.info?.category == (int)ChaListDefine.CategoryNo.st_pattern
                    && _data?.info?.id == KoiClothesOverlayController.CustomPatternID
                    && __instance.rows != null
                    && _index < __instance.rows.Length
                    && __instance.rows[_index].imgThumb != null
                )
                    __instance.rows[_index].imgThumb.sprite = KoiClothesOverlayController.GetPatternThumbnail();
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CustomClothesColorSet), nameof(CustomClothesColorSet.ChangePatternImage))]
            private static bool ChangePatternImagePreHook(CustomClothesColorSet __instance)
            {
                if (__instance.nowClothes.parts[__instance.parts].colorInfo[__instance.idx].pattern == KoiClothesOverlayController.CustomPatternID)
                {
                    __instance.imgPattern.sprite = KoiClothesOverlayController.GetPatternThumbnail();
                    return false;
                }
                return true;
            }
        }
    }
}
