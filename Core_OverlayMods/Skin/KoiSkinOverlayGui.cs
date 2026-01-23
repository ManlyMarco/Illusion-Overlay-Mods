/*
 
    Powerful plugins
    with unintuitive interfaces
    left me in despair
 
 */

using System;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
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
        internal static new ManualLogSource Logger;
        public const string GUID = KoiSkinOverlayMgr.GUID + "_GUI";

        public const string FileExt = ".png";
        public const string FileFilter = "Overlay images (*.png)|*.png|All files|*.*";

        private static readonly string LocalTexPathDefault = Path.Combine(Paths.GameRootPath, @"UserData\Overlays\_LocalTextures");
        private static string LocalTexPath = LocalTexPathDefault;

        private Subject<KeyValuePair<TexType, Texture2D>> _textureChanged;

        private static readonly MakerToggle[] _tPerCoord = new MakerToggle[2];
        private byte[] _bytesToLoad;
        private Exception _lastError;
        private TexType _typeToLoad;
        private FileSystemWatcher _texChangeWatcher;

        [Browsable(false)]
        public static ConfigEntry<bool> WatchLoadedTexForChanges;
        public static ConfigEntry<string> ConfLocalTexPath;

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

            var perCoordToggle = AddConfigSettings(e, owner, makerCategory, 0);

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.FaceOver, "Face overlay texture (On top of almost everything)", perCoordToggle);

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.BodyOver, "Body overlay texture (On top of almost everything)", perCoordToggle);

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.FaceUnder, "Face underlay texture (Under tattoos, blushes, etc.)", perCoordToggle);

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.BodyUnder, "Body underlay texture (Under tattoos, blushes, etc.)", perCoordToggle);

#if AI || HS2
            // Controls for DetailMainTex
            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.FaceDetailOver, "Face detail overlay texture (On top of almost everything)", perCoordToggle);

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.BodyDetailOver, "Body detail overlay texture (On top of almost everything)", perCoordToggle);

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.FaceDetailUnder, "Face detail underlay texture (Under tattoos, blushes, etc.)", perCoordToggle);

            e.AddControl(new MakerSeparator(makerCategory, owner));

            SetupTexControls(e, makerCategory, owner, TexType.BodyDetailUnder, "Body detail underlay texture (Under tattoos, blushes, etc.)", perCoordToggle);
#endif
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

            var perCoordToggle = AddConfigSettings(e, owner, eyeCategory, 1);

            e.AddControl(new MakerSeparator(eyeCategory, owner));

            SetupTexControls(e, eyeCategory, owner, TexType.EyeOver, "Iris overlay texture (On top of original iris)", perCoordToggle);

            e.AddControl(new MakerSeparator(eyeCategory, owner));

            SetupTexControls(e, eyeCategory, owner, TexType.EyeUnder, "Iris underlay texture (Before coloring and effects)", perCoordToggle);

            e.AddControl(new MakerSeparator(eyeCategory, owner));

            SetupTexControls(e, eyeCategory, owner, TexType.EyelineUnder, "Eyelashes override texture (Before coloring and effects)", perCoordToggle);

            e.AddControl(new MakerSeparator(eyeCategory, owner));

            SetupTexControls(e, eyeCategory, owner, TexType.EyebrowUnder, "Eyebrow override texture (Before coloring and effects)", perCoordToggle);
        }

        private static MakerToggle AddConfigSettings(RegisterSubCategoriesEvent e, KoiSkinOverlayMgr owner, MakerCategory makerCategory, int id)
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

            return _tPerCoord[id];
#else
            return null;
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

        private void SetupTexControls(RegisterCustomControlsEvent e, MakerCategory makerCategory, BaseUnityPlugin owner, TexType texType, string title, MakerToggle perCoordToggle)
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

#if KK || KKS
            SetupCopyToOtherClothButton(e, makerCategory, owner, perCoordToggle, (coords) =>
            {
                var ctrl = GetOverlayController();
                var copyTexType = GetTexType(false);

                if (radButtons != null && radButtons.Value == 0)
                {
                    ctrl.OverlayStorage.CopyToOtherCoords(coords, copyTexType + 2, copyTexType + 4);
                }
                else
                {
                    ctrl.OverlayStorage.CopyToOtherCoords(coords, copyTexType);
                }
            });
#endif

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

