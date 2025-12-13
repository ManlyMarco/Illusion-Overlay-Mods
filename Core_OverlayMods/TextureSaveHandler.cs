#pragma warning disable CS0436

using ExtensibleSaveFormat;
using KKAPI.Maker;
using KKAPI.Utilities;
using KoiClothesOverlayX;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static KoiSkinOverlayX.TextureStorage;
#if KK || KKS
using CoordinateType = ChaFileDefine.CoordinateType;
#elif EC
using CoordinateType = KoikatsuCharaFile.ChaFileDefine.CoordinateType;
#endif

namespace KoiSkinOverlayX
{
    internal class TextureSaveHandler : TextureSaveHandlerBase
    {
        public const string DataKey = "_OverlayDictionary_";

        public static TextureSaveHandler Instance { get; private set; }

#if !EC
        private Dictionary<string, byte[]> DedupedTextureData = null;
#endif

        public TextureSaveHandler(
            string localTexturePath,
            string localTexPrefix = "OM_LocalTex_",
            string localTexSavePrefix = "LOCAL",
            string dedupedTexSavePrefix = "DEDUPED",
            string dedupedTexSavePostfix = "DATA",
            string localTexUnusedFolder = "_Unused"
        ) : base(
            localTexturePath,
            localTexPrefix,
            localTexSavePrefix,
            dedupedTexSavePrefix,
            dedupedTexSavePostfix,
            localTexUnusedFolder
        )
        { Instance = this; }

        protected override object DefaultData()
        {
            return null;
        }

        public override void Save(PluginData pluginData, string key, object data, bool isCharaController)
        {
            try
            {
                base.Save(pluginData, key, data, isCharaController);
            }
            catch (Exception ex)
            {
                KoiSkinOverlayGui.Logger.LogError(ex);
                KoiSkinOverlayGui.Logger.LogWarning("Save method failed, falling back to Bundled saving!");
                SaveBundled(pluginData, key, data, isCharaController);
            }
        }

        public override T Load<T>(PluginData pluginData, string key, bool isCharaController)
        {
            if (pluginData.version <= 2)
                return base.Load<T>(pluginData, key, isCharaController);
            else
            {
                KoiSkinOverlayGui.Logger.LogMessage("[OverlayMods] Unsupported data version! Please update your plugin!");
                throw new Exception("Unsupported data version! Plugin is outdated.");
            }
        }

        protected override bool IsBundled(PluginData pluginData, string key, out object data)
        {
            data = null;

            if (pluginData == null)
                return false;
            var startsWith = pluginData.data.Where(x => x.Key.StartsWith(key)).ToArray();
            if (startsWith != null && startsWith.Length > 0 && pluginData.data[startsWith[0].Key] != null)
                return true;
            return false;
        }

#if !EC

        protected override bool IsDeduped(PluginData pluginData, string key, out object data)
        {
            data = null;
            return pluginData?.data.TryGetValue(DedupedTexSavePrefix + DataKey, out data) == true && data != null;
        }

#endif

        protected override bool IsLocal(PluginData pluginData, string key, out object data)
        {
            data = null;
            return pluginData?.data.TryGetValue(LocalTexSavePrefix + DataKey, out data) == true && data != null;
        }

        protected override void SaveBundled(PluginData pluginData, string key, object dictRaw, bool isCharaController = false)
        {
            if (!isCharaController) return;
            if (pluginData == null) throw new ArgumentNullException(nameof(pluginData));

            // KoiSkinOverlayX
            if (dictRaw is Dictionary<int, TextureHolder> _dataSkin)
            {
                lock (_dataSkin)
                    foreach (var tex in _dataSkin.Where(x => x.Value != null))
                        pluginData.data[key + tex.Key] = tex.Value.Data;
            }
            // KoiClothesOverlayX
            else if (dictRaw is Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> _dataClothes)
            {
                pluginData.data[key] = MessagePackSerializer.Serialize(_dataClothes);
            }
        }

