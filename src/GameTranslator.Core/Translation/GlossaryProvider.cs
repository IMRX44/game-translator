using System.Text.Json;
using GameTranslator.Core.Abstractions;

namespace GameTranslator.Core.Translation;

/// <summary>
/// Instant, zero-cost offline translator backed by a phrasebook (assets/glossary/fa.json).
/// Great for the common, short UI strings games reuse everywhere ("Play", "Settings",
/// "Inventory", "Health"...). Lines with no entry are returned unchanged so a heavier
/// translator (local NMT or an AI provider) can fill the gaps.
/// </summary>
public sealed class GlossaryProvider : ITranslationProvider
{
    private readonly IReadOnlyDictionary<string, string> _map;

    public GlossaryProvider(IReadOnlyDictionary<string, string> map)
    {
        // Normalize keys for case-insensitive lookup.
        var norm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in map)
            norm[Normalize(kv.Key)] = kv.Value;
        _map = norm;
    }

    public string Name => "Offline Glossary";
    public bool RequiresNetwork => false;

    /// <summary>Load a glossary JSON file ({ "Play": "بازی", ... }). Missing file => empty.</summary>
    public static GlossaryProvider FromFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                if (data is not null)
                    return new GlossaryProvider(data);
            }
        }
        catch
        {
            // fall through to empty glossary
        }
        return new GlossaryProvider(new Dictionary<string, string>());
    }

    public Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> lines, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        var outp = new string[lines.Count];
        for (int i = 0; i < lines.Count; i++)
            outp[i] = TryTranslate(lines[i], out var fa) ? fa : lines[i];
        return Task.FromResult<IReadOnlyList<string>>(outp);
    }

    /// <summary>True if the whole line (normalized) has a glossary entry.</summary>
    public bool TryTranslate(string line, out string translation)
        => _map.TryGetValue(Normalize(line), out translation!);

    private static string Normalize(string s) => s.Trim().Trim('.', ':', '!', '?', '…', '-');
}
