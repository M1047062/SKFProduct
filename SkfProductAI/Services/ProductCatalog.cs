using System.Text.Json;

namespace SkfProductAI.Services;

/// <summary>
/// Loads and serves product specification documents (JSON or CSV converted to JSON).
/// Supports loose matching for minor formatting differences in product designations.
/// </summary>
public class ProductCatalog
{
    public readonly Dictionary<string, JsonDocument> _products = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _folder;

    public ProductCatalog()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName ?? baseDir;
        _folder = Path.Combine(projectRoot, "Products");
        if (!Directory.Exists(_folder) && Directory.Exists("Products"))
            _folder = "Products"; // fallback relative
        Reload();
    }

    /// <summary>
    /// Re-scan disk for product files. Clears previous cache.
    /// </summary>
    public void Reload()
    {
        _products.Clear();
        if (!Directory.Exists(_folder)) return;
        foreach (var file in Directory.EnumerateFiles(_folder, "*.json", SearchOption.TopDirectoryOnly))
            LoadJson(file);
    }

    private void LoadJson(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var doc = JsonDocument.Parse(stream);
            string? designation = null;
            if (doc.RootElement.TryGetProperty("designation", out var desigProp))
                designation = desigProp.GetString();
            designation ??= Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(designation))
                _products[designation.Trim()] = doc;
        }
        catch
        { //error handling 
        }
    }

   
    public IEnumerable<string> Keys => _products.Keys;
}