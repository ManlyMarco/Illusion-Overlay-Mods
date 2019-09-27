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
using BepInEx.Configuration;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Studio;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;
using Logger = KoiSkinOverlayX.KoiSkinOverlayMgr;
using Resources = OverlayMods.Properties.Resources;

namespace KoiSkinOverlayX
{
    [BepInPlugin(GUID, "KSOX GUI", KoiSkinOverlayMgr.Version)]
    [BepInDependency(KoiSkinOverlayMgr.GUID)]
    public class KoiSkinOverlayGui : BaseUnityPlugin
    {
        public const string GUID = KoiSkinOverlayMgr.GUID + "_GUI";

        public const string FileExt = ".png";
        public const string FileFilter = "Overlay images (*.png)|*.png|All files|*.*";

        private Subject<KeyValuePair<TexType, Texture2D>> _textureChanged;

        private byte[] _bytesToLoad;
        private Exception _lastError;
        private TexType _typeToLoad;
        private FileSystemWatcher _texChangeWatcher;

        [Browsable(false)]
        public static ConfigEntry<bool> RemoveOldFiles;

        [Browsable(false)]
        public static ConfigEntry<bool> WatchLoadedTexForChanges;

        private static void ExtendedSaveOnCardBeingSaved(ChaFile chaFile)
        {
            if (!MakerAPI.InsideMaker) return;

            if (RemoveOldFiles.Value)
            {
                var ctrl = GetOverlayController();
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
            return MakerAPI.GetCharacterControl().gameObject.GetComponent<KoiSkinOverlayController>();
        }

        private static CharacterApi.ControllerRegistration GetControllerRegistration()
        {
            return CharacterApi.GetRegisteredBehaviour(KoiSkinOverlayMgr.GUID);
        }

        public static string GetUniqueTexDumpFilename()
        {
            var path = KoiSkinOverlayMgr.OverlayDirectory;
            Directory.CreateDirectory(path);
            var file = Path.Combine(path, $"_Export_ {DateTime.Now:yyyy-MM-dd--HH-mm-ss}{FileExt}");
            // Normalize just in case for open in explorer call later
            file = Path.GetFullPath(file);
            return file;
        }

        private void MakerExiting(object sender, EventArgs e)
        {
            _textureChanged?.Dispose();
            _texChangeWatcher?.Dispose();
            _bytesToLoad = null;
            _lastError = null;

            GetControllerRegistration().MaintainState = false;
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
            if (WatchLoadedTexForChanges.Value)
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

            var loadToggle = e.AddLoadToggle(new MakerLoadToggle("Skin/eye overlays"));
            loadToggle.ValueChanged.Subscribe(newValue => GetControllerRegistration().MaintainState = !newValue);

            SetupBodyInterface(e, owner);

            SetupEyeInterface(e, owner);
        }

        private void SetupBodyInterface(RegisterSubCategoriesEvent e, KoiSkinOverlayMgr owner)
        {
            var paintCategory = MakerConstants.Body.Paint;
            var makerCategory = new MakerCategory(paintCategory.CategoryName, "tglOverlayKSOX", paintCategory.Position + 5, "Skin Overlays");
            e.AddSubCategory(makerCategory);

            e.AddControl(new MakerButton("Get face overlay template", makerCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(Resources.face));
            e.AddControl(new MakerButton("Get body overlay template", makerCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(Resources.body));

            AddConfigSettings(e, owner, makerCategory);

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.FaceOver, "Face overlay texture (On top of almost everything)");

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.BodyOver, "Body overlay texture (On top of almost everything)");

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.FaceUnder, "Face underlay texture (Under tattoos, blushes, etc.)");

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.BodyUnder, "Body underlay texture (Under tattoos, blushes, etc.)");
        }

