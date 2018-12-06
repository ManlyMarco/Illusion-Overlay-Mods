/*
 
    Powerful plugins
    with unintuitive interfaces
    left me in despair
 
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using MakerAPI;
using MakerAPI.Utilities;
using UniRx;
using UnityEngine;
using Logger = BepInEx.Logger;
using Resources = KoiSkinOverlayX.Properties.Resources;

namespace KoiSkinOverlayX
{
    [BepInProcess("Koikatu")]
    [BepInPlugin(KoiSkinOverlayMgr.GUID + "_GUI", "KSOX GUI", KoiSkinOverlayMgr.Version)]
    [BepInDependency(KoiSkinOverlayMgr.GUID)]
    public class KoiSkinOverlayGui : BaseUnityPlugin
    {
        private const string FileExt = ".png";
        private const string FileFilter = "Overlay images (*.png)|*.png|All files|*.*";
        private MakerAPI.MakerAPI _api;
        private byte[] _bytesToLoad;
        private Exception _lastError;

        [Browsable(false)]
        private ConfigWrapper<bool> _removeOldFiles;

        private Subject<KeyValuePair<TexType, Texture2D>> _textureChanged;
        private TexType _typeToLoad;

        private void ExtendedSaveOnCardBeingSaved(ChaFile chaFile)
        {
            var ctrl = GetOverlayController();

            KoiSkinOverlayMgr.SaveAllOverlayTextures(ctrl, chaFile);

            if (_removeOldFiles.Value)
            {
                foreach (var overlay in ctrl.Overlays)
                {
                    var path = KoiSkinOverlayMgr.GetTexFilename(chaFile.parameter.fullname, overlay.Key);
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
        }

        private static KoiSkinOverlayController GetOverlayController()
        {
            return MakerAPI.MakerAPI.Instance.GetCharacterControl().gameObject.GetComponent<KoiSkinOverlayController>();
        }

        private static string GetUniqueTexDumpFilename()
        {
            var path = Path.Combine(KoiSkinOverlayMgr.OverlayDirectory, "_Export");
            Directory.CreateDirectory(path);
            var file = Path.Combine(path, DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + FileExt);
            return file;
        }

        private void MakerExiting(object sender, EventArgs e)
        {
            _textureChanged?.Dispose();
        }

        private void OnFileAccept(string[] strings, TexType type)
        {
            if (strings == null || strings.Length == 0) return;

            var texPath = strings[0];
            if (string.IsNullOrEmpty(texPath)) return;

            _typeToLoad = type;

            try
            {
                _bytesToLoad = File.ReadAllBytes(texPath);
            }
            catch (Exception ex)
            {
                _bytesToLoad = null;
                _lastError = ex;
            }
        }

        private void RegisterCustomSubCategories(object sender, RegisterSubCategoriesEvent e)
        {
            var owner = GetComponent<KoiSkinOverlayMgr>();
            _textureChanged = new Subject<KeyValuePair<TexType, Texture2D>>();

            var makerCategory = new MakerCategory("01_BodyTop", "tglOverlayKSOX",
                MakerConstants.GetBuiltInCategory("01_BodyTop", "tglPaint").Position + 5, "Overlays");
            e.AddSubCategory(makerCategory);

            SetupTexControls(e, makerCategory, owner, TexType.FaceOver, "Face overlay texture (on top of everything, true color)");
            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.BodyOver, "Body overlay texture (on top of everything, true color)");
            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.FaceUnder, "Face underlay texture (under tattoos and other overlays)");
            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.BodyUnder, "Body underlay texture (under tattoos and other overlays)");
            e.AddControl(new MakerSeparator(makerCategory, owner));

            var t = e.AddControl(new MakerToggle(makerCategory, "Remove overlays imported from BepInEx\\KoiSkinOverlay when saving cards (they are saved inside the card now and no longer necessary)", owner));
            t.Value = _removeOldFiles.Value;
            t.ValueChanged.Subscribe(b => t.Value = b);
        }

        private void SetTexAndUpdate(Texture2D tex, TexType texType)
        {
            var ctrl = GetOverlayController();
            ctrl.SetOverlayTex(tex, texType);
            ctrl.UpdateTexture(texType);

            _textureChanged.OnNext(new KeyValuePair<TexType, Texture2D>(texType, tex));
        }

        private void SetupTexControls(RegisterCustomControlsEvent e, MakerCategory makerCategory, BaseUnityPlugin owner, TexType texType, string title)
        {
            e.AddControl(new MakerText(title, makerCategory, owner));

            var bi = e.AddControl(new MakerImage(null, makerCategory, owner) {Height = 150, Width = 150});
            _textureChanged.Subscribe(
                d =>
                {
                    if (d.Key == texType)
                        bi.Texture = d.Value;
                });

            e.AddControl(new MakerButton("Load new texture", makerCategory, owner))
                .OnClick.AddListener(
                    () => OpenFileDialog.Show(strings => OnFileAccept(strings, texType), "Open overlay image", UserData.Path, FileFilter, FileExt));

            e.AddControl(new MakerButton("Clear texture", makerCategory, owner))
                .OnClick.AddListener(() => SetTexAndUpdate(null, texType));

            e.AddControl(new MakerButton("Export current texture", makerCategory, owner))
                .OnClick.AddListener(
                    () =>
                    {
                        try
                        {
                            var ctrl = GetOverlayController();
                            var tex = ctrl.Overlays.FirstOrDefault(x => x.Key == texType).Value;
                            if (tex == null) return;
                            var filename = GetUniqueTexDumpFilename();
                            File.WriteAllBytes(filename, tex.EncodeToPNG());
                            Util.OpenFileInExplorer(filename);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Error | LogLevel.Message, "[OverlayX] Failed to export texture - " + ex.Message);
                        }
                    });

            e.AddControl(new MakerButton("Export template texture", makerCategory, owner))
                .OnClick.AddListener(
                    () =>
                    {
                        var filename = GetUniqueTexDumpFilename();
                        switch (texType)
                        {
                            case TexType.BodyOver:
                            case TexType.BodyUnder:
                                File.WriteAllBytes(filename, Resources.body);
                                break;
                            case TexType.FaceOver:
                            case TexType.FaceUnder:
                                File.WriteAllBytes(filename, Resources.face);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(texType), texType, null);
                        }
                        Util.OpenFileInExplorer(filename);
                    });
        }

        private void Start()
        {
            _api = MakerAPI.MakerAPI.Instance;
            _removeOldFiles = new ConfigWrapper<bool>("removeOldFiles", GetComponent<KoiSkinOverlayMgr>(), true);

            _api.RegisterCustomSubCategories += RegisterCustomSubCategories;
            _api.MakerExiting += MakerExiting;
            _api.ChaFileLoaded += (sender, args) => StartCoroutine(UpdateImages());

            ExtendedSave.CardBeingSaved += ExtendedSaveOnCardBeingSaved;
        }

        private void Update()
        {
            if (_bytesToLoad != null)
            {
                try
                {
                    var tex = Util.TextureFromBytes(_bytesToLoad);
                    SetTexAndUpdate(tex, _typeToLoad);

                    Logger.Log(LogLevel.Message, "[OverlayX] Texture imported successfully");
                }
                catch (Exception ex)
                {
                    _lastError = ex;
                }
                _bytesToLoad = null;
            }

            if (_lastError != null)
            {
                Logger.Log(LogLevel.Error | LogLevel.Message, "[OverlayX] Failed to load texture from file - " + _lastError.Message);
                Logger.Log(LogLevel.Debug, _lastError);
                _lastError = null;
            }
        }

        private IEnumerator UpdateImages()
        {
            yield return null;

            var ctrl = GetOverlayController();

            KoiSkinOverlayMgr.LoadAllOverlayTextures(ctrl);

            foreach (var texType in new[] {TexType.BodyOver, TexType.BodyUnder, TexType.FaceOver, TexType.FaceUnder})
            {
                var tex = ctrl.Overlays.FirstOrDefault(x => x.Key == texType).Value;
                _textureChanged.OnNext(new KeyValuePair<TexType, Texture2D>(texType, tex));
            }
        }
    }
}
