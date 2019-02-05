using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using KoiSkinOverlayX;
using MakerAPI;
using MakerAPI.Utilities;
using UniRx;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace KoiClothesOverlayX
{
    [BepInProcess("Koikatu")]
    [BepInPlugin(GUID, "KCOX GUI", KoiSkinOverlayMgr.Version)]
    [BepInDependency(KoiClothesOverlayMgr.GUID)]
    [BepInDependency(KoiSkinOverlayGui.GUID)]
    public partial class KoiClothesOverlayGui : BaseUnityPlugin
    {
        private const string GUID = KoiClothesOverlayMgr.GUID + "_GUI";

        private static MakerAPI.MakerAPI _api;

        private static MakerLoadToggle _makerLoadToggle;
        internal static bool MakerLoadFromCharas => _makerLoadToggle == null || _makerLoadToggle.Value;

        private Subject<KeyValuePair<string, ClothesTexData>> _textureChanged;
        private static Subject<int> _refreshInterface;
        private static bool _refreshInterfaceRunning;

        private static FileSystemWatcher _texChangeWatcher;

        private Exception _lastError;
        private bool _hideMainToLoad;
        private byte[] _bytesToLoad;
        private string _typeToLoad;

        private static KoiClothesOverlayController GetOverlayController()
        {
            return _api.GetCharacterControl().gameObject.GetComponent<KoiClothesOverlayController>();
        }

        private void SetTexAndUpdate(ClothesTexData tex, string texType)
        {
            GetOverlayController().SetOverlayTex(tex, texType);

            _textureChanged.OnNext(new KeyValuePair<string, ClothesTexData>(texType, tex));
        }

        private void OnFileAccept(string[] strings, string type, bool hideMain)
        {
            _hideMainToLoad = hideMain;
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

        private static void RefreshInterface(int category)
        {
            if (_refreshInterfaceRunning || _refreshInterface == null) return;

            _refreshInterfaceRunning = true;
            _api.StartCoroutine(RefreshInterfaceCo(category));
        }

        private static IEnumerator RefreshInterfaceCo(int category)
        {
            _texChangeWatcher?.Dispose();
            yield return null;
            _refreshInterface?.OnNext(category);
            _refreshInterfaceRunning = false;
        }

        private void RegisterCustomControls(object sender, RegisterCustomControlsEvent e)
        {
            var owner = this;
            _textureChanged = new Subject<KeyValuePair<string, ClothesTexData>>();
            _refreshInterface = new Subject<int>();

            _makerLoadToggle = e.AddLoadToggle(new MakerLoadToggle("Clothes overlays"));

            var makerCategory = MakerConstants.GetBuiltInCategory("03_ClothesTop", "tglTop");

            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.SubClothesNames[0], 0, "Overlay textures (Piece 1)");
            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.SubClothesNames[1], 0, "Overlay textures (Piece 2)", true);
            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.SubClothesNames[2], 0, "Overlay textures (Piece 3)", true);

            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.MainClothesNames[0], 0);

            var cats = new[]
            {
                new KeyValuePair<string, string>("tglBot", "ct_clothesBot"),
                new KeyValuePair<string, string>("tglBra", "ct_bra"),
                new KeyValuePair<string, string>("tglShorts", "ct_shorts"),
                new KeyValuePair<string, string>("tglGloves", "ct_gloves"),
                new KeyValuePair<string, string>("tglPanst", "ct_panst"),
                new KeyValuePair<string, string>("tglSocks", "ct_socks"),
                new KeyValuePair<string, string>("tglInnerShoes", "ct_shoes_inner"),
                new KeyValuePair<string, string>("tglOuterShoes", "ct_shoes_outer")
            };

            for (var index = 0; index < cats.Length; index++)
            {
                var pair = cats[index];
                SetupTexControls(e, MakerConstants.GetBuiltInCategory("03_ClothesTop", pair.Key), owner, pair.Value, index + 1);
            }

            GetOverlayController().CurrentCoordinate.Subscribe(type => RefreshInterface(-1));
        }

        private void SetupTexControls(RegisterCustomControlsEvent e, MakerCategory makerCategory, BaseUnityPlugin owner, string clothesId, int clothesIndex, string title = "Overlay textures", bool addSeparator = false)
        {
            var controlSeparator = addSeparator ? e.AddControl(new MakerSeparator(makerCategory, owner)) : null;

            var controlTitle = e.AddControl(new MakerText(title, makerCategory, owner));

            var controlGen = e.AddControl(new MakerButton("Generate overlay template", makerCategory, owner));
            controlGen.OnClick.AddListener(() => GetOverlayController().DumpBaseTexture(clothesId, KoiSkinOverlayGui.WriteAndOpenPng));

            var controlImage = e.AddControl(new MakerImage(null, makerCategory, owner) { Height = 150, Width = 150 });

            var controlOverride = e.AddControl(new MakerToggle(makerCategory, "Hide base texture", owner));
            controlOverride.ValueChanged.Subscribe(
                b =>
                {
                    var c = GetOverlayController();
                    if (c != null)
                    {
                        var tex = c.GetOverlayTex(clothesId) ?? new ClothesTexData();
                        if (tex.Override != b)
                        {
                            tex.Override = b;
                            SetTexAndUpdate(tex, clothesId);
                        }
                    }
                });

            var controlLoad = e.AddControl(new MakerButton("Load new overlay texture", makerCategory, owner));
            controlLoad.OnClick.AddListener(
                () => OpenFileDialog.Show(
                    strings => OnFileAccept(strings, clothesId, controlOverride.Value),
                    "Open overlay image",
                    KoiSkinOverlayGui.GetDefaultLoadDir(),
                    KoiSkinOverlayGui.FileFilter,
                    KoiSkinOverlayGui.FileExt));

            var controlClear = e.AddControl(new MakerButton("Clear overlay texture", makerCategory, owner));
            controlClear.OnClick.AddListener(() => SetTexAndUpdate(null, clothesId));

            var controlExport = e.AddControl(new MakerButton("Export overlay texture", makerCategory, owner));
            controlExport.OnClick.AddListener(
                () =>
                {
                    try
                    {
                        var tex = controlImage.Texture as Texture2D;
                        if (tex == null)
                        {
                            Logger.Log(LogLevel.Message, "[KCOX] Nothing to export");
                            return;
                        }

                        KoiSkinOverlayGui.WriteAndOpenPng(tex.EncodeToPNG());
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error | LogLevel.Message, "[KCOX] Failed to export texture - " + ex.Message);
                    }
                });

            // Refresh logic -----------------------

            _textureChanged.Subscribe(
                d =>
                {
                    if (Equals(clothesId, d.Key))
                    {
                        controlImage.Texture = d.Value?.Texture;
                        controlOverride.Value = d.Value?.Override ?? false;
                    }
                });

            _refreshInterface.Subscribe(
                cat =>
                {
                    if (cat != clothesIndex && cat >= 0) return;
                    if (!controlImage.Exists) return;

                    var ctrl = GetOverlayController();

                    var renderer = ctrl?.GetApplicableRenderers(clothesId)?.FirstOrDefault();
                    var visible = renderer?.material?.mainTexture != null;

                    controlTitle.Visible.OnNext(visible);
                    controlGen.Visible.OnNext(visible);
                    controlImage.Visible.OnNext(visible);
                    controlOverride.Visible.OnNext(visible);
                    controlLoad.Visible.OnNext(visible);
                    controlClear.Visible.OnNext(visible);
                    controlExport.Visible.OnNext(visible);
                    controlSeparator?.Visible.OnNext(visible);

                    _textureChanged.OnNext(new KeyValuePair<string, ClothesTexData>(clothesId, ctrl?.GetOverlayTex(clothesId)));
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
            _makerLoadToggle = null;
            _lastError = null;
        }

        private void Start()
        {
            _api = MakerAPI.MakerAPI.Instance;
            Hooks.Init();

            _api.MakerBaseLoaded += RegisterCustomControls;
            _api.MakerFinishedLoading += (sender, args) => RefreshInterface(-1);
            _api.MakerExiting += MakerExiting;
            _api.ChaFileLoaded += (sender, e) => RefreshInterface(-1);

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
                    var tex = Util.TextureFromBytes(_bytesToLoad);

                    var origTex = GetOverlayController().GetApplicableRenderers(_typeToLoad).First().material.mainTexture;

                    if (tex.width != origTex.width || tex.height != origTex.height)
                        Logger.Log(LogLevel.Message | LogLevel.Warning, $"[KCOX] WARNING - Wrong texture resolution! It's recommended to use {origTex.width}x{origTex.height} instead.");
                    else
                        Logger.Log(LogLevel.Message, "[KCOX] Texture imported successfully");

                    SetTexAndUpdate(new ClothesTexData { Override = _hideMainToLoad, Texture = tex }, _typeToLoad);
                }
                catch (Exception ex)
                {
                    _lastError = ex;
                }
                _bytesToLoad = null;
            }

            if (_lastError != null)
            {
                Logger.Log(LogLevel.Error | LogLevel.Message, "[KCOX] Failed to load texture from file - " + _lastError.Message);
                Logger.Log(LogLevel.Debug, _lastError);
                _lastError = null;
            }
        }
    }
}
