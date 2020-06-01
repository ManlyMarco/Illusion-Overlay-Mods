﻿/*
 
    Powerful plugins
    with unintuitive interfaces
    left me in despair
 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using AIChara;
using BepInEx;
using BepInEx.Configuration;
using ExtensibleSaveFormat;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;

namespace KoiSkinOverlayX
{
    [BepInPlugin(GUID, "Skin Overlay Mod GUI", KoiSkinOverlayMgr.Version)]
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
        public static ConfigEntry<bool> WatchLoadedTexForChanges;

        private static void ExtendedSaveOnCardBeingSaved(ChaFile chaFile)
        {
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
        }

        private void SetupBodyInterface(RegisterSubCategoriesEvent e, KoiSkinOverlayMgr owner)
        {
            var makerCategory = new MakerCategory(MakerConstants.Body.CategoryName, "overlayMod", 11111, "Skin Overlays");
            e.AddSubCategory(makerCategory);

            e.AddControl(new MakerButton("Get face overlay template", makerCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(ResourceUtils.GetEmbeddedResource("face.png")));
            e.AddControl(new MakerButton("Get body overlay template", makerCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(ResourceUtils.GetEmbeddedResource("body.png")));

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

        private static void AddConfigSettings(RegisterSubCategoriesEvent e, KoiSkinOverlayMgr owner, MakerCategory makerCategory)
        {
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
                            Logger.LogMessage("Failed to export texture - " + ex.Message);
                        }
                    });
        }

        public static string GetDefaultLoadDir()
        {
            return Directory.Exists(KoiSkinOverlayMgr.OverlayDirectory) ? KoiSkinOverlayMgr.OverlayDirectory : Paths.GameRootPath;
        }

        private void Awake()
        {
            WatchLoadedTexForChanges = Config.AddSetting("Maker", "Watch loaded texture for changes", true);
            WatchLoadedTexForChanges.SettingChanged += (sender, args) =>
            {
                if (!WatchLoadedTexForChanges.Value)
                    _texChangeWatcher?.Dispose();
            };
        }

        private void Start()
        {
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
                        Logger.LogMessage($"WARNING - Unusual texture resolution! It's recommended to use {recommendedSize}x{recommendedSize} for {_typeToLoad}.");
                    else
                        Logger.LogMessage("Texture imported successfully");

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
                Logger.LogMessage("Failed to load texture from file - " + _lastError.Message);
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
                case TexType.FaceOver:
                case TexType.FaceUnder:
                    return 4096;
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
