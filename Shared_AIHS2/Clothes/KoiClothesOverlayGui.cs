using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Utilities;
using KoiSkinOverlayX;
using UniRx;
using UnityEngine;

namespace KoiClothesOverlayX
{
    [BepInPlugin(GUID, "Clothes Overlay Mod GUI", KoiSkinOverlayMgr.Version)]
    [BepInDependency(KoiClothesOverlayMgr.GUID)]
    public partial class KoiClothesOverlayGui : BaseUnityPlugin
    {
        private const string GUID = KoiClothesOverlayMgr.GUID + "_GUI";

        private static MonoBehaviour _instance;

        private Subject<KeyValuePair<string, Texture2D>> _textureChanged;
        private static Subject<string> _refreshInterface;
        private static bool _refreshInterfaceRunning;

        private static FileSystemWatcher _texChangeWatcher;

        private Exception _lastError;
        private byte[] _bytesToLoad;
        private string _typeToLoad;

        private static KoiClothesOverlayController GetOverlayController()
        {
            return MakerAPI.GetCharacterControl().gameObject.GetComponent<KoiClothesOverlayController>();
        }

        private static CharacterApi.ControllerRegistration GetControllerRegistration()
        {
            return CharacterApi.GetRegisteredBehaviour(KoiClothesOverlayMgr.GUID);
        }

        private void SetTexAndUpdate(Texture2D tex, string texType)
        {
            var ctrl = GetOverlayController();
            var t = ctrl.GetOverlayTex(texType, true);
            t.Texture = tex;
            ctrl.RefreshTexture(texType);

            _textureChanged.OnNext(new KeyValuePair<string, Texture2D>(texType, tex));
        }

        private void OnFileAccept(string[] strings, string type)
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
            if (KoiSkinOverlayGui.WatchLoadedTexForChanges?.Value ?? true)
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

        private static void RefreshInterface(string category = null)
        {
            if (!MakerAPI.InsideMaker || _refreshInterfaceRunning || _refreshInterface == null) return;

            _refreshInterfaceRunning = true;
            _instance.StartCoroutine(RefreshInterfaceCo(category));
        }

        private static IEnumerator RefreshInterfaceCo(string category)
        {
            _texChangeWatcher?.Dispose();
            yield return null;
            yield return null;
            _refreshInterface?.OnNext(category);
            _refreshInterfaceRunning = false;
        }

        private void RegisterCustomControls(object sender, RegisterSubCategoriesEvent e)
        {
            var cat = new MakerCategory(MakerConstants.Clothes.CategoryName, "Clothes Overlays");
            e.AddSubCategory(cat);

            _textureChanged = new Subject<KeyValuePair<string, Texture2D>>();
            _refreshInterface = new Subject<string>();

            var loadToggle = e.AddLoadToggle(new MakerLoadToggle("Clothes overlays"));
            loadToggle.ValueChanged.Subscribe(newValue => GetControllerRegistration().MaintainState = !newValue);

            var coordLoadToggle = e.AddCoordinateLoadToggle(new MakerCoordinateLoadToggle("Clothes overlays"));
            coordLoadToggle.ValueChanged.Subscribe(newValue => GetControllerRegistration().MaintainCoordinateState = !newValue);

            var types = new[]
            {
                new KeyValuePair<string, string>("Top", "ct_clothesTop"),
                new KeyValuePair<string, string>("Bottom", "ct_clothesBot"),
                new KeyValuePair<string, string>("Inner Top", "ct_inner_t"),
                new KeyValuePair<string, string>("Inner Bottom", "ct_inner_b"),
                new KeyValuePair<string, string>("Gloves", "ct_gloves"),
                new KeyValuePair<string, string>("Pantyhose", "ct_panst"),
                new KeyValuePair<string, string>("Socks", "ct_socks"),
                new KeyValuePair<string, string>("Shoes", "ct_shoes"),
            };

            for (var index = 0; index < types.Length; index++)
            {
                var pair = types[index];
                SetupTexControls(e, cat, this, pair.Value, pair.Key, index != 0);
            }

            //todo GetOverlayController().CurrentCoordinate.Subscribe(type => RefreshInterface());
        }

