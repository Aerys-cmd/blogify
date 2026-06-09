using System.Text;
using System.Text.Json;

namespace Blogify.Web.Services;

public static class BlockNoteContentExtractor
{
    /// <summary>
    /// Extracts all plain text from a BlockNote JSON block array.
    /// Returns an empty string if the input is null, empty, or not valid JSON.
    /// </summary>
    public static string ExtractPlainText(string blocksJson)
    {
        if (string.IsNullOrWhiteSpace(blocksJson)) return string.Empty;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(blocksJson);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array) return string.Empty;

            StringBuilder sb = new();
            ExtractFromBlocks(root, sb);
            return sb.ToString().Trim();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void ExtractFromBlocks(JsonElement blocks, StringBuilder sb)
    {
        foreach (JsonElement block in blocks.EnumerateArray())
        {
            ExtractFromBlock(block, sb);
        }
    }

    private static void ExtractFromBlock(JsonElement block, StringBuilder sb)
    {
        if (block.TryGetProperty("content", out JsonElement content))
        {
            ExtractFromInlineContent(content, sb);
        }

        if (block.TryGetProperty("children", out JsonElement children)
            && children.ValueKind == JsonValueKind.Array)
        {
            ExtractFromBlocks(children, sb);
        }
    }

    private static void ExtractFromInlineContent(JsonElement content, StringBuilder sb)
    {
        if (content.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement node in content.EnumerateArray())
            {
                ExtractFromInlineNode(node, sb);
            }
        }
    }

    private static void ExtractFromInlineNode(JsonElement node, StringBuilder sb)
    {
        if (!node.TryGetProperty("type", out JsonElement typeEl)) return;
        string type = typeEl.GetString() ?? string.Empty;

        if (type == "text")
        {
            if (node.TryGetProperty("text", out JsonElement textEl))
            {
                string? text = textEl.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    sb.Append(text).Append(' ');
                }
            }
        }
        else if (type == "link")
        {
            if (node.TryGetProperty("content", out JsonElement linkContent))
            {
                ExtractFromInlineContent(linkContent, sb);
            }
        }
    }
}