        protected override object LoadBundled(PluginData pluginData, string key, object discard, bool isCharaController = false)
        {
            if (!isCharaController) return DefaultData();

            var _data = new Dictionary<int, TextureHolder>();

            foreach (var dataPair in pluginData.data.Where(x => x.Key.StartsWith(key)))
            {
                // KoiClothesOverlayX
                if (dataPair.Key == key)
                {
                    try
                    {
                        return MessagePackSerializer.Deserialize
                            <Dictionary<CoordinateType, Dictionary<string, ClothesTexData>>>
                            ((byte[])dataPair.Value);
                    }
                    catch (Exception ex)
                    {
                        if (MakerAPI.InsideMaker)
                            KoiSkinOverlayMgr.Logger.LogMessage("WARNING: Failed to load clothes overlay data");
                        else
                            KoiSkinOverlayMgr.Logger.LogDebug("WARNING: Failed to load clothes overlay data");
                        KoiSkinOverlayMgr.Logger.LogError(ex);
                    }
                }

                // KoiSkinOverlayX
                var idStr = dataPair.Key.Substring(key.Length);
                if (!int.TryParse(idStr, out var id))
                {
                    KoiSkinOverlayMgr.Logger.LogDebug($"Invalid ID {idStr} in key {dataPair.Key}");
                    continue;
                }

                var value = dataPair.Value as byte[];
                if (value == null && dataPair.Value != null)
                {
                    KoiSkinOverlayMgr.Logger.LogDebug($"Invalid value of ID {id}. Should be of type byte[] but is {dataPair.Value.GetType()}");
                    continue;
                }

                _data[id] = new TextureHolder(value);
            }

            return _data;
        }
#if !EC

        protected override void SaveDeduped(PluginData pluginData, string key, object dictRaw, bool isCharaController = false)
        {
            if (isCharaController)
            {
                if (!Directory.Exists(LocalTexturePath))
                    Directory.CreateDirectory(LocalTexturePath);

                var dict = new Dictionary<KeyValuePair<int, ulong>, byte[]>();
                // KoiClothesOverlayX
                Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> clothesData = null;
                if (dictRaw is Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> clothesDataReceived)
                {
                    clothesData = clothesDataReceived.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.ToDictionary(
                            kvp2 => kvp2.Key,
                            kvp2 => kvp2.Value));

                    int i = 0;
                    foreach (var kvp1 in clothesData)
                        foreach (var kvpKey in kvp1.Value.Keys.ToArray())
                        {
                            dict.Add(new KeyValuePair<int, ulong>(i, kvp1.Value[kvpKey].Hash), kvp1.Value[kvpKey].TextureBytes);
                            kvp1.Value[kvpKey] = new ClothesTexData()
                            {
                                TextureBytes = BitConverter.GetBytes(i),
                                BlendingMode = kvp1.Value[kvpKey].BlendingMode,
                                Override = kvp1.Value[kvpKey].Override
                            };
                            i++;
                        }
                }
                // KoiSkinOverlayX
                else if (dictRaw is Dictionary<int, TextureHolder> skinData)
                {
                    foreach (var kvp in skinData)
                        dict.Add(new KeyValuePair<int, ulong>(kvp.Key, kvp.Value.Hash), kvp.Value.Data);
                }
                else
                {
                    KoiSkinOverlayGui.Logger.LogError("Invalid data received for saving!");
                    KoiSkinOverlayGui.Logger.LogMessage("[Overlays] Couldn't save overlays!");
                    return;
                }

                var hashDict = dict.ToDictionary(pair => pair.Key.Key, pair => pair.Key.Value.ToString("X16"));
                pluginData.data.Add(DedupedTexSavePrefix + DataKey, MessagePackSerializer.Serialize(hashDict));
                if (clothesData != null)
                    pluginData.data.Add(DedupedTexSavePrefix + key, MessagePackSerializer.Serialize(clothesData));

                return;
            }

