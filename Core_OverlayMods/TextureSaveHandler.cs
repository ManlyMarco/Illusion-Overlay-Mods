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
        internal static TextureSaveHandler Instance;
        private const string DataKey = "_OverlayDictionary_";
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
            catch
            {
                KoiSkinOverlayGui.Logger.LogWarning("Save method failed, falling back to Bundled saving!");
                SaveBundled(pluginData, key, data, isCharaController);
            }
        }

        protected override bool IsBundled(PluginData pluginData, string key, out object data)
        {
            if (pluginData == null)
                throw new System.ArgumentNullException(nameof(pluginData));

            data = null;
            var firstKey = pluginData.data.Keys.First(x => x.StartsWith(key));
            if (firstKey != null && pluginData.data[firstKey] != null)
                return true;
            return false;
        }

#if !EC

        protected override bool IsDeduped(PluginData pluginData, string key, out object data)
        {
            return pluginData.data.TryGetValue(DedupedTexSavePrefix + key, out data) && data != null;
        }

#endif

        protected override bool IsLocal(PluginData pluginData, string key, out object data)
        {
            return pluginData.data.TryGetValue(LocalTexSavePrefix + DataKey, out data) && data != null;
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
            throw new System.NotImplementedException();
            //if (!Directory.Exists(LocalTexturePath))
            //    Directory.CreateDirectory(LocalTexturePath);

            //var dict = dictRaw as Dictionary<int, TextureHolder>;
            //var hashDict = dict.ToDictionary(pair => pair.Key, pair => pair.Value.Hash.ToString("X16"));
            //foreach (var kvp in hashDict)
            //{
            //    string fileName = LocalTexPrefix + kvp.Value + "." + ImageTypeIdentifier.Identify(dict[kvp.Key].Data);
            //    string filePath = Path.Combine(LocalTexturePath, fileName);
            //    if (!File.Exists(filePath))
            //        File.WriteAllBytes(filePath, dict[kvp.Key].Data);
            //}

            //data.data.Add(LocalTexSavePrefix + key, MessagePackSerializer.Serialize(hashDict));
        }

        protected override object LoadLocal(PluginData data, string key, object dataLocal, bool isCharaController = false)
        {
            throw new System.NotImplementedException();
            //var hashDic = MessagePackSerializer.Deserialize<Dictionary<int, string>>((byte[])data.data[LocalTexSavePrefix + key]);
            //return hashDic.ToDictionary(kvp => kvp.Key, kvp => new TextureContainer(LoadLocal(kvp.Value)));
        }

        private byte[] LoadLocal(string hash)
        {
            if (!Directory.Exists(LocalTexturePath))
            {
                KoiSkinOverlayGui.Logger.LogMessage("[MaterialEditor] Local texture directory doesn't exist, can't load texture!");
                return new byte[0];
            }

            string searchPattern = LocalTexPrefix + hash + ".*";
            string[] files = Directory.GetFiles(LocalTexturePath, searchPattern, SearchOption.TopDirectoryOnly);
            if (files == null || files.Length == 0)
            {
                KoiSkinOverlayGui.Logger.LogMessage($"[MaterialEditor] No local texture found with hash {hash}!");
                return new byte[0];
            }
            if (files.Length > 1)
            {
                KoiSkinOverlayGui.Logger.LogMessage($"[MaterialEditor] Multiple local textures found with hash {hash}, aborting!");
                return new byte[0];
            }

            return File.ReadAllBytes(files[0]);
        }
    }
}
