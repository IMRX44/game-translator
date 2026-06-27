using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Cache;
using GameTranslator.Core.Models;
using GameTranslator.Core.Translation;
using Xunit;

namespace GameTranslator.Tests;

public class HybridTranslatorTests
{
    private static TranslationCache FreshCache() =>
        TranslationCache.Load(Path.Combine(Path.GetTempPath(), $"gt-{Guid.NewGuid():N}.json"));

    private static GlossaryProvider Glossary() => new(new Dictionary<string, string>
    {
        ["Play"] = "بازی",
        ["Settings"] = "تنظیمات"
    });

    /// <summary>Fake AI provider that upper-cases each line (stands in for a real translation).</summary>
    private sealed class FakeOnline : ITranslationProvider
    {
        public string Name => "fake";
        public bool RequiresNetwork => true;
        public int Calls { get; private set; }
        public Task<IReadOnlyList<string>> TranslateBatchAsync(
            IReadOnlyList<string> lines, string s, string t, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<string>>(lines.Select(l => $"AI:{l}").ToList());
        }
    }

    [Fact]
    public async Task Offline_mode_uses_glossary_only()
    {
        var h = new HybridTranslator(FreshCache(), Glossary());
        var result = await h.TranslateAsync(new[] { "Play", "Unknown" }, "en", "fa", TranslationMode.Offline);
        Assert.Equal(new[] { "بازی", "Unknown" }, result);
    }

    [Fact]
    public async Task Online_mode_calls_provider_for_uncached_lines()
    {
        var online = new FakeOnline();
        var h = new HybridTranslator(FreshCache(), Glossary(), online: online);
        var result = await h.TranslateAsync(new[] { "Hello" }, "en", "fa", TranslationMode.Online);
        Assert.Equal(new[] { "AI:Hello" }, result);
        Assert.Equal(1, online.Calls);
    }

    [Fact]
    public async Task Hybrid_surfaces_instant_offline_then_returns_ai()
    {
        var online = new FakeOnline();
        var h = new HybridTranslator(FreshCache(), Glossary(), online: online);

        IReadOnlyList<string>? instant = null;
        var final = await h.TranslateAsync(
            new[] { "Play" }, "en", "fa", TranslationMode.Hybrid, onInstant: r => instant = r);

        Assert.NotNull(instant);
        Assert.Equal(new[] { "بازی" }, instant!);   // glossary shown first
        Assert.Equal(new[] { "AI:Play" }, final);    // AI result returned
    }

    [Fact]
    public async Task Second_call_hits_cache_and_skips_provider()
    {
        var online = new FakeOnline();
        var cache = FreshCache();
        var h = new HybridTranslator(cache, Glossary(), online: online);

        await h.TranslateAsync(new[] { "Hello" }, "en", "fa", TranslationMode.Online);
        await h.TranslateAsync(new[] { "Hello" }, "en", "fa", TranslationMode.Online);

        Assert.Equal(1, online.Calls); // second call served from cache
    }

    [Fact]
    public async Task Auto_mode_falls_back_to_offline_without_provider()
    {
        var h = new HybridTranslator(FreshCache(), Glossary());
        var result = await h.TranslateAsync(new[] { "Play" }, "en", "fa", TranslationMode.Auto);
        Assert.Equal(new[] { "بازی" }, result);
    }
}