            // SceneController
            var dicHashData = new Dictionary<ulong, byte[]>();
            foreach (var controller in UnityEngine.Object.FindObjectsOfType<KoiSkinOverlayController>())
                foreach (var item in controller.OverlayStorage.TextureData.Where(x => !dicHashData.ContainsKey(x.Hash)))
                    dicHashData.Add(item.Hash, item.Data);
            foreach (var controller in UnityEngine.Object.FindObjectsOfType<KoiClothesOverlayController>())
                foreach (var item in controller.TextureData.Where(x => !dicHashData.ContainsKey(x.Hash)))
                    dicHashData.Add(item.Hash, item.TextureBytes);

            var hashDictScene = dicHashData.ToDictionary(kvp => kvp.Key.ToString("X16"), kvp => kvp.Value);
            pluginData.data.Add(DedupedTexSavePrefix + DataKey + DedupedTexSavePostfix, MessagePackSerializer.Serialize(hashDictScene));
        }

        protected override object LoadDeduped(PluginData pluginData, string key, object dataDeduped, bool isCharaController = false)
        {
            if (isCharaController)
            {
                if (DedupedTextureData == null)
                    if (OverlaySceneTextureController.Instance.GetExtendedData()?.data.TryGetValue(DedupedTexSavePrefix + DataKey + DedupedTexSavePostfix, out var dataBytes) != null && dataBytes != null)
                        DedupedTextureData = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>((byte[])dataBytes);
                    else
                    {
                        KoiSkinOverlayGui.Logger.LogMessage("[Overlays] Failed to load deduped character textures!");
                        return DefaultData();
                    }
                var hashDic = MessagePackSerializer.Deserialize<Dictionary<int, string>>((byte[])dataDeduped);
                
                // KoiClothesOverlayX
                if (pluginData.data.TryGetValue(DedupedTexSavePrefix + key, out var clothesDataBytes))
                {
                    var hashBytesToData = hashDic.ToDictionary(
                        kvp => BitConverter.GetBytes(kvp.Key),
                        kvp => DedupedTextureData[kvp.Value]);
                    var loadedDic = MessagePackSerializer.Deserialize
                        <Dictionary<CoordinateType, Dictionary<string, ClothesTexData>>>
                        ((byte[])clothesDataBytes);
                    foreach (var kvp1 in loadedDic)
                        foreach (var kvp2 in kvp1.Value)
                            foreach (var kvp3 in hashBytesToData)
                                if (kvp2.Value.TextureBytes.SequenceEqualFast(kvp3.Key))
                                {
                                    kvp2.Value.TextureBytes = kvp3.Value;
                                    break;
                                }
                    return loadedDic;
                }
                // KoiSkinOverlayX
                else
                {
                    return hashDic.ToDictionary(kvp => kvp.Key, kvp => new TextureHolder(DedupedTextureData[kvp.Value]));
                }
            }

            DedupedTextureData = null;
            return DefaultData();
        }

#endif
        protected override void SaveLocal(PluginData pluginData, string key, object dictRaw, bool isCharaController = false)
        {
            if (!isCharaController) return;

            if (!Directory.Exists(LocalTexturePath))
                Directory.CreateDirectory(LocalTexturePath);

            var dict = new Dictionary<KeyValuePair<int, ulong>, byte[]>();
            // KoiClothesOverlayX
            Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> clothesData = null;
            if (dictRaw is Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> clothesDataReceived)
            {
                clothesData = clothesDataReceived.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToDictionary(
                        kvp2 => kvp2.Key,
                        kvp2 => kvp2.Value));

                int i = 0;
                foreach (var kvp1 in clothesData)
                    foreach (var kvpKey in kvp1.Value.Keys.ToArray())
                    {
                        dict.Add(new KeyValuePair<int, ulong>(i, kvp1.Value[kvpKey].Hash), kvp1.Value[kvpKey].TextureBytes);
                        kvp1.Value[kvpKey] = new ClothesTexData() {
                            TextureBytes = BitConverter.GetBytes(i),
                            BlendingMode = kvp1.Value[kvpKey].BlendingMode,
                            Override = kvp1.Value[kvpKey].Override
                        };
                        i++;
                    }
            }
            // KoiSkinOverlayX
            else if (dictRaw is Dictionary<int, TextureHolder> skinData)
            {
                foreach (var kvp in skinData)
                    dict.Add(new KeyValuePair<int, ulong>(kvp.Key, kvp.Value.Hash), kvp.Value.Data);
            }
            else
            {
                KoiSkinOverlayGui.Logger.LogError("Invalid data received for saving!");
                KoiSkinOverlayGui.Logger.LogMessage("[Overlays] Couldn't save overlays!");
                return;
            }

