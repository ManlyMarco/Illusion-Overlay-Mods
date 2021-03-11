using System;
using System.Collections.Generic;
using System.Linq;
using ExtensibleSaveFormat;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace KoiSkinOverlayX
{
    internal class TextureStorage : IDisposable
    {
        private const string DataMarker = "_TextureID_";

        private readonly Dictionary<int, TextureHolder> _data = new Dictionary<int, TextureHolder>();

        void IDisposable.Dispose()
        {
            lock (_data)
            {
                foreach (var tex in _data) tex.Value?.Dispose();
                _data.Clear();
            }
        }

        public void PurgeUnused(IEnumerable<int> usedIDs)
        {
            if (usedIDs == null) throw new ArgumentNullException(nameof(usedIDs));
            var lookup = new HashSet<int>(usedIDs);

            lock (_data)
            {
                foreach (var kvp in _data.ToList())
                {
                    var contains = lookup.Contains(kvp.Key);
                    if (!contains || kvp.Value?.Data == null)
                    {
                        Console.WriteLine($"Removing {(contains ? "empty" : "unused")} texture with ID {kvp.Key}");
                        kvp.Value?.Dispose();
                        _data.Remove(kvp.Key);
                    }
                }
            }
        }

        public void Clear()
        {
            ((IDisposable)this).Dispose();
        }

        public void Load(PluginData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            lock (_data)
            {
                foreach (var dataPair in data.data.Where(x => x.Key.StartsWith(DataMarker)))
                {
                    var idStr = dataPair.Key.Substring(DataMarker.Length);
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
            }
        }

        public void Save(PluginData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            lock (_data)
            {
                foreach (var tex in _data)
                {
                    if (tex.Value == null) continue;
                    data.data[DataMarker + tex.Key] = tex.Value.Data;
                }
            }
        }

        // auto dedupe and return the same id
        public int StoreTexture(byte[] tex)
        {
            if (tex == null) throw new ArgumentNullException(nameof(tex));
            lock (_data)
            {
                var existing = _data.FirstOrDefault(x => x.Value != null && x.Value.Data.SequenceEqual(tex));
                if (existing.Value != null)
                {
                    Console.WriteLine("StoreTexture - Texture already exists, reusing it");
                    return existing.Key;
                }

                // Use random ID instaed of sequential to help catch code using IDs that no longer exist
                for (var i = Random.Range(1000, 9990); ; i++)
                {
                    if (!_data.ContainsKey(i))
                    {
                        _data[i] = new TextureHolder(tex);
                        return i;
                    }
                }
            }
        }

        //public int StoreTexture(Texture2D tex)
        //{
        //    if (tex == null) throw new ArgumentNullException(nameof(tex));
        //    var rawTextureData = tex.GetRawTextureData();
        //    lock (_data)
        //    {
        //        var existing = _data.FirstOrDefault(x =>
        //            x.Value != null && x.Value.Texture.GetRawTextureData().SequenceEqual(rawTextureData));
        //        if (existing.Value != null) return existing.Key;
        //        return StoreTexture(tex.EncodeToPNG());
        //    }
        //}

        //todo debug mode - keep stack trace of last call and if current call sees that the texture was destroyed them show warning with the stack
        public Texture2D GetSharedTexture(int id)
        {
            lock (_data)
            {
                if (_data.TryGetValue(id, out var data))
                    return data?.Texture;
            }

            KoiSkinOverlayMgr.Logger.LogWarning("Tried getting texture with nonexisting ID: " + id);
            return null;
        }

        private sealed class TextureHolder : IDisposable
        {
            private byte[] _data;
            private Texture2D _texture;

            public TextureHolder(byte[] data)
            {
                Data = data ?? throw new ArgumentNullException(nameof(data));
            }

            public byte[] Data
            {
                get => _data;
                set
                {
                    Dispose();
                    _data = value;
                }
            }

            public Texture2D Texture
            {
                get
                {
                    if (_texture == null && _data != null)
                        _texture = Util.TextureFromBytes(_data, KoiSkinOverlayMgr.GetSelectedOverlayTexFormat(false));
                    return _texture;
                }
            }

            public void Dispose()
            {
                if (_texture != null)
                {
                    Object.Destroy(_texture);
                    _texture = null;
                }
            }
        }
    }
}