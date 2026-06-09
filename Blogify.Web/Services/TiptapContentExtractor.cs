using System.Text;
using System.Text.Json;

namespace Blogify.Web.Services;

public static class TiptapContentExtractor
{
    public static string ExtractPlainText(string tiptapJson)
    {
        if (string.IsNullOrWhiteSpace(tiptapJson)) return string.Empty;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(tiptapJson);
            StringBuilder sb = new();
            ExtractTextNodes(doc.RootElement, sb);
            return sb.ToString().Trim();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void ExtractTextNodes(JsonElement element, StringBuilder sb)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out JsonElement typeEl)
                && typeEl.GetString() == "text"
                && element.TryGetProperty("text", out JsonElement textEl))
            {
                string? text = textEl.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    sb.Append(text).Append(' ');
                }
            }

            if (element.TryGetProperty("content", out JsonElement contentEl))
            {
                ExtractTextNodes(contentEl, sb);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                ExtractTextNodes(child, sb);
            }
        }
    }
}