            foreach (var kvp in dict)
            {
                string fileName = LocalTexPrefix + kvp.Key.Value.ToString("X16") + "." + ImageTypeIdentifier.Identify(kvp.Value);
                string filePath = Path.Combine(LocalTexturePath, fileName);
                if (!File.Exists(filePath))
                    File.WriteAllBytes(filePath, kvp.Value);
            }

            var hashDict = dict.ToDictionary(pair => pair.Key.Key, pair => pair.Key.Value.ToString("X16"));
            pluginData.data.Add(LocalTexSavePrefix + DataKey, MessagePackSerializer.Serialize(hashDict));
            if (clothesData != null)
                pluginData.data.Add(LocalTexSavePrefix + key, MessagePackSerializer.Serialize(clothesData));
        }

        protected override object LoadLocal(PluginData data, string key, object dataLocal, bool isCharaController = false)
        {
            if (!isCharaController) return DefaultData();

            var hashDic = MessagePackSerializer.Deserialize<Dictionary<int, string>>((byte[])dataLocal);
            if (hashDic == null || hashDic.Count == 0) return DefaultData();
            // KoiClothesOverlayX
            if (data.data.TryGetValue(LocalTexSavePrefix + key, out var loadedDicBytes))
            {
                if (loadedDicBytes == null)
                {
                    KoiSkinOverlayGui.Logger.LogMessage("[Overlays] Failed to load lcoal character overlays!");
                    return DefaultData();
                }

                var hashBytesToData = hashDic.ToDictionary(
                    kvp => BitConverter.GetBytes(kvp.Key),
                    kvp => LoadLocal(kvp.Value));
                var loadedDic = MessagePackSerializer.Deserialize
                    <Dictionary<CoordinateType, Dictionary<string, ClothesTexData>>>
                    ((byte[])loadedDicBytes);
                foreach (var kvp1 in loadedDic)
                    foreach (var kvp2 in kvp1.Value)
                        foreach (var kvp3 in hashBytesToData)
                            if (kvp2.Value.TextureBytes.SequenceEqualFast(kvp3.Key))
                            {
                                kvp2.Value.TextureBytes = kvp3.Value;
                                break;
                            }
                return loadedDic;
            }
            // KoiSkinOverlayX
            else
            {
                return hashDic.ToDictionary(kvp => kvp.Key, kvp => new TextureHolder(LoadLocal(kvp.Value)));
            }
        }

        private byte[] LoadLocal(string hash)
        {
            if (!Directory.Exists(LocalTexturePath))
            {
                KoiSkinOverlayGui.Logger.LogMessage("[Overlays] Local texture directory doesn't exist, can't load texture!");
                return new byte[0];
            }

            string searchPattern = LocalTexPrefix + hash + ".*";
            string[] files = Directory.GetFiles(LocalTexturePath, searchPattern, SearchOption.TopDirectoryOnly);
            if (files == null || files.Length == 0)
            {
                KoiSkinOverlayGui.Logger.LogMessage($"[Overlays] No local texture found with hash {hash}!");
                return new byte[0];
            }
            if (files.Length > 1)
            {
                KoiSkinOverlayGui.Logger.LogMessage($"[Overlays] Multiple local textures found with hash {hash}, aborting!");
                return new byte[0];
            }

            return File.ReadAllBytes(files[0]);
        }
    }
}