        private void SetupTexControls(RegisterCustomControlsEvent e, MakerCategory makerCategory, BaseUnityPlugin owner, string clothesId, string title = "Overlay textures", bool addSeparator = false)
        {
            var texType = "overlay texture";

            var controlSeparator = addSeparator ? e.AddControl(new MakerSeparator(makerCategory, owner)) : null;

            var controlTitle = e.AddControl(new MakerText(title, makerCategory, owner));

            var controlGen = e.AddControl(new MakerButton("Dump original texture", makerCategory, owner));
            controlGen.OnClick.AddListener(() => GetOverlayController().DumpBaseTexture(clothesId, KoiSkinOverlayGui.WriteAndOpenPng));

            var controlImage = e.AddControl(new MakerImage(null, makerCategory, owner) { Height = 150, Width = 150 });

            var controlOverride = e.AddControl(new MakerToggle(makerCategory, "Hide base texture", owner));
            controlOverride.ValueChanged.Subscribe(
                b =>
                {
                    var c = GetOverlayController();
                    if (c != null)
                    {
                        var tex = c.GetOverlayTex(clothesId, true);
                        if (tex.Override != b)
                        {
                            tex.Override = b;
                            c.RefreshTexture(clothesId);
                        }
                    }
                });

            var controlLoad = e.AddControl(new MakerButton("Load new " + texType, makerCategory, owner));
            controlLoad.OnClick.AddListener(
                () => OpenFileDialog.Show(
                    strings => OnFileAccept(strings, clothesId),
                    "Open overlay image",
                    KoiSkinOverlayGui.GetDefaultLoadDir(),
                    KoiSkinOverlayGui.FileFilter,
                    KoiSkinOverlayGui.FileExt));

            var controlClear = e.AddControl(new MakerButton("Clear " + texType, makerCategory, owner));
            controlClear.OnClick.AddListener(() => SetTexAndUpdate(null, clothesId));

            var controlExport = e.AddControl(new MakerButton("Export " + texType, makerCategory, owner));
            controlExport.OnClick.AddListener(
                () =>
                {
                    try
                    {
                        var tex = GetOverlayController().GetOverlayTex(clothesId, false)?.TextureBytes;
                        if (tex == null)
                        {
                            Logger.LogMessage("Nothing to export");
                            return;
                        }

                        KoiSkinOverlayGui.WriteAndOpenPng(tex);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogMessage("Failed to export texture - " + ex.Message);
                    }
                });

            // Refresh logic -----------------------

            _textureChanged.Subscribe(
                d =>
                {
                    if (Equals(clothesId, d.Key))
                    {
                        controlImage.Texture = d.Value;
                        if (controlOverride != null)
                            controlOverride.Value = GetOverlayController().GetOverlayTex(d.Key, false)?.Override ?? false;
                    }
                });

            _refreshInterface.Subscribe(
                cat =>
                {
                    if (cat != null && cat != clothesId) return;
                    if (!controlImage.Exists) return;

                    var ctrl = GetOverlayController();

                    var renderer = ctrl?.GetApplicableRenderers(clothesId)?.FirstOrDefault();
                    var visible = renderer?.material?.mainTexture != null;

                    controlTitle.Visible.OnNext(visible);
                    controlGen.Visible.OnNext(visible);
                    controlImage.Visible.OnNext(visible);
                    controlOverride?.Visible.OnNext(visible);
                    controlLoad.Visible.OnNext(visible);
                    controlClear.Visible.OnNext(visible);
                    controlExport.Visible.OnNext(visible);
                    controlSeparator?.Visible.OnNext(visible);

                    _textureChanged.OnNext(new KeyValuePair<string, Texture2D>(clothesId, ctrl?.GetOverlayTex(clothesId, false)?.Texture));
                }
            );
        }

        private void MakerExiting(object sender, EventArgs e)
        {
            _texChangeWatcher?.Dispose();

            _textureChanged?.Dispose();
            _textureChanged = null;

            _refreshInterface?.Dispose();
            _refreshInterface = null;
            _refreshInterfaceRunning = false;

            _bytesToLoad = null;
            _lastError = null;

            var registration = GetControllerRegistration();
            registration.MaintainState = false;
            registration.MaintainCoordinateState = false;
        }

        private void Start()
        {
            _instance = this;

#if KK
            if (KKAPI.Studio.StudioAPI.InsideStudio)
            {
                enabled = false;
                return;
            }
#endif

            Hooks.Init();

            MakerAPI.RegisterCustomSubCategories += RegisterCustomControls;
            MakerAPI.MakerFinishedLoading += (sender, args) => RefreshInterface();
            MakerAPI.MakerExiting += MakerExiting;
            CharacterApi.CharacterReloaded += (sender, e) => RefreshInterface();
            CharacterApi.CoordinateLoaded += (sender, e) => RefreshInterface();

            if (KoiSkinOverlayGui.WatchLoadedTexForChanges != null)
                KoiSkinOverlayGui.WatchLoadedTexForChanges.SettingChanged += (sender, args) =>
                {
                    if (!KoiSkinOverlayGui.WatchLoadedTexForChanges.Value)
                        _texChangeWatcher?.Dispose();
                };
        }

        private void Update()
        {
            if (_bytesToLoad != null)
            {
                try
                {
                    // Always save to the card in lossless format
                    var textureFormat = TextureFormat.ARGB32;
                    var tex = Util.TextureFromBytes(_bytesToLoad, textureFormat);

                    var controller = GetOverlayController();
                    var origTex = controller.GetApplicableRenderers(_typeToLoad).First().material.mainTexture;

                    if (origTex != null && (tex.width != origTex.width || tex.height != origTex.height))
                        Logger.LogMessage($"WARNING - Wrong texture resolution! It's recommended to use {origTex.width}x{origTex.height} instead.");
                    else
                        Logger.LogMessage("Texture imported successfully");

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
                Logger.LogMessage("Failed to load texture from file - " + _lastError.Message);
                Logger.LogError(_lastError);
                _lastError = null;
            }
        }
    }
}
