using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
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
    [BepInPlugin(GUID, "KCOX (KoiClothesOverlay) GUI", KoiSkinOverlayMgr.Version)]
    [BepInDependency(KoiClothesOverlayMgr.GUID)]
    public partial class KoiClothesOverlayGui : BaseUnityPlugin
    {
        private const string GUID = KoiClothesOverlayMgr.GUID + "_GUI";

        private static MakerAPI.MakerAPI _api;

        private static MakerLoadToggle _makerLoadToggle;
        internal static bool MakerLoadFromCharas => _makerLoadToggle == null || _makerLoadToggle.Value;

        private Subject<KeyValuePair<ClothesTexId, ClothesTexData>> _textureChanged;
        private static Subject<int> _refreshInterface;
        private static bool _refreshInterfaceRunning;

        private Exception _lastError;
        private bool _hideMainToLoad;
        private byte[] _bytesToLoad;
        private ClothesTexId _typeToLoad;

        [DisplayName("Advanced mode")]
        [Description("Show additional texture slots for each clothing item")]
        public ConfigWrapper<bool> AdvancedMode { get; private set; }

        private static KoiClothesOverlayController GetOverlayController()
        {
            return _api.GetCharacterControl().gameObject.GetComponent<KoiClothesOverlayController>();
        }

        private void SetTexAndUpdate(ClothesTexData tex, ClothesTexId texType)
        {
            GetOverlayController().SetOverlayTex(tex, texType);

            _textureChanged.OnNext(new KeyValuePair<ClothesTexId, ClothesTexData>(texType, tex));
        }

        private void OnFileAccept(string[] strings, ClothesTexId type, bool hideMain)
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
        }

        private static void RefreshInterface(int category)
        {
            if (_refreshInterfaceRunning || _refreshInterface == null) return;

            _refreshInterfaceRunning = true;
            _api.StartCoroutine(RefreshInterfaceCo(category));
        }

        private static IEnumerator RefreshInterfaceCo(int category)
        {
            yield return null;
            _refreshInterface?.OnNext(category);
            _refreshInterfaceRunning = false;
        }

        private void RegisterCustomControls(object sender, RegisterCustomControlsEvent e)
        {
            var owner = this;
            _textureChanged = new Subject<KeyValuePair<ClothesTexId, ClothesTexData>>();
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

            var rCat = e.AddControl(new MakerRadioButtons(makerCategory, owner, "Renderer group", "Normal 1", "Normal 2", "Alpha 1", "Alpha 2"));
            var rNum = e.AddControl(new MakerRadioButtons(makerCategory, owner, "Renderer number", "1", "2", "3", "4", "5", "6"));

            ClothesTexId GetSelectedId()
            {
                return new ClothesTexId(clothesId, (ClothesRendererGroup) rCat.Value, rNum.Value);
            }

            var refreshing = false;

            void OnSelectionChange(int _)
            {
                if (refreshing) return;

                var id = GetSelectedId();
                _textureChanged.OnNext(new KeyValuePair<ClothesTexId, ClothesTexData>(id, GetOverlayController()?.GetOverlayTex(id)));
            }

            rCat.ValueChanged.Subscribe(OnSelectionChange);
            rNum.ValueChanged.Subscribe(OnSelectionChange);

            var controlGen = e.AddControl(new MakerButton("Generate overlay template", makerCategory, owner));
            controlGen.OnClick.AddListener(() => GetOverlayController().DumpBaseTexture(GetSelectedId(), KoiSkinOverlayGui.WriteAndOpenPng));

            var controlImage = e.AddControl(new MakerImage(null, makerCategory, owner) {Height = 150, Width = 150});

            var controlOverride = e.AddControl(new MakerToggle(makerCategory, "Hide base texture", owner));
            controlOverride.ValueChanged.Subscribe(
                b =>
                {
                    var c = GetOverlayController();
                    if (c != null)
                    {
                        var tex = c.GetOverlayTex(GetSelectedId()) ?? new ClothesTexData();
                        if (tex.Override != b)
                        {
                            tex.Override = b;
                            SetTexAndUpdate(tex, GetSelectedId());
                        }
                    }
                });

            var controlLoad = e.AddControl(new MakerButton("Load new overlay texture", makerCategory, owner));
            controlLoad.OnClick.AddListener(
                () => OpenFileDialog.Show(
                    strings => OnFileAccept(strings, GetSelectedId(), controlOverride.Value),
                    "Open overlay image",
                    KoiSkinOverlayGui.GetDefaultLoadDir(),
                    KoiSkinOverlayGui.FileFilter,
                    KoiSkinOverlayGui.FileExt));

            var controlClear = e.AddControl(new MakerButton("Clear overlay texture", makerCategory, owner));
            controlClear.OnClick.AddListener(() => SetTexAndUpdate(null, GetSelectedId()));

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
                    if (Equals(GetSelectedId(), d.Key))
                    {
                        controlImage.Texture = d.Value?.Texture;
                        controlOverride.Value = d.Value?.Override ?? false;
                    }
                });

            void RefreshControls(int categoryNum)
            {
                if (!rNum.Exists) return;

                var any = false;

                var ctrl = GetOverlayController();
                var clothes = ctrl.GetCustomClothesComponent(clothesId);

                var renderers = clothes == null ? null : KoiClothesOverlayController.GetRendererArrays(clothes).ElementAtOrDefault(categoryNum);

                if (AdvancedMode.Value)
                {
                    if (rCat.Visible.Value)
                    {
                        rNum.Visible.OnNext(true);

                        var resetSelection = false;
                        var anyCats = rCat.Buttons.Any(x => x.gameObject.activeSelf);
                        for (var i = 0; i < rNum.Buttons.Count; i++)
                        {
                            var visible = anyCats && renderers != null && i < renderers.Length;
                            rNum.Buttons[i].gameObject.SetActive(visible);

                            if (!visible && rNum.Buttons[i].isOn)
                                resetSelection = true;
                            if (visible)
                                any = true;
                        }

                        if (resetSelection || rNum.Buttons.All(x => !x.isOn))
                        {
                            var toggle = rNum.Buttons.TakeWhile(x => !x.gameObject.activeSelf).Count();
                            rNum.Value = toggle;
                        }
                    }
                    else
                        rNum.Visible.OnNext(false);
                }
                else
                {
                    rNum.Visible.OnNext(false);
                    rNum.Value = 0;
                    any = renderers != null && renderers.Length > 0 && renderers[0] != null;
                }

                controlTitle.Visible.OnNext(any);
                controlGen.Visible.OnNext(any);
                controlImage.Visible.OnNext(any);
                controlOverride.Visible.OnNext(any);
                controlLoad.Visible.OnNext(any);
                controlClear.Visible.OnNext(any);
                controlExport.Visible.OnNext(any);
                controlSeparator?.Visible.OnNext(any);
            }

            _refreshInterface.Subscribe(
                cat =>
                {
                    if (cat != clothesIndex && cat >= 0) return;
                    if (!rCat.Exists) return;

                    var ctrl = GetOverlayController();

                    refreshing = true;
                    try
                    {
                        if (!AdvancedMode.Value)
                        {
                            rCat.Visible.OnNext(false);
                            rCat.Value = 0;
                            return;
                        }

                        var clothes = ctrl?.GetCustomClothesComponent(clothesId);
                        var resetSelection = false;

                        if (clothes != null)
                        {
                            rCat.Visible.OnNext(true);

                            var rendererArrays = KoiClothesOverlayController.GetRendererArrays(clothes);

                            for (var i = 0; i < rendererArrays.Length; i++)
                            {
                                var renderers = rendererArrays[i];
                                var any = renderers.Length > 0;
                                rCat.Buttons[i].gameObject.SetActive(any);
                                if (!any && rCat.Buttons[i].isOn)
                                    resetSelection = true;
                            }
                        }
                        else
                            rCat.Visible.OnNext(false);

                        if (resetSelection)
                        {
                            var toggle = rCat.Buttons.TakeWhile(x => !x.gameObject.activeSelf).Count();
                            rCat.Value = toggle;
                        }
                        else
                            RefreshControls(rCat.Value);
                    }
                    finally
                    {
                        refreshing = false;
                        var id = GetSelectedId();
                        _textureChanged.OnNext(new KeyValuePair<ClothesTexId, ClothesTexData>(id, ctrl?.GetOverlayTex(id)));
                    }
                });

            rCat.ValueChanged.Subscribe(RefreshControls);
        }

        private void MakerExiting(object sender, EventArgs e)
        {
            _textureChanged?.Dispose();
            _textureChanged = null;
            _refreshInterface?.Dispose();
            _refreshInterface = null;
            _bytesToLoad = null;
            _lastError = null;
            _refreshInterfaceRunning = false;
            _makerLoadToggle = null;
        }

        private void Start()
        {
            AdvancedMode = new ConfigWrapper<bool>(nameof(AdvancedMode), this);
            _api = MakerAPI.MakerAPI.Instance;
            Hooks.Init();

            _api.MakerBaseLoaded += RegisterCustomControls;
            _api.MakerFinishedLoading += (sender, args) => RefreshInterface(-1);
            _api.MakerExiting += MakerExiting;
            _api.ChaFileLoaded += (sender, e) => RefreshInterface(-1);
        }

        private void Update()
        {
            if (_bytesToLoad != null)
            {
                try
                {
                    var tex = Util.TextureFromBytes(_bytesToLoad);

                    var origTex = GetOverlayController().GetRenderer(_typeToLoad).material.mainTexture;

                    if (tex.width != origTex.width || tex.height != origTex.height)
                        Logger.Log(LogLevel.Message | LogLevel.Warning, $"[KCOX] WARNING - Wrong texture resolution! It's recommended to use {origTex.width}x{origTex.height} instead.");
                    else
                        Logger.Log(LogLevel.Message, "[KCOX] Texture imported successfully");

                    SetTexAndUpdate(new ClothesTexData {Override = _hideMainToLoad, Texture = tex}, _typeToLoad);
                }
                catch (Exception ex)
                {
                    _lastError = ex;
                }
                _bytesToLoad = null;
            }

            if (_lastError != null)
            {
                KoiSkinOverlayGui.ShowTextureLoadError(_lastError);
                _lastError = null;
            }
        }
    }
}
