using System.Net;
using System.Text;
using System.Text.Json;

namespace Blogify.Web.Services;

public interface IBlockNoteHtmlRenderer
{
    string Render(string blocksJson);
}

/// <summary>
/// Converts BlockNote block-array JSON to safe HTML for public rendering.
/// BlockNote stores content as a flat array of block objects, each with:
///   { id, type, props, content: InlineContent[], children: Block[] }
/// </summary>
public sealed class BlockNoteHtmlRenderer : IBlockNoteHtmlRenderer
{
    public string Render(string blocksJson)
    {
        if (string.IsNullOrWhiteSpace(blocksJson)) return string.Empty;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(blocksJson);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array) return string.Empty;

            JsonElement[] blocks = root.EnumerateArray().ToArray();
            StringBuilder sb = new();
            RenderBlocks(blocks, sb);
            return sb.ToString();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    // ─── Block-level rendering ────────────────────────────────────────────────

    private static void RenderBlocks(JsonElement[] blocks, StringBuilder sb)
    {
        int i = 0;
        while (i < blocks.Length)
        {
            string type = GetBlockType(blocks[i]);

            if (type == "bulletListItem")
            {
                sb.Append("<ul>\n");
                while (i < blocks.Length && GetBlockType(blocks[i]) == "bulletListItem")
                {
                    RenderListItem(blocks[i], sb, "ul");
                    i++;
                }
                sb.Append("</ul>\n");
            }
            else if (type == "numberedListItem")
            {
                sb.Append("<ol>\n");
                while (i < blocks.Length && GetBlockType(blocks[i]) == "numberedListItem")
                {
                    RenderListItem(blocks[i], sb, "ol");
                    i++;
                }
                sb.Append("</ol>\n");
            }
            else if (type == "checkListItem")
            {
                sb.Append("<ul class=\"checklist\">\n");
                while (i < blocks.Length && GetBlockType(blocks[i]) == "checkListItem")
                {
                    RenderCheckListItem(blocks[i], sb);
                    i++;
                }
                sb.Append("</ul>\n");
            }
            else
            {
                RenderBlock(blocks[i], sb);
                i++;
            }
        }
    }

    private static void RenderBlock(JsonElement block, StringBuilder sb)
    {
        string type = GetBlockType(block);

        switch (type)
        {
            case "paragraph":
                sb.Append("<p>");
                RenderInlineContent(block, sb);
                sb.Append("</p>\n");
                break;

            case "heading":
                int level = GetIntProp(block, "level") ?? 1;
                level = Math.Clamp(level, 1, 3);
                sb.Append($"<h{level}>");
                RenderInlineContent(block, sb);
                sb.Append($"</h{level}>\n");
                break;

            case "quote":
                sb.Append("<blockquote>\n<p>");
                RenderInlineContent(block, sb);
                sb.Append("</p>\n</blockquote>\n");
                break;

            case "codeBlock":
                string? language = GetStringProp(block, "language");
                string langAttr = string.IsNullOrEmpty(language) || language == "text"
                    ? string.Empty
                    : $" class=\"language-{WebUtility.HtmlEncode(language)}\"";
                sb.Append($"<pre><code{langAttr}>");
                RenderInlineContent(block, sb);
                sb.Append("</code></pre>\n");
                break;

            case "image":
                RenderImageBlock(block, sb);
                break;

            case "table":
                RenderTableBlock(block, sb);
                break;

            case "embed":
                RenderEmbedBlock(block, sb);
                break;

            case "pageBreak":
                sb.Append("<hr />\n");
                break;

            // file / video / audio: render a simple link or skip
            case "file":
            case "audio":
            case "video":
                RenderFileBlock(block, sb, type);
                break;
        }

        // Render any children that aren't list items (handled by callers already)
        if (block.TryGetProperty("children", out JsonElement children)
            && children.ValueKind == JsonValueKind.Array
            && children.GetArrayLength() > 0)
        {
            JsonElement[] childBlocks = children.EnumerateArray().ToArray();
            RenderBlocks(childBlocks, sb);
        }
    }