#if KK || KKS
        private void SetupCopyToOtherClothButton(RegisterCustomControlsEvent e, MakerCategory makerCategory, BaseUnityPlugin owner, MakerToggle perCoordToggle, UnityEngine.Events.UnityAction<HashSet<int>> onClickCopy)
        {
            if (perCoordToggle == null)
                return;

            var copyButton = e.AddControl(new MakerButton("Copy to other outfit overlay", makerCategory, owner));

            perCoordToggle.ObserveEveryValueChanged(t => t.Value).Subscribe(isPerCoord =>
            {
                copyButton.ControlObject?.SetActive(isPerCoord);
            });

            copyButton.ObserveEveryValueChanged(b => b.ControlObject).Where(g => g)
                .Subscribe(_ =>
                {
                    //setup

                    var copyButtonGObj = copyButton.ControlObject;
                    if (!copyButtonGObj) return;

                    copyButtonGObj.SetActive(perCoordToggle.Value);

                    var coordListButton = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/03_ClothesTop/tglCopy/CopyTop/rect/copyinfo/dst/ddDstCoordinate");

                    if (coordListButton)
                    {
                        RectTransform buttonRect = (RectTransform)copyButtonGObj.transform;
                        ((RectTransform)buttonRect.GetChild(0)).anchorMax = new Vector2(0.6f, 1.0f);

                        var copyCoordListDropdown = Instantiate(coordListButton, copyButtonGObj.transform);
                        copyCoordListDropdown.transform.SetSiblingIndex(copyButtonGObj.transform.GetSiblingIndex() + 1);
                        copyCoordListDropdown.transform.Find("Template/Scrollbar").GetComponent<Image>().raycastTarget = true;

                        foreach (var component in copyCoordListDropdown.GetComponents<UnityEngine.Component>())
                            if (component.GetType().Name.Contains("MultiSelectDropdown"))
                                DestroyImmediate(component);

                        var multiSelectDropdown = copyCoordListDropdown.AddComponent<MultiSelectDropdown>();
                        var layout = copyCoordListDropdown.AddComponent<LayoutElement>();
                        var srcLayout = copyButtonGObj.GetComponent<LayoutElement>();

                        copyButton.OnClick.RemoveAllListeners();
                        copyButton.OnClick.AddListener(() =>
                        {
                            onClickCopy(multiSelectDropdown.selected);
                            Logger.LogMessage($"Copy overlay to other {multiSelectDropdown.selected.Count} clothes.");
                        });

                        RectTransform dropdownRect = (RectTransform)copyCoordListDropdown.transform;
                        dropdownRect.anchorMin = new Vector2(0.58f, 0.0f);
                        dropdownRect.anchorMax = new Vector2(1.0f, 1.0f);
                        dropdownRect.offsetMin = new Vector2(8.0f, 5.0f);
                        dropdownRect.offsetMax = new Vector2(-8.0f, -5.0f);

                        if (srcLayout)
                        {
                            layout.ignoreLayout = srcLayout.ignoreLayout;
                            layout.minWidth = srcLayout.minWidth;
                            layout.minHeight = srcLayout.minHeight;
                            layout.preferredWidth = srcLayout.preferredWidth;
                            layout.preferredHeight = srcLayout.preferredHeight;
                            layout.flexibleWidth = srcLayout.flexibleWidth;
                            layout.flexibleHeight = srcLayout.flexibleHeight;
                        }

                        var chaCtrl = Singleton<ChaCustom.CustomBase>.Instance.chaCtrl;

                        var templateItem = copyCoordListDropdown.transform.Find("Template/Viewport/Content/Item");
                        foreach (var image in templateItem.GetComponentsInChildren<Image>())
                            image.raycastTarget = true;

                        var toggleGObj = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/00_FaceTop/tglMouth/MouthTop/tglCanine");
                        var copyToggleGObj = Instantiate(toggleGObj, templateItem);
                        DestroyImmediate(copyToggleGObj.GetComponent<LayoutElement>());
                        DestroyImmediate(copyToggleGObj.transform.Find("imgTglCol/textTgl").gameObject);

                        var toggle = copyToggleGObj.GetComponentInChildren<Toggle>();

                        toggle.gameObject.AddComponent<MultiSelectDropdownToggle>();
                        toggle.image.raycastTarget = true;
                        toggle.graphic.raycastTarget = true;
                        RectTransform toggleRect = (RectTransform)copyToggleGObj.transform;
                        toggleRect.anchorMin = new Vector2(0.92f, 0.0f);
                        toggleRect.anchorMax = new Vector2(1.0f, 1.0f);
                        toggleRect.offsetMin = new Vector2(-25.0f, -5.0f);
                        toggleRect.offsetMax = new Vector2(-10.0f, 5.0f);

                        chaCtrl?.ObserveEveryValueChanged(cha => cha.chaFile.coordinate.Length).Subscribe(__ =>
                        {
                            var coordsDropdown = copyCoordListDropdown.GetComponentInChildren<TMP_Dropdown>();
#if KK
                            int baseCoordinates = 7;
#elif KKS
                            int baseCoordinates = 4;
#endif
                            int coordinates = Math.Max(baseCoordinates, chaCtrl.chaFile.coordinate.Length) + 1; //+1=ALL

                            if (coordsDropdown.options.Count > coordinates)
                            {
                                coordsDropdown.options.RemoveRange(coordinates, coordsDropdown.options.Count - coordinates);
                            }
                            else if (coordsDropdown.options.Count < coordinates)
                            {
                                for (int i = coordsDropdown.options.Count; i < coordinates; ++i)
                                {
                                    if (i == coordinates - 1)
                                    {
                                        coordsDropdown.options.Add(new TMP_Dropdown.OptionData("All"));
                                    }
                                    else
                                    {
                                        coordsDropdown.options.Add(new TMP_Dropdown.OptionData("Outfit " + (i + 1)));
                                    }
                                }
                            }

                            // ReSharper disable once Unity.UnresolvedComponentOrScriptableObject
                            var moreOutfitsController = chaCtrl.GetComponent("MoreOutfitsController");
                            if (moreOutfitsController && moreOutfitsController.GetFieldValue("CoordinateNames", out object nameObjects))
                            {
                                Dictionary<int, string> nameTable = (Dictionary<int, string>)nameObjects;

                                foreach (var clothName in nameTable)
                                {
                                    if (0 <= clothName.Key && clothName.Key < coordsDropdown.options.Count)
                                    {
                                        coordsDropdown.options[clothName.Key].text = clothName.Value;
                                    }
                                }
                            }
                        });
                    }
                });
        }
