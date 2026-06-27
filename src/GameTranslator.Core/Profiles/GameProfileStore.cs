using System.Text.Json;

namespace GameTranslator.Core.Profiles;

/// <summary>
/// Loads and saves <see cref="GameProfile"/>s as individual JSON files under a directory
/// (by default %AppData%/GameTranslator/profiles). Also resolves the best matching profile
/// for a running game.
/// </summary>
public sealed class GameProfileStore
{
    private readonly string _dir;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public GameProfileStore(string directory)
    {
        _dir = directory;
        Directory.CreateDirectory(_dir);
    }

    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameTranslator", "profiles");

    public IReadOnlyList<GameProfile> LoadAll()
    {
        var list = new List<GameProfile>();
        foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<GameProfile>(File.ReadAllText(file));
                if (profile is not null)
                    list.Add(profile);
            }
            catch
            {
                // Skip unreadable/corrupt profile files.
            }
        }
        return list;
    }

    public void Save(GameProfile profile)
    {
        var path = Path.Combine(_dir, SafeFileName(profile.Name) + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOpts));
    }

    public void Delete(string name)
    {
        var path = Path.Combine(_dir, SafeFileName(name) + ".json");
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>
    /// Returns the most specific profile that matches the given game, or null. A profile that
    /// constrains both process and title wins over one that constrains only one of them.
    /// </summary>
    public GameProfile? Resolve(string processName, string windowTitle)
    {
        GameProfile? best = null;
        int bestScore = -1;
        foreach (var p in LoadAll())
        {
            if (!p.Matches(processName, windowTitle)) continue;
            int score = (string.IsNullOrEmpty(p.ProcessName) ? 0 : 1) +
                        (string.IsNullOrEmpty(p.WindowTitleContains) ? 0 : 1);
            if (score > bestScore)
            {
                bestScore = score;
                best = p;
            }
        }
        return best;
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "profile" : name;
    }
}
