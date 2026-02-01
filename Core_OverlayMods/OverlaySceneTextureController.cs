#if !EC
using ExtensibleSaveFormat;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using KoiClothesOverlayX;
using Studio;

namespace KoiSkinOverlayX
{
    public class OverlaySceneTextureController : SceneCustomFunctionController
    {
        public const string Savekey = nameof(OverlaySceneTextureController);

        public static OverlaySceneTextureController Instance { get; private set; }

        protected void Awake()
        {
            Instance = this;
        }

        protected override void OnSceneSave()
        {
            PluginData data = new PluginData { version = KoiSkinOverlayController.SaveVersion };
            TextureSaveHandler.Instance.Save(data, "", null, false);
            if (data.data.Keys.Count > 0)
                SetExtendedData(data);
            else
                SetExtendedData(null);
        }

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            TextureSaveHandler.Instance.Load<object>(null, "", false);
        }

        protected override void OnObjectsCopied(ReadOnlyDictionary<int, ObjectCtrlInfo> copiedItems)
        {
            foreach (var item in copiedItems)
            {
                if (item.Value is OCIChar ociChar)
                {
                    ociChar.charInfo.GetComponent<KoiSkinOverlayController>().DuplicatingFrom = item.Key;
                    ociChar.charInfo.GetComponent<KoiClothesOverlayController>().DuplicatingFrom = item.Key;
                }
            }
        }
    }
}
#endif