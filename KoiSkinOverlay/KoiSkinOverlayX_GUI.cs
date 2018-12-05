/*
 
    Powerful plugins
    counterintuitive interfaces
    I'm in despair
 
 */

using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
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
    [BepInPlugin(KoiSkinOverlayX.GUID +"_GUI", "KoiSkinOverlayX GUI", KoiSkinOverlayX.Version)]
    [BepInDependency(KoiSkinOverlayX.GUID)]
    public class KoiSkinOverlayX_GUI : BaseUnityPlugin
    {
        private const string FileExt = ".png";
        private const string FileFilter = "Overlay images (*.png)|*.png|All files|*.*";
        private MakerAPI.MakerAPI _api;
        private Subject<Texture2D> _bodyTextureChanged;
        private byte[] _bytesToLoad;
        private Subject<Texture2D> _faceTextureChanged;
        private KoiSkinOverlayX _ksox;
        private Exception _lastError;
        private TexType _lastType;

        [Browsable(false)]
        private ConfigWrapper<bool> _removeOldFiles;

        private static string GetUniqueTexDumpFilename()
        {
            var path = Path.Combine(KoiSkinOverlayX.OverlayDirectory, "_Export");
            Directory.CreateDirectory(path);
            var file = Path.Combine(path, DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + FileExt);
            return file;
        }

        private void MakerExiting(object sender, EventArgs e)
        {
            _bodyTextureChanged?.Dispose();
            _faceTextureChanged?.Dispose();
        }

        private void OnFileAccept(string[] strings, TexType type)
        {
            if (strings == null || strings.Length == 0) return;

            var texPath = strings[0];
            if (string.IsNullOrEmpty(texPath)) return;

            _lastType = type;

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
            var makerCategory = new MakerCategory("01_BodyTop", "tglOverlayKSOX",
                MakerConstants.GetBuiltInCategory("01_BodyTop", "tglPaint").Position + 5, "Overlays");

            SetupFaceControls(e, makerCategory);

            e.AddControl(new MakerSeparator(makerCategory, _ksox));

            SetupBodyControls(e, makerCategory);

            e.AddControl(new MakerSeparator(makerCategory, _ksox));

            var t = e.AddControl(new MakerToggle(makerCategory, "Remove overlays imported from BepInEx\\KoiSkinOverlay when saving cards (they are saved inside the card now)", _ksox));
            t.Value = _removeOldFiles.Value;
            t.ValueChanged.Subscribe(b => t.Value = b);
        }

        private void SetupBodyControls(RegisterSubCategoriesEvent e, MakerCategory makerCategory)
        {
            _bodyTextureChanged = new Subject<Texture2D>();
            e.AddControl(new MakerText("Body overlay texture", makerCategory, _ksox));

            var bi = e.AddControl(new MakerImage(null, makerCategory, _ksox) { Height = 150, Width = 150 });
            _bodyTextureChanged.Subscribe(d => bi.Texture = d);

            e.AddControl(new MakerButton("Load new texture", makerCategory, _ksox))
                .OnClick.AddListener(
                    () => OpenFileDialog.Show(strings => OnFileAccept(strings, TexType.Body), "Open overlay image", UserData.Path, FileFilter, FileExt));

            e.AddControl(new MakerButton("Clear texture", makerCategory, _ksox))
                .OnClick.AddListener(
                    () =>
                    {
                        KoiSkinOverlayX.SetTexExtData(MakerAPI.MakerAPI.Instance.CurrentChaFile, null, TexType.Body);
                        KoiSkinOverlayX.UpdateTexture(MakerAPI.MakerAPI.Instance.GetCharacterControl(), TexType.Body);
                        _bodyTextureChanged.OnNext(null);
                    });

            e.AddControl(new MakerButton("Export current texture", makerCategory, _ksox))
                .OnClick.AddListener(
                    () =>
                    {
                        try
                        {
                            var tex = KoiSkinOverlayX.GetTexExtData(MakerAPI.MakerAPI.Instance.CurrentChaFile, TexType.Body);
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

            e.AddControl(new MakerButton("Export template texture", makerCategory, _ksox))
                .OnClick.AddListener(
                    () =>
                    {
                        var filename = GetUniqueTexDumpFilename();
                        File.WriteAllBytes(filename, Resources.body);
                        Util.OpenFileInExplorer(filename);
                    });
        }

        private void SetupFaceControls(RegisterSubCategoriesEvent e, MakerCategory makerCategory)
        {
            _faceTextureChanged = new Subject<Texture2D>();

            e.AddSubCategory(makerCategory);

            e.AddControl(new MakerText("Face overlay texture", makerCategory, _ksox));

            var fi = e.AddControl(new MakerImage(null, makerCategory, _ksox) { Height = 150, Width = 150 });
            _faceTextureChanged.Subscribe(d => fi.Texture = d);

            e.AddControl(new MakerButton("Load new texture", makerCategory, _ksox))
                .OnClick.AddListener(
                    () => OpenFileDialog.Show(strings => OnFileAccept(strings, TexType.Face), "Open overlay image", UserData.Path, FileFilter, FileExt));

            e.AddControl(new MakerButton("Clear texture", makerCategory, _ksox))
                .OnClick.AddListener(
                    () =>
                    {
                        KoiSkinOverlayX.SetTexExtData(MakerAPI.MakerAPI.Instance.CurrentChaFile, null, TexType.Face);
                        KoiSkinOverlayX.UpdateTexture(MakerAPI.MakerAPI.Instance.GetCharacterControl(), TexType.Face);
                        _faceTextureChanged.OnNext(null);
                    });

            e.AddControl(new MakerButton("Export current texture", makerCategory, _ksox))
                .OnClick.AddListener(
                    () =>
                    {
                        try
                        {
                            var tex = KoiSkinOverlayX.GetTexExtData(MakerAPI.MakerAPI.Instance.CurrentChaFile, TexType.Face);
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

            e.AddControl(new MakerButton("Export template texture", makerCategory, _ksox))
                .OnClick.AddListener(
                    () =>
                    {
                        var filename = GetUniqueTexDumpFilename();
                        File.WriteAllBytes(filename, Resources.face);
                        Util.OpenFileInExplorer(filename);
                    });
        }

        private void Start()
        {
            _api = MakerAPI.MakerAPI.Instance;
            _ksox = GetComponent<KoiSkinOverlayX>();
            _removeOldFiles = new ConfigWrapper<bool>("removeOldFiles", _ksox, true);

            _api.RegisterCustomSubCategories += RegisterCustomSubCategories;
            _api.MakerExiting += MakerExiting;
            _api.CharacterChanged += (sender, args) => StartCoroutine(UpdateImages());

            ExtendedSave.CardBeingSaved += ExtendedSaveOnCardBeingSaved;
        }

        private IEnumerator UpdateImages()
        {
            yield return null;
            _bodyTextureChanged.OnNext(KoiSkinOverlayX.GetTexExtData(_api.CurrentChaFile, TexType.Body));
            _faceTextureChanged.OnNext(KoiSkinOverlayX.GetTexExtData(_api.CurrentChaFile, TexType.Face));
        }

        private void ExtendedSaveOnCardBeingSaved(ChaFile chaFile)
        {
            if (_removeOldFiles.Value)
            {
                foreach (var type in new[] { TexType.Face, TexType.Body })
                {
                    var path = KoiSkinOverlayX.GetTexFilename(chaFile.parameter.fullname, type);
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
        }

        private void Update()
        {
            if (_bytesToLoad != null)
            {
                try
                {
                    var tex = Util.TextureFromBytes(_bytesToLoad);
                    switch (_lastType)
                    {
                        case TexType.Body:
                            _bodyTextureChanged?.OnNext(tex);
                            break;
                        case TexType.Face:
                            _faceTextureChanged?.OnNext(tex);
                            break;
                    }
                    KoiSkinOverlayX.SetTexExtData(MakerAPI.MakerAPI.Instance.CurrentChaFile, tex, _lastType);
                    KoiSkinOverlayX.UpdateTexture(MakerAPI.MakerAPI.Instance.GetCharacterControl(), _lastType);

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
    }
}
