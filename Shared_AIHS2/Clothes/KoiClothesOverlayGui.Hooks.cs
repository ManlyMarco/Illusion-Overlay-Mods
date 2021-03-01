using BepInEx.Harmony;
using CharaCustom;
using HarmonyLib;

namespace KoiClothesOverlayX
{
    public partial class KoiClothesOverlayGui
    {
        private static class Hooks
        {
            public static void Init()
            {
                HarmonyWrapper.PatchAll(typeof(Hooks));
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsC_Clothes), nameof(CvsC_Clothes.RestrictClothesMenu))]
            public static void UpdateSelectClothesPost(CvsC_Clothes __instance)
            {
                if (!__instance.isActiveAndEnabled) return;

                RefreshInterface();
            }
        }
    }
}