    private static void RenderListItem(JsonElement block, StringBuilder sb, string listTag)
    {
        sb.Append("<li>");
        RenderInlineContent(block, sb);

        if (block.TryGetProperty("children", out JsonElement children)
            && children.ValueKind == JsonValueKind.Array
            && children.GetArrayLength() > 0)
        {
            JsonElement[] childBlocks = children.EnumerateArray().ToArray();
            sb.Append($"\n<{listTag}>\n");
            foreach (JsonElement child in childBlocks)
            {
                RenderListItem(child, sb, listTag);
            }
            sb.Append($"</{listTag}>\n");
        }

        sb.Append("</li>\n");
    }

    private static void RenderCheckListItem(JsonElement block, StringBuilder sb)
    {
        bool checked_ = GetBoolProp(block, "checked") ?? false;
        string checkedAttr = checked_ ? " checked" : string.Empty;
        sb.Append("<li>");
        sb.Append($"<input type=\"checkbox\" disabled{checkedAttr} /> ");
        RenderInlineContent(block, sb);
        sb.Append("</li>\n");
    }

    private static void RenderImageBlock(JsonElement block, StringBuilder sb)
    {
        string url = GetStringProp(block, "url") ?? string.Empty;
        if (!IsSafeUrl(url)) return;

        string caption = GetStringProp(block, "caption") ?? string.Empty;
        int? previewWidth = GetIntProp(block, "previewWidth");

        string widthAttr = previewWidth.HasValue
            ? $" style=\"width:{previewWidth.Value}px;max-width:100%\""
            : " style=\"max-width:100%\"";

        sb.Append("<figure>\n");
        sb.Append($"<img src=\"{WebUtility.HtmlEncode(url)}\" alt=\"{WebUtility.HtmlEncode(caption)}\"{widthAttr} />\n");

        if (!string.IsNullOrEmpty(caption))
        {
            sb.Append($"<figcaption>{WebUtility.HtmlEncode(caption)}</figcaption>\n");
        }

        sb.Append("</figure>\n");
    }

    private static void RenderTableBlock(JsonElement block, StringBuilder sb)
    {
        if (!block.TryGetProperty("content", out JsonElement tableContent)) return;
        if (!tableContent.TryGetProperty("rows", out JsonElement rows)) return;
        if (rows.ValueKind != JsonValueKind.Array) return;

        sb.Append("<table>\n<tbody>\n");

        bool isFirstRow = true;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("cells", out JsonElement cells)) continue;
            if (cells.ValueKind != JsonValueKind.Array) continue;

