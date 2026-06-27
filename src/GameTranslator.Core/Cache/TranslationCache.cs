using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameTranslator.Core.Cache;

/// <summary>
/// Persistent translation memory. Keyed by a hash of (sourceLang|targetLang|text) so the
/// same line is never re-translated — across sessions — which keeps AI API usage (and cost)
/// low. Backed by a JSON file that is loaded once and flushed in the background.
/// </summary>
public sealed class TranslationCache
{
    private readonly string _path;
    private readonly ConcurrentDictionary<string, string> _map;
    private readonly object _flushLock = new();
    private int _dirty;

    private TranslationCache(string path, ConcurrentDictionary<string, string> map)
    {
        _path = path;
        _map = map;
    }

    /// <summary>Load (or create) a cache file at <paramref name="path"/>.</summary>
    public static TranslationCache Load(string path)
    {
        ConcurrentDictionary<string, string> map = new();
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data is not null)
                    map = new ConcurrentDictionary<string, string>(data);
            }
        }
        catch
        {
            // Corrupt cache shouldn't break the app — start fresh.
            map = new ConcurrentDictionary<string, string>();
        }
        return new TranslationCache(path, map);
    }

    public int Count => _map.Count;

    public static string Key(string sourceLang, string targetLang, string text)
    {
        var bytes = Encoding.UTF8.GetBytes($"{sourceLang}{targetLang}{text}");
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash, 0, 12); // 24 hex chars is plenty to avoid collisions here
    }

    public bool TryGet(string sourceLang, string targetLang, string text, out string translation)
        => _map.TryGetValue(Key(sourceLang, targetLang, text), out translation!);

    public void Set(string sourceLang, string targetLang, string text, string translation)
    {
        _map[Key(sourceLang, targetLang, text)] = translation;
        Interlocked.Exchange(ref _dirty, 1);
    }

    /// <summary>Write to disk if there are unsaved changes. Safe to call frequently.</summary>
    public void Flush()
    {
        if (Interlocked.Exchange(ref _dirty, 0) == 0)
            return;

        lock (_flushLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var tmp = _path + ".tmp";
                var json = JsonSerializer.Serialize(
                    new Dictionary<string, string>(_map),
                    new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(tmp, json);
                File.Move(tmp, _path, overwrite: true);
            }
            catch
            {
                // Best-effort persistence; mark dirty again so we retry next time.
                Interlocked.Exchange(ref _dirty, 1);
            }
        }
    }

    public void Clear()
    {
        _map.Clear();
        Interlocked.Exchange(ref _dirty, 1);
        Flush();
    }
}
