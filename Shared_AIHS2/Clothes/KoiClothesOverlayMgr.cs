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
            "ct_inner_t",
            "ct_inner_b",
            "ct_gloves",
            "ct_panst",
            "ct_socks",
            "ct_shoes",
        };

        private void Awake()
        {
            CharacterApi.RegisterExtraBehaviour<KoiClothesOverlayController>(GUID);
            KoiClothesOverlayController.Hooks.Init();
        }
    }
}
