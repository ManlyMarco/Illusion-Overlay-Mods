using BepInEx;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using OverlayMods;

namespace KoiClothesOverlayX
{
    [BepInPlugin(GUID, "Clothes Overlay Mod", Version)]
    [BepInDependency(ExtendedSave.GUID)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public class KoiClothesOverlayMgr : BaseUnityPlugin
    {
        public const string GUID = "KCOX";
        public const string Version = Constants.Version;

#if KK || KKS || EC
        public static readonly string[] MainClothesNames =
        {
            "ct_clothesTop",
            "ct_clothesBot",
            "ct_bra",
            "ct_shorts",
            "ct_gloves",
            "ct_panst",
            "ct_socks",
#if KK || KKS
            "ct_shoes_inner",
            "ct_shoes_outer"
#elif EC
            "ct_shoes",
#endif
        };

        public static readonly string[] SubClothesNames =
        {
            "ct_top_parts_A",
            "ct_top_parts_B",
            "ct_top_parts_C"
        };
#elif AI || HS2
        public static readonly string[] MainClothesNames =
        {
            "ct_clothesTop",
            "ct_clothesBot",
            "ct_inner_t",
            "ct_inner_b",
            "ct_gloves",
            "ct_panst",
            "ct_socks",
            "ct_shoes",
        };
#endif

        private void Awake()
        {
            CharacterApi.RegisterExtraBehaviour<KoiClothesOverlayController>(GUID);
            KoiClothesOverlayController.Hooks.Init();
        }
    }
}
