using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Utilities;
using KoiSkinOverlayX;
using UniRx;
using UnityEngine;

#if !EC
using KKAPI.Studio;
using KKAPI.Studio.UI;
#endif

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

        internal static void RefreshInterface(string category = null)
        {
            if (!MakerAPI.InsideMaker || _refreshInterfaceRunning || _refreshInterface == null) return;

            _refreshInterfaceRunning = true;
            _instance.StartCoroutine(RefreshInterfaceCo(category));
        }

        internal static void RefreshMenuColors(int parts)
        {
            if (!MakerAPI.InsideMaker) return;

#if KK || KKS || EC
            var makerBase = MakerAPI.GetMakerBase();
            if (makerBase != null)
            {
                var components = makerBase.GetComponentInChildren<ChaCustom.CustomChangeMainMenu>(true).ccClothesMenu.cvsClothes;
                foreach (var component in components)
                    component.ChangeUseColorVisible();
            }
#endif
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
            var owner = this;

            _textureChanged = new Subject<KeyValuePair<string, Texture2D>>();
            _refreshInterface = new Subject<string>();

            var loadToggle = e.AddLoadToggle(new MakerLoadToggle("Clothes overlays"));
            loadToggle.ValueChanged.Subscribe(newValue => GetControllerRegistration().MaintainState = !newValue);

            var coordLoadToggle = e.AddCoordinateLoadToggle(new MakerCoordinateLoadToggle("Clothes overlays"));
            coordLoadToggle.ValueChanged.Subscribe(newValue => GetControllerRegistration().MaintainCoordinateState = !newValue);

#if KK || KKS || EC
            var makerCategory = MakerConstants.GetBuiltInCategory("03_ClothesTop", "tglTop");

            // Either the 3 subs will be visible or the one main. 1st separator is made by the API
            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.SubClothesNames[0], "Overlay textures (Piece 1)");
            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.SubClothesNames[0], "Color mask (Piece 1)", true, KoiClothesOverlayController.MakeColormaskId(KoiClothesOverlayMgr.SubClothesNames[0]));
            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.SubClothesNames[1], "Overlay textures (Piece 2)", true);
            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.SubClothesNames[1], "Color mask (Piece 2)", true, KoiClothesOverlayController.MakeColormaskId(KoiClothesOverlayMgr.SubClothesNames[1]));
            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.SubClothesNames[2], "Overlay textures (Piece 3)", true);
            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.SubClothesNames[2], "Color mask (Piece 3)", true, KoiClothesOverlayController.MakeColormaskId(KoiClothesOverlayMgr.SubClothesNames[2]));

            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.MainClothesNames[0]);
            SetupTexControls(e, makerCategory, owner, KoiClothesOverlayMgr.MainClothesNames[0], "Color mask", true, KoiClothesOverlayController.MakeColormaskId(KoiClothesOverlayMgr.MainClothesNames[0]));

            SetupTexControls(e, makerCategory, owner, MaskKind.BodyMask.ToString(), "Body alpha mask", true);
            SetupTexControls(e, makerCategory, owner, MaskKind.InnerMask.ToString(), "Inner clothes alpha mask", true);
            SetupTexControls(e, makerCategory, owner, MaskKind.BraMask.ToString(), "Bra alpha mask", true);

            var cats = new[]
            {
                new KeyValuePair<string, string>("tglBot", "ct_clothesBot"),
                new KeyValuePair<string, string>("tglBra", "ct_bra"),
                new KeyValuePair<string, string>("tglShorts", "ct_shorts"),
                new KeyValuePair<string, string>("tglGloves", "ct_gloves"),
                new KeyValuePair<string, string>("tglPanst", "ct_panst"),
                new KeyValuePair<string, string>("tglSocks", "ct_socks"),
#if KK 
                new KeyValuePair<string, string>("tglInnerShoes", "ct_shoes_inner"),
                new KeyValuePair<string, string>("tglOuterShoes", "ct_shoes_outer")
#elif EC
                new KeyValuePair<string, string>("tglShoes", "ct_shoes"),
#elif KKS
                new KeyValuePair<string, string>("tglOuterShoes", "ct_shoes_outer")
#endif
            };

            for (var index = 0; index < cats.Length; index++)
            {
                var pair = cats[index];
                var cat = MakerConstants.GetBuiltInCategory("03_ClothesTop", pair.Key);
                SetupTexControls(e, cat, owner, pair.Value);
#if KK
                SetupTexControls(e, cat, owner, pair.Value, "Color mask", true, KoiClothesOverlayController.MakeColormaskId(pair.Value));
#else
                var kind = index == 6 ? index + 2 : index + 1;
                SetupTexControls(e, cat, owner, pair.Value, "Color mask", true, KoiClothesOverlayController.MakeColormaskId(pair.Value));
#endif
            }

