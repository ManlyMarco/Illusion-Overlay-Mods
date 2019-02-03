/*
 
    Powerful plugins
    with unintuitive interfaces
    left me in despair
 
 */

using System;
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
        public const string FileExt = ".png";
        public const string FileFilter = "Overlay images (*.png)|*.png|All files|*.*";
        private static MakerAPI.MakerAPI _api;
        private byte[] _bytesToLoad;
        private Exception _lastError;

        private bool _loadFromLoadedCards;

        [Browsable(false)]
        private static ConfigWrapper<bool> _removeOldFiles;
        [Browsable(false)]
        private static ConfigWrapper<bool> _watchLoadedTexForChanges;

        private Subject<KeyValuePair<TexType, Texture2D>> _textureChanged;
        private TexType _typeToLoad;

        private FileSystemWatcher _texChangeWatcher;

        internal static void ExtendedSaveOnCardBeingSaved(ChaFile chaFile)
        {
            if (!_api.InsideMaker) return;

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
            return _api.GetCharacterControl().gameObject.GetComponent<KoiSkinOverlayController>();
        }

        public static string GetUniqueTexDumpFilename()
        {
            var path = Path.Combine(KoiSkinOverlayMgr.OverlayDirectory, "_Export");
            Directory.CreateDirectory(path);
            var file = Path.Combine(path, DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + FileExt);
            return file;
        }

        private void MakerExiting(object sender, EventArgs e)
        {
            _textureChanged?.Dispose();
            _texChangeWatcher?.Dispose();
            _bytesToLoad = null;
            _lastError = null;
        }

        private void OnFileAccept(string[] strings, TexType type)
        {
            if (strings == null || strings.Length == 0) return;

            var texPath = strings[0];
            if (string.IsNullOrEmpty(texPath)) return;

            _typeToLoad = type;

            void ReadTex(string texturePath)
            {
                try
                {
                    _bytesToLoad = File.ReadAllBytes(texturePath);
                }
                catch (Exception ex)
                {
                    _bytesToLoad = null;
                    _lastError = ex;
                }
            }

            ReadTex(texPath);

            _texChangeWatcher?.Dispose();
            if (_watchLoadedTexForChanges.Value)
            {
                var directory = Path.GetDirectoryName(texPath);
                if (directory != null)
                {
                    _texChangeWatcher = new FileSystemWatcher(directory, Path.GetFileName(texPath));
                    _texChangeWatcher.Changed += (sender, args) =>
                    {
                        if (File.Exists(texPath))
                            ReadTex(texPath);
                    };
                    _texChangeWatcher.Deleted += (sender, args) => _texChangeWatcher?.Dispose();
                    _texChangeWatcher.Error += (sender, args) => _texChangeWatcher?.Dispose();
                    _texChangeWatcher.EnableRaisingEvents = true;
                }
            }
        }

        private void RegisterCustomSubCategories(object sender, RegisterSubCategoriesEvent e)
        {
            var owner = GetComponent<KoiSkinOverlayMgr>();
            _textureChanged = new Subject<KeyValuePair<TexType, Texture2D>>();

            _loadFromLoadedCards = true;
            e.AddLoadToggle(new MakerLoadToggle("Overlays")).ValueChanged.Subscribe(b => _loadFromLoadedCards = b);

            var makerCategory = new MakerCategory("01_BodyTop", "tglOverlayKSOX",
                MakerConstants.GetBuiltInCategory("01_BodyTop", "tglPaint").Position + 5, "Overlays");
            e.AddSubCategory(makerCategory);

            e.AddControl(new MakerButton("Get face overlay template", makerCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(Resources.face));
            e.AddControl(new MakerButton("Get body overlay template", makerCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(Resources.body));

            var tRemove = e.AddControl(new MakerToggle(makerCategory, "Remove overlays imported from BepInEx\\KoiSkinOverlay when saving cards (they are saved inside the card now and no longer necessary)", owner));
            tRemove.Value = _removeOldFiles.Value;
            tRemove.ValueChanged.Subscribe(b => _removeOldFiles.Value = b);
            var tWatch = e.AddControl(new MakerToggle(makerCategory, "Watch last loaded texture file for changes", owner));
            tWatch.Value = _watchLoadedTexForChanges.Value;
            tWatch.ValueChanged.Subscribe(b => _watchLoadedTexForChanges.Value = b);

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.FaceOver, "Face overlay texture (On top of almost everything)");

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.BodyOver, "Body overlay texture (On top of almost everything)");

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.FaceUnder, "Face underlay texture (Under tattoos, blushes, etc.)");

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.BodyUnder, "Body underlay texture (Under tattoos, blushes, etc.)");
        }

        public static void WriteAndOpenPng(byte[] pngData)
        {
            if (pngData == null) throw new ArgumentNullException(nameof(pngData));
            var filename = GetUniqueTexDumpFilename();
            File.WriteAllBytes(filename, pngData);
            Util.OpenFileInExplorer(filename);
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

            var bi = e.AddControl(new MakerImage(null, makerCategory, owner) { Height = 150, Width = 150 });
            _textureChanged.Subscribe(
                d =>
                {
                    if (d.Key == texType)
                        bi.Texture = d.Value;
                });

            e.AddControl(new MakerButton("Load new texture", makerCategory, owner))
                .OnClick.AddListener(
                    () => OpenFileDialog.Show(strings => OnFileAccept(strings, texType), "Open overlay image", GetDefaultLoadDir(), FileFilter, FileExt));

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
                            WriteAndOpenPng(tex.EncodeToPNG());
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Error | LogLevel.Message, "[KSOX] Failed to export texture - " + ex.Message);
                        }
                    });
        }

        public static string GetDefaultLoadDir()
        {
            return File.Exists(KoiSkinOverlayMgr.OverlayDirectory) ? KoiSkinOverlayMgr.OverlayDirectory : Paths.GameRootPath;
        }

        private void Start()
        {
            _api = MakerAPI.MakerAPI.Instance;

            var owner = GetComponent<KoiSkinOverlayMgr>();
            _removeOldFiles = new ConfigWrapper<bool>("removeOldFiles", owner, true);
            _watchLoadedTexForChanges = new ConfigWrapper<bool>("watchLoadedTexForChanges", owner, true);
            _watchLoadedTexForChanges.SettingChanged += (sender, args) =>
            {
                if (!_watchLoadedTexForChanges.Value)
                    _texChangeWatcher?.Dispose();
            };

            _api.RegisterCustomSubCategories += RegisterCustomSubCategories;
            _api.MakerExiting += MakerExiting;
            _api.ChaFileLoaded += OnChaFileLoaded;
            // Needed for starting maker in class roster. There is no ChaFileLoaded event fired
            _api.MakerFinishedLoading += (sender, args) => UpdateInterface(GetOverlayController());

            ExtendedSave.CardBeingSaved += ExtendedSaveOnCardBeingSaved;
        }

        private void Update()
        {
            if (_bytesToLoad != null)
            {
                try
                {
                    var tex = Util.TextureFromBytes(_bytesToLoad);

                    if (tex.width != tex.height || tex.height % 1024 != 0 || tex.height == 0)
                        Logger.Log(LogLevel.Message | LogLevel.Warning, "[KSOX] WARNING - Unusual texture resolution! It's recommended to use 1024x1024 for face and 2048x2048 for body.");
                    else
                        Logger.Log(LogLevel.Message, "[KSOX] Texture imported successfully");

                    SetTexAndUpdate(tex, _typeToLoad);
                }
                catch (Exception ex)
                {
                    _lastError = ex;
                }
                _bytesToLoad = null;
            }

            if (_lastError != null)
            {
                ShowTextureLoadError(_lastError);
                _lastError = null;
            }
        }

        public static void ShowTextureLoadError(Exception texLoadError)
        {
            Logger.Log(LogLevel.Error | LogLevel.Message, "[KSOX] Failed to load texture from file - " + texLoadError.Message);
            Logger.Log(LogLevel.Debug, texLoadError);
        }

        private void OnChaFileLoaded(object sender, ChaFileLoadedEventArgs e)
        {
            _texChangeWatcher?.Dispose();

            var ctrl = GetOverlayController();

            if (_loadFromLoadedCards)
                KoiSkinOverlayMgr.LoadAllOverlayTextures(ctrl);

            UpdateInterface(ctrl);
        }

        private void UpdateInterface(KoiSkinOverlayController ctrl)
        {
            foreach (var texType in new[] { TexType.BodyOver, TexType.BodyUnder, TexType.FaceOver, TexType.FaceUnder })
            {
                var tex = ctrl.Overlays.FirstOrDefault(x => x.Key == texType).Value;
                _textureChanged.OnNext(new KeyValuePair<TexType, Texture2D>(texType, tex));
            }
        }
    }
}