#endif

        public static string GetDefaultLoadDir()
        {
            return Directory.Exists(KoiSkinOverlayMgr.OverlayDirectory) ? KoiSkinOverlayMgr.OverlayDirectory : Paths.GameRootPath;
        }

        private void Awake()
        {
            Logger = base.Logger;
            WatchLoadedTexForChanges = Config.Bind("Maker", "Watch loaded texture for changes", true);
            WatchLoadedTexForChanges.SettingChanged += (sender, args) =>
            {
                if (!WatchLoadedTexForChanges.Value)
                    _texChangeWatcher?.Dispose();
            };

            // Texture saving configs
            ConfLocalTexPath = Config.Bind("Textures", "Local Texture Path Override", "", new ConfigDescription($"Local textures will be exported to / imported from this folder. If empty, defaults to {LocalTexPathDefault}.\nWARNING: If you change this, make sure to move all files to the new path!", null, new ConfigurationManagerAttributes { Order = 10, IsAdvanced = true }));
            ConfLocalTexPath.SettingChanged += ConfLocalTexPath_SettingChanged;
            ConfLocalTexPath_SettingChanged(null, null);
            var handler = new TextureSaveHandler(LocalTexPath);
            handler.RegisterForAudit("Overlays", handler.LocalTexSavePrefix + TextureSaveHandler.DataKey);

            CharaLocalTextures.Activate();
#if !EC
            SceneLocalTextures.Activate();
#endif

#if KK || KKS
            Harmony.CreateAndPatchAll(typeof(TMP_Dropdown_Injector), nameof(TMP_Dropdown_Injector));
#endif
        }

        private void ConfLocalTexPath_SettingChanged(object sender, EventArgs e)
        {
            if (ConfLocalTexPath.Value.ToLower().StartsWith(Paths.GameRootPath.ToLower()))
            {
                if (ConfLocalTexPath.Value.Length > Paths.GameRootPath.Length)
                    ConfLocalTexPath.Value = ConfLocalTexPath.Value.Substring(Paths.GameRootPath.Length + 1);
                else
                    ConfLocalTexPath.Value = "";
                return;
            }
            if (ConfLocalTexPath.Value.Split(Path.GetInvalidPathChars()).Length == 1)
                SetLocalTexPath();
            if (TextureSaveHandler.Instance != null)
                TextureSaveHandler.Instance.LocalTexturePath = LocalTexPath;
        }

        private void SetLocalTexPath()
        {
            if (ConfLocalTexPath.Value == "")
                LocalTexPath = LocalTexPathDefault;
            else if (Path.IsPathRooted(ConfLocalTexPath.Value))
                LocalTexPath = ConfLocalTexPath.Value;
            else
                LocalTexPath = Path.Combine(Paths.GameRootPath, ConfLocalTexPath.Value);
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

#if KK || KKS

    internal class MultiSelectDropdownToggle : UIBehaviour
    {
        public Toggle parent;

        public override void Awake()
        {
            base.Awake();
            parent = GetComponent<Toggle>();
            parent.onValueChanged.AddListener(OnCheck);
        }

        private void OnCheck(bool check)
        {
            GetComponentInParent<MultiSelectDropdown>().OnToggle(transform.parent.parent.GetSiblingIndex() - 1, check);
        }
    }

    internal class MultiSelectDropdown : UIBehaviour
    {
        private TMP_Dropdown m_Dropdown;
        public HashSet<int> selected = new HashSet<int>();

        public override void Awake()
        {
            base.Awake();
            m_Dropdown = GetComponent<TMP_Dropdown>();
            selected.Add(m_Dropdown.value);
        }

        public void OnToggle(int index, bool check)
        {
            var toggles = GetComponentsInChildren<MultiSelectDropdownToggle>();
            int allIndex = toggles.Length - 1;

            if (index == allIndex)
            {
                if (check)
                {
                    for (int i = 0; i < toggles.Length; ++i)
                        selected.Add(i);
                }
                else
                {
                    selected.Clear();
                }
            }
            else
            {
                if (check)
                {
                    selected.Add(index);

                    if (selected.Count == toggles.Length - 1)
                        selected.Add(allIndex);
                }
                else
                {
                    selected.Remove(index);
                    selected.Remove(allIndex);
                }
            }

            SyncToggleState();
        }

        public void SyncToggleState()
        {
            var toggles = GetComponentsInChildren<MultiSelectDropdownToggle>();

            for (int i = 0; i < toggles.Length; ++i)
            {
                toggles[i].parent.Set(selected.Contains(i), false);
            }

            ReflashCaption();
        }

        public void SetOne(int index)
        {
            selected.Clear();

            var toggles = GetComponentsInChildren<MultiSelectDropdownToggle>();

            if (index == toggles.Length - 1)
            {
                for (int i = 0; i < toggles.Length; ++i)
                    selected.Add(i);
            }
            else
            {
                selected.Add(index);
            }

            SyncToggleState();
        }

        public void SetAll()
        {
            var toggles = GetComponentsInChildren<MultiSelectDropdownToggle>();

            selected.Clear();
            for (int i = 0; i < toggles.Length; ++i)
                selected.Add(i);

            SyncToggleState();
        }

        public void ReflashCaption()
        {
            if (!m_Dropdown)
                return;

            var items = m_Dropdown.m_Items;

            if (items == null || items.Count <= 0)
                return;

            StringBuilder builder = new StringBuilder();
            int maxLength = 36;

            for (int i = 0; i < items.Count - 1; ++i)
            {
                if (!selected.Contains(i))
                    continue;

                if (builder.Length > 0)
                    builder.Append('/');

                var name = items[i].name;
                builder.Append(name.Substring(name.IndexOf(':') + 1).Trim());

                if (builder.Length >= maxLength)
                    break;
            }

            if (builder.Length > maxLength)
            {
                builder.Remove(maxLength, builder.Length - maxLength);
                builder.Append("...");
            }

            m_Dropdown.m_CaptionText.text = builder.ToString();
        }
    }

    internal class TMP_Dropdown_Injector
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.Show))]
        private static void ShowPostfix(TMP_Dropdown __instance)
        {
            if (!__instance)
                return;

            MultiSelectDropdown multi = __instance.GetComponent<MultiSelectDropdown>();
            if (!multi)
                return;

            multi.SyncToggleState();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.RefreshShownValue))]
        private static void RefreshShownValuePostfix(TMP_Dropdown __instance)
        {
            if (!__instance)
                return;

            MultiSelectDropdown multi = __instance.GetComponent<MultiSelectDropdown>();
            if (!multi)
                return;

            multi.ReflashCaption();
        }

        [HarmonyPrefix]
#if KKS
        [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.SetValue))]
#endif
        [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.value), MethodType.Setter)]
        private static void SetValuePrefix(TMP_Dropdown __instance, int value)
        {
            if (!__instance)
                return;

            MultiSelectDropdown multi = __instance.GetComponent<MultiSelectDropdown>();
            if (!multi)
                return;

            multi.SetOne(value);
        }
    }

#endif
}
