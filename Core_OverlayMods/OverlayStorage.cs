﻿using System;
using System.Collections.Generic;
using System.Linq;
using ExtensibleSaveFormat;
using KKAPI.Chara;
using KKAPI.Maker;
using KoiClothesOverlayX;
using MessagePack;
using UnityEngine;
#if KK || KKS
using CoordinateType = ChaFileDefine.CoordinateType;
#elif EC
using CoordinateType = KoikatsuCharaFile.ChaFileDefine.CoordinateType;
#elif AI || HS2
using AIChara;
#endif

namespace KoiSkinOverlayX
{
#if AI || HS2
    public enum CoordinateType
    {
        Unknown = 0
    }
#endif

    public class OverlayStorage
    {
        private const string OverlayDataKey = "Lookup";

        private readonly TextureStorage _textureStorage;
        private readonly ChaControl _chaControl;
        private Dictionary<CoordinateType, Dictionary<TexType, int>> _allOverlayTextures;

        public OverlayStorage(CharaCustomFunctionController controller)
        {
            _chaControl = controller.ChaControl;
            _textureStorage = new TextureStorage();
            _allOverlayTextures = new Dictionary<CoordinateType, Dictionary<TexType, int>>();
        }

        private Dictionary<TexType, int> GetCurrentOverlayTextures()
        {
#if KK || KKS
            // Need to do this instead of polling the CurrentCoordinate prop because it's updated too late
            var coordinateType = (CoordinateType)_chaControl.fileStatus.coordinateType;
#elif EC
            var coordinateType = CoordinateType.School01;
#else
            var coordinateType = CoordinateType.Unknown;
#endif
            return GetOverlayTextures(coordinateType);
        }

        private Dictionary<TexType, int> GetOverlayTextures(CoordinateType coordinateType)
        {
            _allOverlayTextures.TryGetValue(coordinateType, out var dict);

            if (dict == null)
            {
                dict = new Dictionary<TexType, int>();
                _allOverlayTextures.Add(coordinateType, dict);
            }

            return dict;
        }

        //CoordinateType coordinateType
        public Texture2D GetTexture(TexType type)
        {
            var texs = GetCurrentOverlayTextures();
            if (texs.TryGetValue(type, out var id))
                return _textureStorage.GetSharedTexture(id);

            return null;
        }

        public void SetTexture(TexType type, byte[] pngData)
        {
            var texs = GetCurrentOverlayTextures();
            if (pngData == null)
            {
                texs.Remove(type);
            }
            else
            {
                var id = _textureStorage.StoreTexture(pngData);
                texs[type] = id;
            }
        }

        public int GetCount(bool onlyCurrentCoord = true)
        {
            return onlyCurrentCoord ? GetCurrentOverlayTextures().Count : _allOverlayTextures.Sum(x => x.Value.Count);
        }

        public void Clear()
        {
            // Less garbage generated than clearing the whole dict?
            foreach (var dic in _allOverlayTextures) dic.Value.Clear();
            _textureStorage.Clear();
        }

        public void Load(PluginData data)
        {
            data.data.TryGetValue(OverlayDataKey, out var lookup);
            if (lookup is byte[] lookuparr)
            {
                try
                {
                    _allOverlayTextures = MessagePackSerializer.Deserialize<Dictionary<CoordinateType, Dictionary<TexType, int>>>(lookuparr);
                    _textureStorage.Load(data);
                }
                catch (Exception ex)
                {
                    if (MakerAPI.InsideMaker)
                        KoiSkinOverlayMgr.Logger.LogMessage("WARNING: Failed to load embedded overlay data for " + (_chaControl.chaFile?.charaFileName ?? "?"));
                    else
                        KoiSkinOverlayMgr.Logger.LogDebug("WARNING: Failed to load embedded overlay data for " + (_chaControl.chaFile?.charaFileName ?? "?"));
                    KoiSkinOverlayMgr.Logger.LogError(ex);

                    Clear();
                }
            }
        }

        public void Save(PluginData data)
        {
            PurgeUnused();
            if (GetCount(false) > 0)
            {
                _textureStorage.Save(data);
                data.data[OverlayDataKey] = MessagePackSerializer.Serialize(_allOverlayTextures);
            }
        }

        private void PurgeUnused()
        {
            _textureStorage.PurgeUnused(_allOverlayTextures.SelectMany(x => x.Value.Values));

            var allTextures = _textureStorage.GetAllTextureIDs();
            foreach (var dic in _allOverlayTextures)
            {
                foreach (var invalidEntry in dic.Value.Where(x => !allTextures.Contains(x.Value)).ToList())
                {
                    KoiSkinOverlayMgr.Logger.LogWarning($"Invalid texture ID found, entry will be removed: coord={dic.Key} type={invalidEntry.Key} texID={invalidEntry.Value}");
                    dic.Value.Remove(invalidEntry.Key);
                }
            }
        }

#if KK || KKS
        public bool IsPerCoord() //todo handle adding coords
        {
            Dictionary<TexType, int> first = null;
            foreach (var dic in _allOverlayTextures)
            {
                if (first == null)
                    first = dic.Value;
                else if (!dic.Value.SequenceEqual(first))
                    return true;
            }

            return false;
        }

        public void CopyToOtherCoords()
        {
            var cur = GetCurrentOverlayTextures();

            for (var coordId = 0; coordId < _chaControl.chaFile.coordinate.Length; coordId++)
            {
                var other = GetOverlayTextures((CoordinateType)coordId);
                if (cur == other) continue;

                other.Clear();
                foreach (var curval in cur)
                    other.Add(curval.Key, curval.Value);
            }
        }
#endif

#if KKS
        public static void ImportFromKK(PluginData pluginData, Dictionary<int, int?> mapping)
        {
            pluginData.data.TryGetValue(OverlayDataKey, out var lookup);
            if (lookup is byte[] lookuparr)
            {
                var dic = MessagePackSerializer.Deserialize<Dictionary<CoordinateType, Dictionary<TexType, int>>>(lookuparr);
                var outDic = new Dictionary<CoordinateType, Dictionary<TexType, int>>(dic.Count);

                foreach (var map in mapping)
                {
                    // Discard unused
                    if (map.Value == null) continue;

                    dic.TryGetValue((CoordinateType)map.Key, out var value);
                    if (value != null)
                    {
                        outDic[(CoordinateType)map.Value.Value] = value;
                    }
                }

                pluginData.data[OverlayDataKey] = MessagePackSerializer.Serialize(outDic);
            }
        }
#endif
    }
}