using BepInEx;
using BepInEx.Logging;
using KKAPI.Chara;
using KoiSkinOverlayX;
using OverlayMods;

namespace KoiClothesOverlayX
{
    [BepInPlugin(GUID, "KCOX (KoiClothesOverlay)", KoiSkinOverlayMgr.Version)]
    [BepInDependency("com.bepis.bepinex.extendedsave")]
    [BepInDependency(KoiSkinOverlayMgr.GUID)]
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
            "ct_shoes_inner",
            "ct_shoes_outer"
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
            KoiClothesOverlayController.Hooks.Init(GUID);
        }

        internal static void Log(LogLevel logLevel, object data)
        {
            Logger.Log(logLevel, data);
        }
    }
}
