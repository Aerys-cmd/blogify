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
            if (content.ValueKind == JsonValueKind.Object
                && content.TryGetProperty("rows", out JsonElement rows)
                && rows.ValueKind == JsonValueKind.Array)
            {
                ExtractFromTableRows(rows, sb);
            }
            else
            {
                ExtractFromInlineContent(content, sb);
            }
        }

        ExtractStringProp(block, "caption", sb);
        ExtractStringProp(block, "name", sb);

        string type = block.TryGetProperty("type", out JsonElement typeEl)
            ? typeEl.GetString() ?? string.Empty
            : string.Empty;

        if (type == "embed")
        {
            ExtractStringProp(block, "title", sb);
            ExtractStringProp(block, "url", sb);
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

    private static void ExtractFromTableRows(JsonElement rows, StringBuilder sb)
    {
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("cells", out JsonElement cells)
                || cells.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement cell in cells.EnumerateArray())
            {
                if (cell.ValueKind == JsonValueKind.Array)
                {
                    ExtractFromInlineContent(cell, sb);
                }
                else if (cell.ValueKind == JsonValueKind.Object
                    && cell.TryGetProperty("content", out JsonElement cellContent))
                {
                    ExtractFromInlineContent(cellContent, sb);
                }
            }
        }
    }

    private static void ExtractStringProp(JsonElement block, string name, StringBuilder sb)
    {
        if (!block.TryGetProperty("props", out JsonElement props)) return;
        if (!props.TryGetProperty(name, out JsonElement value)) return;
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            sb.Append(text).Append(' ');
        }
    }
}
