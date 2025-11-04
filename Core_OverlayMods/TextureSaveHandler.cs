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
                KoiSkinOverlayGui.Logger.LogWarning("Save method failed, falling back to Bundled saving!");
                KoiSkinOverlayGui.Logger.LogError(ex);
                SaveBundled(pluginData, key, data, isCharaController);
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
            return pluginData?.data.TryGetValue(DedupedTexSavePrefix + DataKey, out data) != null && data != null;
        }

#endif

        protected override bool IsLocal(PluginData pluginData, string key, out object data)
        {
            data = null;
            return pluginData?.data.TryGetValue(LocalTexSavePrefix + DataKey, out data) != null && data != null;
        }

        protected override void SaveBundled(PluginData pluginData, string key, object dictRaw, bool isCharaController = false)
        {
            if (dictRaw is Dictionary<int, TextureHolder> _dataSkin)
            {
                if (pluginData == null) throw new System.ArgumentNullException(nameof(pluginData));
                lock (_dataSkin)
                {
                    foreach (var tex in _dataSkin)
                    {
                        if (tex.Value == null) continue;
                        pluginData.data[key + tex.Key] = tex.Value.Data;
                    }
                }
            }
            // KoiClothesOverlayX
            else if (dictRaw is Dictionary<CoordinateType, Dictionary<string, ClothesTexData>> _dataClothes)
            {
                pluginData.data[key] = MessagePackSerializer.Serialize(_dataClothes);
            }
        }

        protected override object LoadBundled(PluginData pluginData, string key, object discard, bool isCharaController = false)
        {
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

        protected override void SaveDeduped(PluginData data, string key, object dictRaw, bool isCharaController = false)
        {
            throw new System.NotImplementedException();
            //var dict = dictRaw as Dictionary<int, TextureContainer>;

            //data.data.Add(DedupedTexSavePrefix + key, MessagePackSerializer.Serialize(
            //    dict.ToDictionary(pair => pair.Key, pair => pair.Value.Hash.ToString("X16"))
            //));
            //if (isCharaController)
            //    return;

            //HashSet<long> hashes = new HashSet<long>();
            //Dictionary<string, byte[]> dicHashToData = new Dictionary<string, byte[]>();
            //foreach (var kvp in dict)
            //{
            //    string hashString = kvp.Value.Hash.ToString("X16");
            //    hashes.Add(kvp.Value.Hash);
            //    dicHashToData.Add(hashString, kvp.Value.Data);
            //}

            //foreach (var controller in MaterialEditorCharaController.charaControllers)
            //    foreach (var textureContainer in controller.TextureDictionary.Values)
            //        if (!hashes.Contains(textureContainer.Hash))
            //        {
            //            hashes.Add(textureContainer.Hash);
            //            dicHashToData.Add(textureContainer.Hash.ToString("X16"), textureContainer.Data);
            //        }

            //data.data.Add(DedupedTexSavePrefix + key + DedupedTexSavePostfix, MessagePackSerializer.Serialize(dicHashToData));
        }

        protected override object LoadDeduped(PluginData data, string key, object dataDeduped, bool isCharaController = false)
        {
            throw new System.NotImplementedException();
            //if (data.data.TryGetValue(DedupedTexSavePrefix + key, out var dedupedData) && dedupedData != null)
            //{
            //    if (DedupedTextureData == null)
            //        if (MEStudio.GetSceneController().GetExtendedData()?.data.TryGetValue(DedupedTexSavePrefix + key + DedupedTexSavePostfix, out var dataBytes) != null && dataBytes != null)
            //            DedupedTextureData = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>((byte[])dataBytes);
            //        else
            //            MaterialEditorPluginBase.Logger.LogMessage($"[MaterialEditor] Failed to load deduped {(isCharaController ? "character" : "scene")} textures!");
            //    Dictionary<int, TextureContainer> result = new Dictionary<int, TextureContainer>();
            //    if (DedupedTextureData != null)
            //        result = MessagePackSerializer.Deserialize<Dictionary<int, string>>((byte[])dedupedData).ToDictionary(pair => pair.Key, pair => new TextureContainer(DedupedTextureData[pair.Value]));
            //    if (!isCharaController)
            //        DedupedTextureData = null;
            //    return result;
            //}

            //return DefaultData();
        }

#endif
        protected override void SaveLocal(PluginData data, string key, object dictRaw, bool isCharaController = false)
        {

            if (!Directory.Exists(LocalTexturePath))
                Directory.CreateDirectory(LocalTexturePath);

            var dict = new Dictionary<KeyValuePair<string, ulong>, byte[]>();
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
                        dict.Add(new KeyValuePair<string, ulong>(i.ToString(), kvp1.Value[kvpKey].Hash), kvp1.Value[kvpKey].TextureBytes);
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
                    dict.Add(new KeyValuePair<string, ulong>(kvp.Key.ToString(), kvp.Value.Hash), kvp.Value.Data);
            }

            foreach (var kvp in dict)
            {
                string fileName = LocalTexPrefix + kvp.Key.Value.ToString("X16") + "." + ImageTypeIdentifier.Identify(kvp.Value);
                string filePath = Path.Combine(LocalTexturePath, fileName);
                if (!File.Exists(filePath))
                    File.WriteAllBytes(filePath, kvp.Value);
            }

            var hashDict = dict.ToDictionary(pair => pair.Key.Key, pair => pair.Key.Value.ToString("X16"));
            data.data.Add(LocalTexSavePrefix + DataKey, MessagePackSerializer.Serialize(hashDict));
            if (clothesData != null)
                data.data.Add(LocalTexSavePrefix + key, MessagePackSerializer.Serialize(clothesData));
        }

        protected override object LoadLocal(PluginData data, string key, object dataLocal, bool isCharaController = false)
        {
            var hashDic = MessagePackSerializer.Deserialize<Dictionary<string, string>>((byte[])data.data[LocalTexSavePrefix + DataKey]);
            if (hashDic == null ||  hashDic.Count == 0) return DefaultData();
            // KoiSkinOverlayX
            if (!data.data.ContainsKey(LocalTexSavePrefix + key))
            {
                return hashDic.ToDictionary(kvp => int.Parse(kvp.Key), kvp => new TextureHolder(LoadLocal(kvp.Value)));
            }
            // KoiClothesOverlayX
            else
            {
                var hashBytesToData = hashDic.ToDictionary(
                    kvp => BitConverter.GetBytes(int.Parse(kvp.Key)),
                    kvp => LoadLocal(kvp.Value));
                var loadedDic = MessagePackSerializer.Deserialize
                    <Dictionary<CoordinateType, Dictionary<string, ClothesTexData>>>
                    ((byte[])data.data[LocalTexSavePrefix + key]);
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
