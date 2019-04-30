using BepInEx;
using BepInEx.Logging;
using EC.Core.ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using KoiSkinOverlayX;
using OverlayMods;

namespace KoiClothesOverlayX
{
    [BepInPlugin(GUID, "ECCOX (EC ClothesOverlay)", KoiSkinOverlayMgr.Version)]
    [BepInDependency(ExtendedSave.GUID)]
    [BepInDependency(KoikatuAPI.GUID)]
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
            "ct_shoes",
        };

        public static readonly string[] SubClothesNames =
        {
            "ct_top_parts_A",
            "ct_top_parts_B",
            "ct_top_parts_C"
        };

        private static ManualLogSource _logger;

        private void Awake()
        {
            _logger = Logger;
            CharacterApi.RegisterExtraBehaviour<KoiClothesOverlayController>(GUID);
            KoiClothesOverlayController.Hooks.Init(GUID);
        }

        internal static void Log(LogLevel logLevel, object data)
        {
            _logger.Log(logLevel, data);
        }
    }
}
