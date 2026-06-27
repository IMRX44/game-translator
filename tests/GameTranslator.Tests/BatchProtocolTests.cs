using GameTranslator.Providers;
using Xunit;

namespace GameTranslator.Tests;

public class BatchProtocolTests
{
    private static readonly string[] Originals = { "Play", "Settings" };

    [Fact]
    public void Parses_clean_json_array()
    {
        var result = BatchTranslationProtocol.ParseArray("[\"بازی\",\"تنظیمات\"]", Originals);
        Assert.Equal(new[] { "بازی", "تنظیمات" }, result);
    }

    [Fact]
    public void Tolerates_code_fences_and_prose()
    {
        var text = "Here you go:\n```json\n[\"بازی\", \"تنظیمات\"]\n```";
        var result = BatchTranslationProtocol.ParseArray(text, Originals);
        Assert.Equal(new[] { "بازی", "تنظیمات" }, result);
    }

    [Fact]
    public void Falls_back_to_originals_on_garbage()
    {
        var result = BatchTranslationProtocol.ParseArray("not json at all", Originals);
        Assert.Equal(Originals, result);
    }

    [Fact]
    public void Pads_short_array_with_originals()
    {
        var result = BatchTranslationProtocol.ParseArray("[\"بازی\"]", Originals);
        Assert.Equal(new[] { "بازی", "Settings" }, result);
    }

    [Fact]
    public void Null_or_empty_returns_originals()
    {
        Assert.Equal(Originals, BatchTranslationProtocol.ParseArray(null, Originals));
        Assert.Equal(Originals, BatchTranslationProtocol.ParseArray("", Originals));
    }
}
