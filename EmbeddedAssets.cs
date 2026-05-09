using System.Text;

namespace MermaidViewer;

internal static class EmbeddedAssets
{
    public static string MermaidMinJs { get; } = ReadTextResource("MermaidViewer.Assets.mermaid.min.js");

    private static string ReadTextResource(string name)
    {
        var asm = typeof(EmbeddedAssets).Assembly;
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Risorsa incorporata non trovata: {name}");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}