            sb.Append("<tr>\n");
            foreach (JsonElement cell in cells.EnumerateArray())
            {
                string tag = isFirstRow ? "th" : "td";
                sb.Append($"<{tag}>");
                if (cell.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement inline in cell.EnumerateArray())
                    {
                        RenderInlineNode(inline, sb);
                    }
                }
                sb.Append($"</{tag}>");
            }
            sb.Append("\n</tr>\n");
            isFirstRow = false;
        }

        sb.Append("</tbody>\n</table>\n");
    }

    private static void RenderFileBlock(JsonElement block, StringBuilder sb, string type)
    {
        string url = GetStringProp(block, "url") ?? string.Empty;
        string name = GetStringProp(block, "name") ?? GetStringProp(block, "caption") ?? type;

        if (!IsSafeUrl(url)) return;

        if (type == "video" && IsTrustedEmbedProvider(url, out string provider, out string embedUrl))
        {
            sb.Append($"<figure class=\"blogify-embed blogify-embed-{provider}\">\n");
            sb.Append($"<iframe src=\"{WebUtility.HtmlEncode(embedUrl)}\" title=\"{WebUtility.HtmlEncode(name)}\" loading=\"lazy\" allowfullscreen referrerpolicy=\"strict-origin-when-cross-origin\"></iframe>\n");
            if (!string.IsNullOrEmpty(name) && name != type)
            {
                sb.Append($"<figcaption>{WebUtility.HtmlEncode(name)}</figcaption>\n");
            }
            sb.Append("</figure>\n");
            return;
        }

        sb.Append($"<p><a href=\"{WebUtility.HtmlEncode(url)}\" rel=\"noopener noreferrer\">{WebUtility.HtmlEncode(name)}</a></p>\n");
    }

    private static void RenderEmbedBlock(JsonElement block, StringBuilder sb)
    {
        string url = GetStringProp(block, "url") ?? string.Empty;
        string title = GetStringProp(block, "title") ?? url;
        if (!IsSafeUrl(url)) return;

        if (IsTrustedEmbedProvider(url, out string provider, out string embedUrl))
        {
            sb.Append($"<figure class=\"blogify-embed blogify-embed-{provider}\">\n");
            sb.Append($"<iframe src=\"{WebUtility.HtmlEncode(embedUrl)}\" title=\"{WebUtility.HtmlEncode(title)}\" loading=\"lazy\" allowfullscreen referrerpolicy=\"strict-origin-when-cross-origin\"></iframe>\n");
            if (!string.IsNullOrWhiteSpace(title) && title != url)
            {
                sb.Append($"<figcaption>{WebUtility.HtmlEncode(title)}</figcaption>\n");
            }
            sb.Append("</figure>\n");
            return;
        }

        sb.Append($"<p><a href=\"{WebUtility.HtmlEncode(url)}\" rel=\"noopener noreferrer\">{WebUtility.HtmlEncode(title)}</a></p>\n");
    }

    // ─── Inline content rendering ─────────────────────────────────────────────

    private static void RenderInlineContent(JsonElement block, StringBuilder sb)
    {
        if (!block.TryGetProperty("content", out JsonElement content)) return;
        if (content.ValueKind != JsonValueKind.Array) return;

        foreach (JsonElement node in content.EnumerateArray())
        {
            RenderInlineNode(node, sb);
        }
    }

    private static void RenderInlineNode(JsonElement node, StringBuilder sb)
    {
        if (!node.TryGetProperty("type", out JsonElement typeEl)) return;
        string type = typeEl.GetString() ?? string.Empty;

        switch (type)
        {
            case "text":
                RenderTextNode(node, sb);
                break;

            case "link":
                RenderLinkNode(node, sb);
                break;
        }
    }

    private static void RenderTextNode(JsonElement node, StringBuilder sb)
    {
        string text = node.TryGetProperty("text", out JsonElement textEl)
            ? (textEl.GetString() ?? string.Empty)
            : string.Empty;

        if (text.Length == 0) return;

        string content = WebUtility.HtmlEncode(text);

        if (node.TryGetProperty("styles", out JsonElement styles)
            && styles.ValueKind == JsonValueKind.Object)
        {
            content = ApplyInlineStyles(styles, content);
        }

        sb.Append(content);
    }

    private static void RenderLinkNode(JsonElement node, StringBuilder sb)
    {
        string href = node.TryGetProperty("href", out JsonElement hrefEl)
            ? (hrefEl.GetString() ?? "#")
            : "#";

        StringBuilder inner = new();
        if (node.TryGetProperty("content", out JsonElement linkContent)
            && linkContent.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in linkContent.EnumerateArray())
            {
                RenderInlineNode(child, inner);
            }
        }

        string safeHref = IsSafeUrl(href) ? href : "#";
        sb.Append($"<a href=\"{WebUtility.HtmlEncode(safeHref)}\" rel=\"noopener noreferrer\">{inner}</a>");
    }

    private static string ApplyInlineStyles(JsonElement styles, string content)
    {
        string result = content;

        if (GetStyleBool(styles, "bold")) result = $"<strong>{result}</strong>";
        if (GetStyleBool(styles, "italic")) result = $"<em>{result}</em>";
        if (GetStyleBool(styles, "underline")) result = $"<u>{result}</u>";
        if (GetStyleBool(styles, "strikethrough")) result = $"<s>{result}</s>";
        if (GetStyleBool(styles, "code")) result = $"<code>{result}</code>";

        string? textColor = styles.TryGetProperty("textColor", out JsonElement tcEl) ? tcEl.GetString() : null;
        if (!string.IsNullOrEmpty(textColor) && textColor != "default")
        {
            result = $"<span style=\"color:{WebUtility.HtmlEncode(textColor)}\">{result}</span>";
        }

        string? bgColor = styles.TryGetProperty("backgroundColor", out JsonElement bgEl) ? bgEl.GetString() : null;
        if (!string.IsNullOrEmpty(bgColor) && bgColor != "default")
        {
            result = $"<span style=\"background-color:{WebUtility.HtmlEncode(bgColor)}\">{result}</span>";
        }

        return result;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string GetBlockType(JsonElement block)
    {
        return block.TryGetProperty("type", out JsonElement t)
            ? (t.GetString() ?? string.Empty)
            : string.Empty;
    }

    private static int? GetIntProp(JsonElement block, string name)
    {
        if (!block.TryGetProperty("props", out JsonElement props)) return null;
        if (!props.TryGetProperty(name, out JsonElement val)) return null;
        return val.ValueKind == JsonValueKind.Number ? val.GetInt32() : null;
    }

    private static string? GetStringProp(JsonElement block, string name)
    {
        if (!block.TryGetProperty("props", out JsonElement props)) return null;
        if (!props.TryGetProperty(name, out JsonElement val)) return null;
        return val.ValueKind == JsonValueKind.String ? val.GetString() : null;
    }

    private static bool? GetBoolProp(JsonElement block, string name)
    {
        if (!block.TryGetProperty("props", out JsonElement props)) return null;
        if (!props.TryGetProperty(name, out JsonElement val)) return null;
        return val.ValueKind is JsonValueKind.True or JsonValueKind.False ? val.GetBoolean() : null;
    }

    private static bool GetStyleBool(JsonElement styles, string name)
    {
        if (!styles.TryGetProperty(name, out JsonElement val)) return false;
        return val.ValueKind == JsonValueKind.True;
    }

    private static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url.StartsWith("/", StringComparison.Ordinal)) return true;

        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool IsTrustedEmbedProvider(string url, out string provider, out string embedUrl)
    {
        provider = "link";
        embedUrl = string.Empty;

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return false;
        string host = uri.Host.ToLowerInvariant();

        if (host is "youtu.be")
        {
            string id = uri.AbsolutePath.Trim('/');
            if (IsSafeEmbedId(id))
            {
                provider = "youtube";
                embedUrl = $"https://www.youtube-nocookie.com/embed/{id}";
                return true;
            }
        }

        if (host.EndsWith("youtube.com", StringComparison.Ordinal) && uri.AbsolutePath == "/watch")
        {
            string? id = GetQueryValue(uri.Query, "v");
            if (IsSafeEmbedId(id))
            {
                provider = "youtube";
                embedUrl = $"https://www.youtube-nocookie.com/embed/{id}";
                return true;
            }
        }

        if (host.EndsWith("vimeo.com", StringComparison.Ordinal))
        {
            string id = uri.AbsolutePath.Trim('/').Split('/').LastOrDefault() ?? string.Empty;
            if (IsSafeEmbedId(id))
            {
                provider = "vimeo";
                embedUrl = $"https://player.vimeo.com/video/{id}";
                return true;
            }
        }

        if (host.EndsWith("spotify.com", StringComparison.Ordinal))
        {
            string path = uri.AbsolutePath.Trim('/');
            if (!string.IsNullOrEmpty(path) && path.All(c => char.IsLetterOrDigit(c) || c is '/' or '-' or '_'))
            {
                provider = "spotify";
                embedUrl = $"https://open.spotify.com/embed/{path}";
                return true;
            }
        }

        if (host.EndsWith("codepen.io", StringComparison.Ordinal))
        {
            string path = uri.AbsolutePath.Trim('/');
            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[^2] == "pen" && IsSafeEmbedId(parts[^1]))
            {
                provider = "codepen";
                embedUrl = $"https://codepen.io/{WebUtility.UrlEncode(parts[0])}/embed/{parts[^1]}";
                return true;
            }
        }

        return false;
    }

    private static bool IsSafeEmbedId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 128
            && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }

    private static string? GetQueryValue(string query, string name)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = part.Split('=', 2);
            if (pair.Length == 0 || pair[0] != name) continue;
            return pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace("+", " ", StringComparison.Ordinal)) : string.Empty;
        }

        return null;
    }
}
