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
using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;
#if AI || HS2
using AIChara;
#endif
#if !EC
using KKAPI.Studio;
using KKAPI.Studio.UI;
#endif

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

        private static MakerToggle[] _tPerCoord = new MakerToggle[2];
        private byte[] _bytesToLoad;
        private Exception _lastError;
        private TexType _typeToLoad;
        private FileSystemWatcher _texChangeWatcher;

        [Browsable(false)]
        public static ConfigEntry<bool> WatchLoadedTexForChanges;

        private static KoiSkinOverlayController GetOverlayController()
        {
            return MakerAPI.GetCharacterControl().gameObject.GetComponent<KoiSkinOverlayController>();
        }

        private static CharacterApi.ControllerRegistration GetControllerRegistration()
        {
            return CharacterApi.GetRegisteredBehaviour(KoiSkinOverlayMgr.GUID);
        }

        public static string GetUniqueTexDumpFilename(string dumpType)
        {
            var path = KoiSkinOverlayMgr.OverlayDirectory;
            Directory.CreateDirectory(path);
            var file = Path.Combine(path, $"_Export_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}_{dumpType}{FileExt}");
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

#if KK || KKS 
            var ctrl = MakerAPI.GetCharacterControl().GetComponent<KoiSkinOverlayController>();
            ctrl.CurrentCoordinate.Subscribe(_ => UpdateInterface(ctrl));
#endif
        }

        private void SetupBodyInterface(RegisterSubCategoriesEvent e, KoiSkinOverlayMgr owner)
        {
#if KK || KKS || EC
            var paintCategory = MakerConstants.Body.Paint;
            var makerCategory = new MakerCategory(paintCategory.CategoryName, "tglOverlayKSOX", paintCategory.Position + 5, "Skin Overlays");
#else
            var makerCategory = new MakerCategory(MakerConstants.Body.CategoryName, "overlayMod", 11111, "Skin Overlays");
#endif
            e.AddSubCategory(makerCategory);

            e.AddControl(new MakerButton("Get face overlay template", makerCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(ResourceUtils.GetEmbeddedResource("face.png"), "Face template"));
            e.AddControl(new MakerButton("Get body overlay template", makerCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(ResourceUtils.GetEmbeddedResource("body.png"), "Body template"));

            AddConfigSettings(e, owner, makerCategory, 0);

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
#if KK || KKS || EC
            var irisCategory = MakerConstants.Face.Iris;
            var eyeCategory = new MakerCategory(irisCategory.CategoryName, "tglEyeOverlayKSOX", irisCategory.Position + 5, "Eye Overlays");
            const string irisTemplateName = "Get iris template";
            const string eyelinerTemplateName = "Get eyeliner/eyebrow template";
#else
            var eyeCategory = new MakerCategory(MakerConstants.Face.CategoryName, "overlayModIris", 11111, "Eye Overlays");
            const string irisTemplateName = "Get iris underlay template";
            const string eyelinerTemplateName = "Get eyelashes/eyeliner template";
#endif

            e.AddSubCategory(eyeCategory);

            e.AddControl(new MakerButton(irisTemplateName, eyeCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(ResourceUtils.GetEmbeddedResource("eye.png"), "Iris template"));

            e.AddControl(new MakerButton(eyelinerTemplateName, eyeCategory, owner))
                .OnClick.AddListener(() => WriteAndOpenPng(ResourceUtils.GetEmbeddedResource("eyeline.png"), "Eyeliner template"));

            AddConfigSettings(e, owner, eyeCategory, 1);

            e.AddControl(new MakerSeparator(eyeCategory, owner));

            SetupTexControls(e, eyeCategory, owner, TexType.EyeOver, "Iris overlay texture (On top of original iris)");

            e.AddControl(new MakerSeparator(eyeCategory, owner));

            SetupTexControls(e, eyeCategory, owner, TexType.EyeUnder, "Iris underlay texture (Before coloring and effects)");

            e.AddControl(new MakerSeparator(eyeCategory, owner));

            SetupTexControls(e, eyeCategory, owner, TexType.EyelineUnder, "Eyelashes override texture (Before coloring and effects)");

            e.AddControl(new MakerSeparator(eyeCategory, owner));

            SetupTexControls(e, eyeCategory, owner, TexType.EyebrowUnder, "Eyebrow override texture (Before coloring and effects)");
        }

        private static void AddConfigSettings(RegisterSubCategoriesEvent e, KoiSkinOverlayMgr owner, MakerCategory makerCategory, int id)
        {
            var tWatch = e.AddControl(new MakerToggle(makerCategory, "Watch last loaded texture file for changes", owner));
            tWatch.Value = WatchLoadedTexForChanges.Value;
            tWatch.ValueChanged.Subscribe(b => WatchLoadedTexForChanges.Value = b);

#if KK || KKS
            var otherId = id == 0 ? 1 : 0;
            _tPerCoord[id] = e.AddControl(new MakerToggle(makerCategory, "Use different overlays per outfit", owner));
            _tPerCoord[id].BindToFunctionController<KoiSkinOverlayController, bool>(c => c.OverlayStorage.IsPerCoord(),
                (c, value) =>
                {
                    if (!value) c.OverlayStorage.CopyToOtherCoords();
                    _tPerCoord[otherId].SetValue(value, false);
                });

            e.AddControl(new MakerText("When off, there is a single set of overlays for all outfits. When on, each outfit has its own set of skin overlays.", makerCategory, owner)
            { TextColor = MakerText.ExplanationGray });
#endif
        }

        public static void WriteAndOpenPng(byte[] pngData, string dumpType)
        {
            if (pngData == null) throw new ArgumentNullException(nameof(pngData));
            var filename = GetUniqueTexDumpFilename(dumpType);
            File.WriteAllBytes(filename, pngData);
            Util.OpenFileInExplorer(filename);
        }

        private void SetTexAndUpdate(byte[] tex, TexType texType)
        {
            var ctrl = GetOverlayController();
            var overlay = ctrl.SetOverlayTex(tex, texType);

#if KK || KKS
            if (!_tPerCoord[0].Value) ctrl.OverlayStorage.CopyToOtherCoords();
#endif

            _textureChanged.OnNext(new KeyValuePair<TexType, Texture2D>(texType, overlay));
        }

        private void SetupTexControls(RegisterCustomControlsEvent e, MakerCategory makerCategory, BaseUnityPlugin owner, TexType texType, string title)
        {
            var radButtons = texType == TexType.EyeOver || texType == TexType.EyeUnder ?
                e.AddControl(new MakerRadioButtons(makerCategory, owner, "Eye to edit", "Both", "Left", "Right")) :
                null;

            TexType GetTexType(bool cantBeBoth)
            {
                if (radButtons != null)
                {
                    if (radButtons.Value == 0)
                        return cantBeBoth ? texType + 2 : texType; // left or both
                    if (radButtons.Value == 1)
                        return texType + 2; // left
                    if (radButtons.Value == 2)
                        return texType + 4; // right
                }
                return texType;
            }

            e.AddControl(new MakerText(title, makerCategory, owner));

            var forceAllowBoth = false;
            var size = Util.GetRecommendedTexSize(texType);
            var bi = e.AddControl(new MakerImage(null, makerCategory, owner) { Width = Mathf.RoundToInt(150 * ((float)size.Width / size.Height)), Height = 150 });
            _textureChanged.Subscribe(
                d =>
                {
                    var incomingType = d.Key;
                    if (!forceAllowBoth)
                    {
                        // If left and right images are different, and we have Both selected, change selection to Left instead
                        var currentType = GetTexType(false);
                        if (radButtons != null && (currentType == TexType.EyeOver && incomingType == TexType.EyeOverR || currentType == TexType.EyeUnder && incomingType == TexType.EyeUnderR))
                        {
                            var leftTex = GetTex(GetTexType(true));
                            if (d.Value != leftTex)
                                radButtons.Value = 1;
                            else
                                radButtons.Value = 0;
                        }
                    }

                    if (incomingType == GetTexType(true) || incomingType == GetTexType(false))
                        bi.Texture = d.Value;
                });

            e.AddControl(new MakerButton("Load new texture", makerCategory, owner))
                .OnClick.AddListener(
                    () => OpenFileDialog.Show(strings => OnFileAccept(strings, GetTexType(false)), "Open overlay image", GetDefaultLoadDir(), FileFilter, FileExt));

            e.AddControl(new MakerButton("Clear texture", makerCategory, owner))
                .OnClick.AddListener(() => SetTexAndUpdate(null, GetTexType(false)));

            e.AddControl(new MakerButton("Export current texture", makerCategory, owner))
                .OnClick.AddListener(
                    () =>
                    {
                        try
                        {
                            var tex = GetTex(GetTexType(true));
                            if (tex == null) return;
                            // Fix being unable to save some texture formats with EncodeToPNG
                            var texCopy = tex.ToTexture2D();
                            WriteAndOpenPng(texCopy.EncodeToPNG(), GetTexType(false).ToString());
                            Destroy(texCopy);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogMessage("Failed to export texture - " + ex.Message);
                        }
                    });

            radButtons?.ValueChanged.Subscribe(i =>
            {
                forceAllowBoth = true;
                var safeType = GetTexType(true);
                _textureChanged.OnNext(new KeyValuePair<TexType, Texture2D>(safeType, GetTex(safeType)));
                forceAllowBoth = false;
            });

            Texture2D GetTex(TexType type)
            {
                var ctrl = GetOverlayController();
                var overlayTexture = ctrl.OverlayStorage.GetTexture(type);
                return overlayTexture;
            }
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
#if !EC
            if (StudioAPI.InsideStudio)
            {
                enabled = false;
                var cat = StudioAPI.GetOrCreateCurrentStateCategory("Overlays");
                cat.AddControl(new CurrentStateCategorySwitch("Skin overlays",
                    c => c.charInfo.GetComponent<KoiSkinOverlayController>().EnableInStudioSkin)).Value.Subscribe(
                    v => StudioAPI.GetSelectedControllers<KoiSkinOverlayController>().Do(c =>
                    {
                        if (c.EnableInStudioSkin != v)
                        {
                            c.EnableInStudioSkin = v;
                            c.UpdateTexture(TexType.Unknown);
                        }
                    }));
                cat.AddControl(new CurrentStateCategorySwitch("Eye overlays",
                    c => c.charInfo.GetComponent<KoiSkinOverlayController>().EnableInStudioIris)).Value.Subscribe(
                    v => StudioAPI.GetSelectedControllers<KoiSkinOverlayController>().Do(c =>
                    {
                        if (c.EnableInStudioIris != v)
                        {
                            c.EnableInStudioIris = v;
                            c.UpdateTexture(TexType.EyeUnder);
                            c.UpdateTexture(TexType.EyebrowUnder);
                        }
                    }));
                return;
            }
#endif

            MakerAPI.RegisterCustomSubCategories += RegisterCustomSubCategories;
            MakerAPI.MakerExiting += MakerExiting;
            CharacterApi.CharacterReloaded += (sender, args) => OnChaFileLoaded();
        }

        private void Update()
        {
            if (_bytesToLoad != null)
            {
                try
                {
                    var tex = Util.TextureFromBytes(_bytesToLoad, TextureFormat.ARGB32);

                    var recommendedSize = Util.GetRecommendedTexSize(_typeToLoad);
                    if ((tex.width == recommendedSize.Width && tex.height == recommendedSize.Height) ||
                        (tex.width == recommendedSize.Width * 2 && tex.height == recommendedSize.Height * 2))
                        Logger.LogMessage("Texture imported successfully");
                    else
                        Logger.LogMessage($"WARNING - Unusual texture resolution! It's recommended to use {recommendedSize} for {_typeToLoad}.");

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
                var tex = ctrl.OverlayStorage.GetTexture(texType);
                _textureChanged.OnNext(new KeyValuePair<TexType, Texture2D>(texType, tex));
            }
        }
    }
}