        private void SetupEyeInterface(RegisterSubCategoriesEvent e, KoiSkinOverlayMgr owner)
        {
            var irisCategory = MakerConstants.Face.Iris;
            var eyeCategory = new MakerCategory(irisCategory.CategoryName, "tglEyeOverlayKSOX", irisCategory.Position + 5, "Iris Overlays");
            e.AddSubCategory(eyeCategory);

            e.AddControl(new MakerButton("Get iris overlay template", eyeCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(Resources.eye));

            AddConfigSettings(e, owner, eyeCategory);

            e.AddControl(new MakerSeparator(eyeCategory, owner));

            SetupTexControls(e, eyeCategory, owner, TexType.EyeOver, "Iris overlay texture (On top of original iris)");

            e.AddControl(new MakerSeparator(eyeCategory, owner));

            SetupTexControls(e, eyeCategory, owner, TexType.EyeUnder, "Iris underlay texture (Before coloring and effects)");
        }

        private static void AddConfigSettings(RegisterSubCategoriesEvent e, KoiSkinOverlayMgr owner, MakerCategory makerCategory)
        {
            var tRemove = e.AddControl(new MakerToggle(makerCategory, "Remove overlays imported from BepInEx\\KoiSkinOverlay when saving cards (they are saved inside the card now and no longer necessary)", owner));
            tRemove.Value = RemoveOldFiles.Value;
            tRemove.ValueChanged.Subscribe(b => RemoveOldFiles.Value = b);
            var tWatch = e.AddControl(new MakerToggle(makerCategory, "Watch last loaded texture file for changes", owner));
            tWatch.Value = WatchLoadedTexForChanges.Value;
            tWatch.ValueChanged.Subscribe(b => WatchLoadedTexForChanges.Value = b);
        }

        public static void WriteAndOpenPng(byte[] pngData)
        {
            if (pngData == null) throw new ArgumentNullException(nameof(pngData));
            var filename = GetUniqueTexDumpFilename();
            File.WriteAllBytes(filename, pngData);
            Util.OpenFileInExplorer(filename);
        }

        private void SetTexAndUpdate(byte[] tex, TexType texType)
        {
            var ctrl = GetOverlayController();
            var overlay = ctrl.SetOverlayTex(tex, texType);

            _textureChanged.OnNext(new KeyValuePair<TexType, Texture2D>(texType, overlay?.Texture));
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
                            WriteAndOpenPng(tex.Data);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Error | LogLevel.Message, "[KSOX] Failed to export texture - " + ex.Message);
                        }
                    });
        }

        public static string GetDefaultLoadDir()
        {
            return Directory.Exists(KoiSkinOverlayMgr.OverlayDirectory) ? KoiSkinOverlayMgr.OverlayDirectory : Paths.GameRootPath;
        }

        private void Awake()
        {
            if (StudioAPI.InsideStudio)
                return;

            RemoveOldFiles = Config.AddSetting("Maker", "Remove old files", true);
            WatchLoadedTexForChanges = Config.AddSetting("Maker", "Watch loaded texture for changes", true);
            WatchLoadedTexForChanges.SettingChanged += (sender, args) =>
            {
                if (!WatchLoadedTexForChanges.Value)
                    _texChangeWatcher?.Dispose();
            };
        }

        private void Start()
        {
            if (StudioAPI.InsideStudio)
            {
                enabled = false;
                return;
            }

            MakerAPI.RegisterCustomSubCategories += RegisterCustomSubCategories;
            MakerAPI.MakerExiting += MakerExiting;
            CharacterApi.CharacterReloaded += (sender, args) => OnChaFileLoaded();
            ExtendedSave.CardBeingSaved += ExtendedSaveOnCardBeingSaved;
        }

        private void Update()
        {
            if (_bytesToLoad != null)
            {
                try
                {
                    var tex = Util.TextureFromBytes(_bytesToLoad, TextureFormat.ARGB32);

                    var recommendedSize = GetRecommendedTexSize(_typeToLoad);
                    if (tex.width != tex.height || tex.height != recommendedSize)
                        Logger.Log(LogLevel.Message | LogLevel.Warning, $"[KSOX] WARNING - Unusual texture resolution! It's recommended to use {recommendedSize}x{recommendedSize} for {_typeToLoad}.");
                    else
                        Logger.Log(LogLevel.Message, "[KSOX] Texture imported successfully");

                    SetTexAndUpdate(tex.EncodeToPNG(), _typeToLoad);
                }
                catch (Exception ex)
                {
                    _lastError = ex;
                }
                _bytesToLoad = null;
            }

            if (_lastError != null)
            {
                Logger.Log(LogLevel.Error | LogLevel.Message, "[KSOX] Failed to load texture from file - " + _lastError.Message);
                KoiSkinOverlayMgr.Logger.LogDebug(_lastError);
                _lastError = null;
            }
        }

        private int GetRecommendedTexSize(TexType texType)
        {
            switch (texType)
            {
                case TexType.BodyOver:
                case TexType.BodyUnder:
                    return 2048;
                case TexType.FaceOver:
                case TexType.FaceUnder:
                    return 1024;
                case TexType.EyeUnder:
                case TexType.EyeOver:
                    return 512;
                default:
                    throw new ArgumentOutOfRangeException(nameof(texType), texType, null);
            }
        }

        private void OnChaFileLoaded()
        {
            if (!MakerAPI.InsideMaker) return;

            _texChangeWatcher?.Dispose();

            var ctrl = GetOverlayController();
            UpdateInterface(ctrl);
        }

        private void UpdateInterface(KoiSkinOverlayController ctrl)
        {
            foreach (TexType texType in Enum.GetValues(typeof(TexType)))
            {
                var tex = ctrl.Overlays.FirstOrDefault(x => x.Key == texType).Value;
                _textureChanged.OnNext(new KeyValuePair<TexType, Texture2D>(texType, tex?.Texture));
            }
        }
    }
}
