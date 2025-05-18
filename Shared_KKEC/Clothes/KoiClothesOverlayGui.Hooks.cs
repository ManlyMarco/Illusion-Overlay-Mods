using ChaCustom;
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
            [HarmonyPatch(typeof(CustomSelectKind), nameof(CustomSelectKind.OnSelect))]
            public static void UpdateSelectClothesPost(CustomSelectKind __instance)
            {
                if (_refreshInterface == null) return;
                if (_refreshInterfaceRunning) return;

                var type = (CustomSelectKind.SelectKindType)AccessTools.Field(typeof(CustomSelectKind), "type").GetValue(__instance);

                switch (type)
                {
                    case CustomSelectKind.SelectKindType.CosTop:
                    case CustomSelectKind.SelectKindType.CosSailor01:
                    case CustomSelectKind.SelectKindType.CosSailor02:
                    case CustomSelectKind.SelectKindType.CosSailor03:
                    case CustomSelectKind.SelectKindType.CosJacket01:
                    case CustomSelectKind.SelectKindType.CosJacket02:
                    case CustomSelectKind.SelectKindType.CosJacket03:
                        //case CustomSelectKind.SelectKindType.CosTopEmblem:
                        break;
                    case CustomSelectKind.SelectKindType.CosBot:
                        //case CustomSelectKind.SelectKindType.CosBotEmblem:
                        break;
                    case CustomSelectKind.SelectKindType.CosBra:
                        //case CustomSelectKind.SelectKindType.CosBraEmblem:
                        break;
                    case CustomSelectKind.SelectKindType.CosShorts:
                        //case CustomSelectKind.SelectKindType.CosShortsEmblem:
                        break;
                    case CustomSelectKind.SelectKindType.CosGloves:
                        //case CustomSelectKind.SelectKindType.CosGlovesEmblem:
                        break;
                    case CustomSelectKind.SelectKindType.CosPanst:
                        //case CustomSelectKind.SelectKindType.CosPanstEmblem:
                        break;
                    case CustomSelectKind.SelectKindType.CosSocks:
                        //case CustomSelectKind.SelectKindType.CosSocksEmblem:
                        break;
#if KK 
                    case CustomSelectKind.SelectKindType.CosInnerShoes:
                        //case CustomSelectKind.SelectKindType.CosInnerShoesEmblem:
                        break;
                    case CustomSelectKind.SelectKindType.CosOuterShoes:
                        //case CustomSelectKind.SelectKindType.CosOuterShoesEmblem:
                        break;
#elif EC
                    case CustomSelectKind.SelectKindType.CosShoes:
                        //case CustomSelectKind.SelectKindType.CosInnerShoesEmblem:
                        break;
#elif KKS
                    case CustomSelectKind.SelectKindType.CosOuterShoes:
                        //case CustomSelectKind.SelectKindType.CosOuterShoesEmblem:
                        break;
#endif
                    default:
                        return;
                }

                RefreshInterface();
            }

            [HarmonyPrefix]
#if KKS
            [HarmonyPatch("IllusionFixes.MakerOptimizations+VirtualizeMakerLists+VirtualListData, KKS_Fix_MakerOptimizations", "GetThumbSprite")]
#elif KK
            [HarmonyPatch("IllusionFixes.MakerOptimizations+VirtualizeMakerLists+VirtualListData, KK_Fix_MakerOptimizations", "GetThumbSprite")]
#elif EC
            [HarmonyPatch("IllusionFixes.MakerOptimizations+VirtualizeMakerLists+VirtualListData, EC_Fix_MakerOptimizations", "GetThumbSprite")]
#endif
            private static bool GetThumbSpritePreHook(ref Sprite __result, CustomSelectInfo item)
            {
                if (item.category == (int)ChaListDefine.CategoryNo.mt_pattern && item.index == KoiClothesOverlayController.CustomPatternID)
                {
                    __result = KoiClothesOverlayController.GetPatternThumbnail();
                    return false;
                }
                return true;
            }
        }
    }
}
