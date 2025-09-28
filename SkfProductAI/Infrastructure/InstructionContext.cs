namespace SkfProductAI.Infrastructure;

public class InstructionContext
{
    public string Text { get; }
    public InstructionContext()
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "INSTRUCTIONS.md");
        if (!File.Exists(path) && File.Exists("INSTRUCTIONS.md"))
            path = "INSTRUCTIONS.md"; // fallback relative
        try
        {
            Text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch { Text = string.Empty; }
    }
}