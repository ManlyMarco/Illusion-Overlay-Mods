using BepInEx;
using KoiSkinOverlayX;
using MakerAPI.Chara;

namespace KoiClothesOverlayX
{
    [BepInPlugin(GUID, "KCOX (KoiClothesOverlay)", KoiSkinOverlayMgr.Version)]
    [BepInDependency("com.bepis.bepinex.extendedsave")]
    [BepInDependency(MakerAPI.MakerAPI.GUID)]
    [BepInDependency(KoiSkinOverlayMgr.GUID)]
    public class KoiClothesOverlayMgr : BaseUnityPlugin
    {
        public const string GUID = KoiSkinOverlayMgr.GUID + "_Clothes";

        private void Awake()
        {
            CharacterApi.RegisterExtraBehaviour<KoiClothesOverlayController>(GUID);
            KoiClothesOverlayController.Hooks.Init(GUID);
        }
    }
}
