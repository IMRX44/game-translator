using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Cache;
using GameTranslator.Core.Models;

namespace GameTranslator.Core.Translation;

/// <summary>
/// Orchestrates the translation layers and the persistent cache. This is the brain behind
/// the four <see cref="TranslationMode"/>s, including the "smart hybrid" mode that shows the
/// instant offline result first and then upgrades it with the AI provider's answer.
/// </summary>
public sealed class HybridTranslator
{
    private readonly TranslationCache _cache;
    private readonly GlossaryProvider _glossary;
    private readonly ITranslationProvider? _offlineNmt;
    private readonly ITranslationProvider? _online;

    public HybridTranslator(
        TranslationCache cache,
        GlossaryProvider glossary,
        ITranslationProvider? offlineNmt = null,
        ITranslationProvider? online = null)
    {
        _cache = cache;
        _glossary = glossary;
        _offlineNmt = offlineNmt;
        _online = online;
    }

    /// <summary>True when an AI provider is configured.</summary>
    public bool HasOnline => _online is not null;

    /// <summary>
    /// Translate a batch of lines according to <paramref name="mode"/>.
    /// In <see cref="TranslationMode.Hybrid"/>, <paramref name="onInstant"/> is invoked with the
    /// offline result before the (slower) AI result is awaited and returned.
    /// </summary>
    public async Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> lines,
        string sourceLang,
        string targetLang,
        TranslationMode mode,
        Action<IReadOnlyList<string>>? onInstant = null,
        CancellationToken ct = default)
    {
        if (lines.Count == 0)
            return Array.Empty<string>();

        // 1) Seed every slot from the persistent cache.
        var result = new string?[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                result[i] = lines[i];
            else if (_cache.TryGet(sourceLang, targetLang, lines[i], out var hit))
                result[i] = hit;
        }

        bool useOnline = mode switch
        {
            TranslationMode.Online => _online is not null,
            TranslationMode.Auto => _online is not null,
            TranslationMode.Hybrid => _online is not null,
            _ => false
        };

        // 2) Offline pass (glossary, then local NMT). Always runs except pure-Online mode,
        //    because it's the instant layer shown first in Hybrid and the fallback otherwise.
        bool runOffline = mode != TranslationMode.Online || _online is null;
        if (runOffline)
        {
            await FillAsync(result, lines, _glossary, sourceLang, targetLang, cacheWrites: !useOnline, ct);
            if (_offlineNmt is not null)
                await FillAsync(result, lines, _offlineNmt, sourceLang, targetLang, cacheWrites: !useOnline, ct);
        }

        // In hybrid mode, surface the instant offline result before waiting on the network.
        if (mode == TranslationMode.Hybrid && useOnline)
            onInstant?.Invoke(Materialize(result, lines));

        // 3) Online pass. Refines/fills using the AI provider and writes results to the cache.
        if (useOnline && _online is not null)
        {
            // In Hybrid/Online we want the AI translation for every non-empty, non-cached line.
            var indices = new List<int>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                if (_cache.TryGet(sourceLang, targetLang, lines[i], out var hit))
                {
                    result[i] = hit;
                    continue;
                }
                indices.Add(i);
            }

            if (indices.Count > 0)
            {
                var subset = indices.Select(i => lines[i]).ToList();
                try
                {
                    var translated = await _online.TranslateBatchAsync(subset, sourceLang, targetLang, ct);
                    for (int k = 0; k < indices.Count && k < translated.Count; k++)
                    {
                        var src = subset[k];
                        var dst = translated[k];
                        result[indices[k]] = dst;
                        if (!string.IsNullOrWhiteSpace(dst) && dst != src)
                            _cache.Set(sourceLang, targetLang, src, dst);
                    }
                }
                catch when (mode is TranslationMode.Auto or TranslationMode.Hybrid)
                {
                    // Network/provider failure in a forgiving mode: keep the offline result.
                }
            }
        }

        _cache.Flush();
        return Materialize(result, lines);
    }

    /// <summary>Fill still-empty slots using a provider; optionally persist to the cache.</summary>
    private async Task FillAsync(
        string?[] result, IReadOnlyList<string> lines, ITranslationProvider provider,
        string sourceLang, string targetLang, bool cacheWrites, CancellationToken ct)
    {
        var indices = new List<int>();
        for (int i = 0; i < result.Length; i++)
            if (result[i] is null) indices.Add(i);
        if (indices.Count == 0) return;

        var subset = indices.Select(i => lines[i]).ToList();
        IReadOnlyList<string> translated;
        try
        {
            translated = await provider.TranslateBatchAsync(subset, sourceLang, targetLang, ct);
        }
        catch
        {
            return; // offline providers shouldn't throw, but never let them break the pipeline
        }

        for (int k = 0; k < indices.Count && k < translated.Count; k++)
        {
            var src = subset[k];
            var dst = translated[k];
            // A provider returns the original string when it has no translation; treat that as "still empty".
            if (string.IsNullOrEmpty(dst) || dst == src)
                continue;
            result[indices[k]] = dst;
            if (cacheWrites)
                _cache.Set(sourceLang, targetLang, src, dst);
        }
    }

    private static IReadOnlyList<string> Materialize(string?[] result, IReadOnlyList<string> lines)
    {
        var final = new string[result.Length];
        for (int i = 0; i < result.Length; i++)
            final[i] = result[i] ?? lines[i]; // untranslated => show original
        return final;
    }
}
