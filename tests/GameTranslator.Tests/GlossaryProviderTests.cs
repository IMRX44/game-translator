using GameTranslator.Core.Translation;
using Xunit;

namespace GameTranslator.Tests;

public class GlossaryProviderTests
{
    private static GlossaryProvider Make() => new(new Dictionary<string, string>
    {
        ["Play"] = "بازی",
        ["Settings"] = "تنظیمات",
        ["Health"] = "سلامتی"
    });

    [Fact]
    public async Task Translates_known_terms_and_passes_through_unknown()
    {
        var g = Make();
        var result = await g.TranslateBatchAsync(new[] { "Play", "xyzzy", "Settings" }, "en", "fa");
        Assert.Equal(new[] { "بازی", "xyzzy", "تنظیمات" }, result);
    }

    [Theory]
    [InlineData("play", true)]      // case-insensitive
    [InlineData("  Play  ", true)]  // trimmed
    [InlineData("Play!", true)]     // trailing punctuation stripped
    [InlineData("Player", false)]   // not a whole-line match
    public void TryTranslate_normalizes_input(string input, bool expected)
    {
        var ok = Make().TryTranslate(input, out _);
        Assert.Equal(expected, ok);
    }

    [Fact]
    public async Task Empty_input_returns_empty()
    {
        var result = await Make().TranslateBatchAsync(Array.Empty<string>(), "en", "fa");
        Assert.Empty(result);
    }
}
