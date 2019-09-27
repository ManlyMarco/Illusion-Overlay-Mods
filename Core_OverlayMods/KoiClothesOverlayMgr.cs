using BepInEx;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using KoiSkinOverlayX;
using OverlayMods;

namespace KoiClothesOverlayX
{
    [BepInPlugin(GUID, "Clothes Overlay Mod", KoiSkinOverlayMgr.Version)]
    [BepInDependency(ExtendedSave.GUID)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public class KoiClothesOverlayMgr : BaseUnityPlugin
    {
        public const string GUID = Metadata.GUID_KCOX;

        public static readonly string[] MainClothesNames =
        {
            "ct_clothesTop",
            "ct_clothesBot",
            "ct_bra",
            "ct_shorts",
            "ct_gloves",
            "ct_panst",
            "ct_socks",
#if KK
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

        private void Awake()
        {
            CharacterApi.RegisterExtraBehaviour<KoiClothesOverlayController>(GUID);
            KoiClothesOverlayController.Hooks.Init();
        }
    }
}
