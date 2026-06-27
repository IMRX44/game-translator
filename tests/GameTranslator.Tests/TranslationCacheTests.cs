using GameTranslator.Core.Cache;
using Xunit;

namespace GameTranslator.Tests;

public class TranslationCacheTests
{
    [Fact]
    public void Set_get_roundtrips_and_persists_across_reload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gt-cache-{Guid.NewGuid():N}.json");
        try
        {
            var cache = TranslationCache.Load(path);
            Assert.False(cache.TryGet("en", "fa", "Play", out _));

            cache.Set("en", "fa", "Play", "بازی");
            Assert.True(cache.TryGet("en", "fa", "Play", out var hit));
            Assert.Equal("بازی", hit);

            cache.Flush();

            // Reload from disk — persistence is the whole point (low API spend).
            var reloaded = TranslationCache.Load(path);
            Assert.True(reloaded.TryGet("en", "fa", "Play", out var hit2));
            Assert.Equal("بازی", hit2);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Different_language_pair_is_a_different_key()
    {
        var cache = TranslationCache.Load(Path.Combine(Path.GetTempPath(), $"gt-{Guid.NewGuid():N}.json"));
        cache.Set("en", "fa", "Play", "بازی");
        Assert.False(cache.TryGet("ja", "fa", "Play", out _));
    }
}
