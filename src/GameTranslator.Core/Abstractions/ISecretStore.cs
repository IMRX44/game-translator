namespace GameTranslator.Core.Abstractions;

/// <summary>
/// Stores API keys at rest. On Windows this is backed by DPAPI (per-user encryption)
/// so keys are never written to disk in plain text.
/// </summary>
public interface ISecretStore
{
    void Save(string key, string secret);
    string? Load(string key);
    void Delete(string key);
    bool Has(string key);
}
