using GameTranslator.Core.Abstractions;

namespace GameTranslator.Providers.Offline;

/// <summary>
/// Optional fully-offline neural machine translation backed by a local ONNX model
/// (e.g. an OPUS-MT en→fa model exported to ONNX, run via Microsoft.ML.OnnxRuntime).
///
/// The model download is intentionally OPTIONAL so the base app stays small and light.
/// When no model file is configured, this provider is a safe no-op (returns the input
/// unchanged) and the pipeline falls back to the glossary or an AI provider.
///
/// Wiring a real model is a self-contained extension point: implement <see cref="LoadModel"/>
/// and <see cref="RunModel"/> against ONNX Runtime + a SentencePiece tokenizer, then set
/// <see cref="IsAvailable"/> to true. See docs/BUILD-WINDOWS.md → "Offline NMT".
/// </summary>
public sealed class OnnxNmtProvider : ITranslationProvider
{
    private readonly string _modelPath;

    public OnnxNmtProvider(string? modelPath)
    {
        _modelPath = modelPath ?? "";
        IsAvailable = !string.IsNullOrWhiteSpace(_modelPath) && File.Exists(_modelPath);
        // NOTE: when IsAvailable is true, call LoadModel() here once the ONNX integration is added.
    }

    public string Name => "Offline NMT (ONNX)";
    public bool RequiresNetwork => false;

    /// <summary>True once a local model is present and loaded.</summary>
    public bool IsAvailable { get; }

    public Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> lines, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        // No local model => no-op pass-through (the pipeline treats "== input" as "no translation").
        if (!IsAvailable)
            return Task.FromResult<IReadOnlyList<string>>(lines);

        var outp = new string[lines.Count];
        for (int i = 0; i < lines.Count; i++)
            outp[i] = RunModel(lines[i]) ?? lines[i];
        return Task.FromResult<IReadOnlyList<string>>(outp);
    }

    // --- Extension points for a real local model (left unimplemented by design) ---

    // private InferenceSession? _session;
    // private void LoadModel() { _session = new InferenceSession(_modelPath); /* + tokenizer */ }

    /// <summary>Translate a single line with the local model. Returns null when unavailable.</summary>
    private string? RunModel(string line) => null;
}
