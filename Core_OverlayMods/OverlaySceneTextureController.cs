#if !EC
using ExtensibleSaveFormat;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
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
            PluginData data = new PluginData();
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
    }
}
#endif