using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using StackExchange.Redis;
using SkfProductAI.Infrastructure;

namespace SkfProductAI.Services;

/// <summary>
/// Handles natural language product attribute questions by:
/// 1. Finds the product and attribute 
/// 2. Loads the json files present in the folder.
/// 3. Returns a answer w.r.t the attribute.
/// </summary>
public class QueryHandler
{
    private readonly Kernel? _kernel;
    private readonly ProductCatalog _catalog;
    private readonly IConnectionMultiplexer? _redis;
    private readonly InstructionContext _instructions;

    public QueryHandler(Kernel? kernel, ProductCatalog catalog, IConnectionMultiplexer? redis, InstructionContext instructions)
    {
        _kernel = kernel; _catalog = catalog; _redis = redis; _instructions = instructions;
    }

    public async Task<string> AnswerAsync(string question, CancellationToken ct = default)
    {
        if (_kernel is null) return "OpenAI not configured.";
        if (string.IsNullOrWhiteSpace(question)) return "Empty question.";

        var cacheKey = $"qa:{question.Trim().ToLowerInvariant()}";
        if (_redis is not null)
        {
            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync(cacheKey);
            if (cached.HasValue) return cached!;
        }

        var allowedProducts = string.Join(", ", _catalog.Keys);
        var extractionPrompt = BuildPrompt(allowedProducts, question);

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(extractionPrompt);
        var result = await chat.GetChatMessageContentAsync(history, cancellationToken: ct);

        if (!TryParseExtraction(result.Content, out var product, out var attribute))
            return "I could not parse the model response.";

        if (string.IsNullOrWhiteSpace(product))
            return "Invalid Product.";

        product = product.Trim();
        _catalog.Reload(); // ensure latest files are there 

        var jsonFile = _catalog._products[product];

        if (jsonFile is null)
            return $"No Json File Avaialbel for the product {product}";
        if (string.IsNullOrWhiteSpace(attribute))
            return "Attribute value is missing.";

        string attrLower = attribute.ToLowerInvariant().Trim();
        string? answer = null; // initialize

            var attrPrompt = BuildAttributeExtractionPrompt(attribute, jsonFile.RootElement);
            var attrHistory = new ChatHistory();
            attrHistory.AddSystemMessage(attrPrompt);
            try
            {
                var attrResult = await chat.GetChatMessageContentAsync(attrHistory, cancellationToken: ct);
                if (TryParseSingleValue(attrResult.Content, out var extracted) && !string.IsNullOrWhiteSpace(extracted))
                {
                    answer = extracted.Trim();
                }
            }
            catch
            {
                //error handling 
            }
        

        if (string.IsNullOrWhiteSpace(answer))
            return $"I can't find that attribute {attribute} for the product {product}.";

        var final = $"The {attribute.Trim()} of the {product} bearing is {answer}.";
        if (_redis is not null)
            _ = _redis.GetDatabase().StringSetAsync(cacheKey, final, TimeSpan.FromMinutes(10));
        return final;
    }

    private string BuildPrompt(string allowedProducts, string question)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an information extraction engine. Extract the following from the user question:");
        sb.AppendLine("- product: Must be EXACTLY one of: " + allowedProducts + " (case insensitive match). If none present or if product given as 6205NP, 6205 LP, 6205 P it should  return null. Product should exactly match from the allowed products allowedProducts strictly.");
        sb.AppendLine("- attribute: Extract the attribute term/phrase directly from the question (e.g., tolerance class, bore diameter, reference speed). If none present return null.");
        sb.AppendLine("Return strictly JSON: {\"product\":string|null,\"attribute\":string|null}");
        sb.AppendLine("Question: \"" + question + "\"");
        sb.AppendLine("JSON:");
        return sb.ToString();
    }


    private static bool TryParseExtraction(string? content, out string? product, out string? attribute)
    {
        product = null; attribute = null;
        if (string.IsNullOrWhiteSpace(content)) return false;
        try
        {
            using var parsed = JsonDocument.Parse(content);
            if (parsed.RootElement.TryGetProperty("product", out var p)) product = p.GetString();
            if (parsed.RootElement.TryGetProperty("attribute", out var a)) attribute = a.GetString();
            return true;
        }
        catch { return false; }
    }
   

    // Helper to build second-call prompt for attribute extraction
    private static string BuildAttributeExtractionPrompt(string attribute, JsonElement productRoot)
    {
        var raw = productRoot.GetRawText();
  
        var sb = new StringBuilder();
        sb.AppendLine("You are a precise product specification value extractor.");
        sb.AppendLine("Given the JSON spec and a target attribute name, output strictly JSON: {\"value\": string|null}");
        sb.AppendLine("Rules:");
        sb.AppendLine("- Find the attribute by matching name or symbol case-insensitively.");
        sb.AppendLine("- If a unit field exists for the value, append a space and the unit (e.g., '15 mm').");
        sb.AppendLine("- If no unit, just return the raw value string.");
        sb.AppendLine("- If not found with high confidence, return {\"value\": null}.");
        sb.AppendLine("- Do NOT invent or guess numbers.");
        sb.AppendLine($"Attribute: {attribute}");
        sb.AppendLine("ProductJson:");
        sb.AppendLine(raw);
        sb.AppendLine("JSON only response:");
        return sb.ToString();
    }

    private static bool TryParseSingleValue(string? content, out string? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(content)) return false;
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("value", out var v))
            {
                if (v.ValueKind == JsonValueKind.String) value = v.GetString();
                else if (v.ValueKind == JsonValueKind.Number || v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) value = v.ToString();
                else if (v.ValueKind == JsonValueKind.Null) value = null;
            }
            return true;
        }
        catch
        {
            // fallback: if model ignored JSON request and returned a short plain value
            var trimmed = content.Trim();
            if (!trimmed.StartsWith("{") && trimmed.Length < 80)
            {
                value = trimmed;
                return true;
            }
            return false;
        }
    }
}