#else
            var cat = new MakerCategory(MakerConstants.Clothes.CategoryName, "Clothes Overlays");
            e.AddSubCategory(cat);
            var cats = new[]
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

            for (var index = 0; index < cats.Length; index++)
            {
                var pair = cats[index];
                SetupTexControls(e, cat, owner, pair.Value, pair.Key, index != 0);
                SetupTexControls(e, cat, owner, pair.Value, "Color mask", true, KoiClothesOverlayController.MakeColormaskId(pair.Value));
            }
#endif

#if KK || KKS
            GetOverlayController().CurrentCoordinate.Subscribe(type => RefreshInterface());
#endif
        }

        private void SetupTexControls(RegisterCustomControlsEvent e, MakerCategory makerCategory, BaseUnityPlugin owner, string clothesId, string title = "Overlay textures", bool addSeparator = false, string colormaskId = null)
        {
            var isMask = KoiClothesOverlayController.IsMaskKind(clothesId); // todo false in ai hs
            var texType = isMask ? "override texture" : "overlay texture";
            var isColorMask = colormaskId != null;
            texType = isColorMask ? "override texture" : texType;

            var realClothesId = clothesId;
            clothesId = !isColorMask ? clothesId : colormaskId;

            var controlSeparator = addSeparator ? e.AddControl(new MakerSeparator(makerCategory, owner)) : null;

            var controlTitle = e.AddControl(new MakerText(title, makerCategory, owner));

            var controlGen = e.AddControl(new MakerButton("Dump original texture", makerCategory, owner));
            controlGen.OnClick.AddListener(() => GetOverlayController().DumpBaseTexture(clothesId, b => KoiSkinOverlayGui.WriteAndOpenPng(b, clothesId + "_Original")));

            var controlImage = e.AddControl(new MakerImage(null, makerCategory, owner) { Height = 150, Width = 150 });

            MakerToggle controlOverride = null;
            if (!isMask && !isColorMask)
            {
                controlOverride = e.AddControl(new MakerToggle(makerCategory, "Hide base texture", owner));
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
            }

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

                        KoiSkinOverlayGui.WriteAndOpenPng(tex, clothesId);
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
                    if (cat != null && cat != realClothesId) return;
                    if (!controlImage.Exists) return;

                    var ctrl = GetOverlayController();

                    var renderer = ctrl?.GetApplicableRenderers(realClothesId)?.FirstOrDefault();
                    var visible = renderer?.material?.mainTexture != null;

                    controlTitle.Visible.OnNext(visible);
                    controlGen.Visible.OnNext(visible);
                    controlImage.Visible.OnNext(visible);
                    controlOverride?.Visible.OnNext(visible);
                    controlLoad.Visible.OnNext(visible);
                    controlClear.Visible.OnNext(visible);
                    controlExport.Visible.OnNext(visible);
                    controlSeparator?.Visible.OnNext(visible);

                    _textureChanged.OnNext(new KeyValuePair<string, Texture2D>(realClothesId, ctrl?.GetOverlayTex(realClothesId, false)?.Texture));
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

#if !EC
            if (StudioAPI.InsideStudio)
            {
                enabled = false;
                var cat = StudioAPI.GetOrCreateCurrentStateCategory("Overlays");
                cat.AddControl(new CurrentStateCategorySwitch("Clothes overlays",
                    c => c.charInfo.GetComponent<KoiClothesOverlayController>().EnableInStudio)).Value.Subscribe(
                    v => StudioAPI.GetSelectedControllers<KoiClothesOverlayController>().Do(c =>
                    {
                        if (c.EnableInStudio != v)
                        {
                            c.EnableInStudio = v;
                            c.RefreshAllTextures();
                        }
                    }));
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
#if KK || KKS || EC
                    var isMask = KoiClothesOverlayController.IsMaskKind(_typeToLoad);

                    // Always save to the card in lossless format
                    var textureFormat = isMask ? TextureFormat.RG16 : TextureFormat.ARGB32;
                    var tex = Util.TextureFromBytes(_bytesToLoad, textureFormat);

                    var controller = GetOverlayController();

                    Texture origTex = null;
                    if (isMask)
                        origTex = controller.GetOriginalMask((MaskKind)Enum.Parse(typeof(MaskKind), _typeToLoad));
                    else if (KoiClothesOverlayController.IsColormask(_typeToLoad))
                        origTex = controller.GetOriginalColormask(_typeToLoad);
                    else
                        origTex = controller.GetApplicableRenderers(_typeToLoad).First().material.mainTexture;

                    var isWrongRes = origTex != null && (isMask ? tex.width > origTex.width || tex.height > origTex.height : tex.width != origTex.width || tex.height != origTex.height);
#else
                    // Always save to the card in lossless format
                    var textureFormat = TextureFormat.ARGB32;
                    var tex = Util.TextureFromBytes(_bytesToLoad, textureFormat);

                    var controller = GetOverlayController();
                    var origTex = KoiClothesOverlayController.IsColormask(_typeToLoad) ? controller.GetOriginalColormask(_typeToLoad) : controller.GetApplicableRenderers(_typeToLoad).First().material.mainTexture;

                    var isWrongRes = origTex != null && tex.width != origTex.width || tex.height != origTex.height;
#endif
                    if (isWrongRes)
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
