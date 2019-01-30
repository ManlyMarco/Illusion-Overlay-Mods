using System;
using System.Collections.Generic;
using System.IO;
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
    [BepInPlugin(KoiClothesOverlayMgr.GUID + "_GUI", "KCOX GUI", KoiSkinOverlayMgr.Version)]
    [BepInDependency(KoiClothesOverlayMgr.GUID)]
    public class KoiClothesOverlayGui : BaseUnityPlugin
    {
        private const string FileExt = ".png";
        private const string FileFilter = "Overlay images (*.png)|*.png|All files|*.*";
        private static MakerAPI.MakerAPI _api;
        private byte[] _bytesToLoad;
        private Exception _lastError;

        private Subject<KeyValuePair<ClothesTexId, Texture2D>> _textureChanged;
        private ClothesTexId _typeToLoad;

        private static string GetDefaultLoadDir()
        {
            return File.Exists(KoiSkinOverlayMgr.OverlayDirectory) ? KoiSkinOverlayMgr.OverlayDirectory : Paths.GameRootPath;
        }

        private static KoiClothesOverlayController GetOverlayController()
        {
            return _api.GetCharacterControl().gameObject.GetComponent<KoiClothesOverlayController>();
        }

        private static string GetUniqueTexDumpFilename()
        {
            var path = Path.Combine(KoiSkinOverlayMgr.OverlayDirectory, "_Export");
            Directory.CreateDirectory(path);
            var file = Path.Combine(path, DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + FileExt);
            return file;
        }

        private void MakerExiting(object sender, EventArgs e)
        {
            _textureChanged?.Dispose();
            _bytesToLoad = null;
            _lastError = null;
        }

        private void OnChaFileLoaded(object sender, ChaFileLoadedEventArgs e)
        {
            UpdateInterface();
        }

        private void OnFileAccept(string[] strings, ClothesTexId type)
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
        }

        private void RegisterCustomSubCategories(object sender, RegisterSubCategoriesEvent e)
        {
            var owner = this;
            _textureChanged = new Subject<KeyValuePair<ClothesTexId, Texture2D>>();

            var makerCategory = MakerConstants.GetBuiltInCategory("03_ClothesTop", "tglTop");

            SetupTexControls(e, makerCategory, owner, "ct_top_parts_A", "Overlay textures (Piece 1)");
            e.AddControl(new MakerSeparator(makerCategory, owner));
            SetupTexControls(e, makerCategory, owner, "ct_top_parts_B", "Overlay textures (Piece 2)");
            e.AddControl(new MakerSeparator(makerCategory, owner));
            SetupTexControls(e, makerCategory, owner, "ct_top_parts_C", "Overlay textures (Piece 3)");

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

            foreach (var pair in cats)
                SetupTexControls(e, MakerConstants.GetBuiltInCategory("03_ClothesTop", pair.Key), owner, pair.Value);
        }

        private void SetTexAndUpdate(Texture2D tex, ClothesTexId texType)
        {
            var ctrl = GetOverlayController();

            ctrl.SetOverlayTex(tex, texType);
            //ctrl.UpdateTexture(texType);

            _textureChanged.OnNext(new KeyValuePair<ClothesTexId, Texture2D>(texType, tex));
        }

        private void SetupTexControls(RegisterCustomControlsEvent e, MakerCategory makerCategory, BaseUnityPlugin owner, string clothesId, string title = "Overlay textures")
        {
            e.AddControl(new MakerText(title, makerCategory, owner));

            var rCat = e.AddControl(new MakerRadioButtons(makerCategory, owner, "Renderer group", "Normal 1", "Normal 2", "Alpha 1", "Alpha 2"));
            var rNum = e.AddControl(new MakerRadioButtons(makerCategory, owner, "Renderer number", "1", "2", "3", "4", "5", "6"));

            ClothesTexId GetSelectedId()
            {
                return new ClothesTexId(clothesId, (ClothesRendererGroup) rCat.Value, rNum.Value);
            }

            var bi = e.AddControl(new MakerImage(null, makerCategory, owner) {Height = 150, Width = 150});
            _textureChanged.Subscribe(
                d =>
                {
                    // todo better way of updating
                    if (Equals(GetSelectedId(), d.Key))
                        bi.Texture = d.Value;
                });

            e.AddControl(new MakerButton("Load new texture", makerCategory, owner))
                .OnClick.AddListener(
                    () => OpenFileDialog.Show(strings => OnFileAccept(strings, GetSelectedId()), "Open overlay image", GetDefaultLoadDir(), FileFilter, FileExt));

            e.AddControl(new MakerButton("Clear texture", makerCategory, owner))
                .OnClick.AddListener(() => SetTexAndUpdate(null, GetSelectedId()));

            //todo
            e.AddControl(new MakerButton("Get overlay template", makerCategory, owner));

            e.AddControl(new MakerButton("Export current texture", makerCategory, owner))
                .OnClick.AddListener(
                    () =>
                    {
                        try
                        {
                            var tex = bi.Texture as Texture2D;
                            if (tex == null) return;
                            WriteAndOpenPng(tex.EncodeToPNG());
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Error | LogLevel.Message, "[KSOX] Failed to export texture - " + ex.Message);
                        }
                    });
        }

        private void Start()
        {
            _api = MakerAPI.MakerAPI.Instance;

            _api.RegisterCustomSubCategories += RegisterCustomSubCategories;
            _api.MakerExiting += MakerExiting;
            _api.ChaFileLoaded += OnChaFileLoaded;
            // Needed for starting maker in class roster. There is no ChaFileLoaded event fired
            _api.MakerFinishedLoading += (sender, args) => UpdateInterface();
        }

        private void Update()
        {
            if (_bytesToLoad != null)
            {
                try
                {
                    var tex = Util.TextureFromBytes(_bytesToLoad);

                    SetTexAndUpdate(tex, _typeToLoad);

                    if (tex.width != tex.height || tex.height % 1024 != 0 || tex.height == 0)
                        Logger.Log(LogLevel.Message | LogLevel.Warning, "[KSOX] WARNING - Unusual texture resolution! It's recommended to use 1024x1024 for face and 2048x2048 for body.");
                    else
                        Logger.Log(LogLevel.Message, "[KSOX] Texture imported successfully");
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
                Logger.Log(LogLevel.Debug, _lastError);
                _lastError = null;
            }
        }

        private void UpdateInterface()
        {
            //todo unnecessary? update when switching to the tab? or every redraw, check bools if it's a full reload
            /*var ctrl = GetOverlayController();
            foreach (var texType in new[] { TexType.BodyOver, TexType.BodyUnder, TexType.FaceOver, TexType.FaceUnder })
            {
                var tex = ctrl.Overlays.FirstOrDefault(x => x.Key == texType).Value;
                _textureChanged.OnNext(new KeyValuePair<ClothesTexId, Texture2D>(texType, tex));
            }*/
        }

        private static void WriteAndOpenPng(byte[] pngData)
        {
            if (pngData == null) throw new ArgumentNullException(nameof(pngData));
            var filename = GetUniqueTexDumpFilename();
            File.WriteAllBytes(filename, pngData);
            Util.OpenFileInExplorer(filename);
        }
    }
}
