using System.Net;
using System.Text;
using System.Text.Json;

namespace Blogify.Web.Services;

public interface ITiptapHtmlRenderer
{
    string Render(string tiptapJson);
}

public sealed class TiptapHtmlRenderer : ITiptapHtmlRenderer
{
    public string Render(string tiptapJson)
    {
        if (string.IsNullOrWhiteSpace(tiptapJson))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(tiptapJson);
            StringBuilder sb = new();
            RenderNode(doc.RootElement, sb);
            return sb.ToString();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void RenderNode(JsonElement node, StringBuilder sb)
    {
        if (!node.TryGetProperty("type", out JsonElement typeEl)) return;

        string type = typeEl.GetString() ?? string.Empty;

        switch (type)
        {
            case "doc":
                RenderChildren(node, sb);
                break;

            case "paragraph":
                sb.Append("<p>");
                RenderChildren(node, sb);
                sb.Append("</p>\n");
                break;

            case "heading":
                int level = TryGetIntAttr(node, "level") ?? 1;
                sb.Append($"<h{level}>");
                RenderChildren(node, sb);
                sb.Append($"</h{level}>\n");
                break;

            case "text":
                RenderTextNode(node, sb);
                break;

            case "hardBreak":
                sb.Append("<br />");
                break;

            case "horizontalRule":
                sb.Append("<hr />\n");
                break;

            case "bulletList":
                sb.Append("<ul>\n");
                RenderChildren(node, sb);
                sb.Append("</ul>\n");
                break;

            case "orderedList":
                sb.Append("<ol>\n");
                RenderChildren(node, sb);
                sb.Append("</ol>\n");
                break;

            case "listItem":
                sb.Append("<li>");
                RenderListItemContent(node, sb);
                sb.Append("</li>\n");
                break;

            case "blockquote":
                sb.Append("<blockquote>\n");
                RenderChildren(node, sb);
                sb.Append("</blockquote>\n");
                break;

            case "codeBlock":
                string? language = TryGetStringAttr(node, "language");
                string langAttr = string.IsNullOrEmpty(language)
                    ? string.Empty
                    : $" class=\"language-{WebUtility.HtmlEncode(language)}\"";
                sb.Append($"<pre><code{langAttr}>");
                RenderChildren(node, sb);
                sb.Append("</code></pre>\n");
                break;

            case "image":
                RenderImageNode(node, sb);
                break;

            case "table":
                sb.Append("<table>\n<tbody>\n");
                RenderChildren(node, sb);
                sb.Append("</tbody>\n</table>\n");
                break;

            case "tableRow":
                sb.Append("<tr>\n");
                RenderChildren(node, sb);
                sb.Append("</tr>\n");
                break;

            case "tableCell":
                sb.Append("<td>");
                RenderChildren(node, sb);
                sb.Append("</td>\n");
                break;

            case "tableHeader":
                sb.Append("<th>");
                RenderChildren(node, sb);
                sb.Append("</th>\n");
                break;
        }
    }

    private static void RenderTextNode(JsonElement node, StringBuilder sb)
    {
        string rawText = node.TryGetProperty("text", out JsonElement textEl)
            ? (textEl.GetString() ?? string.Empty)
            : string.Empty;

        string content = WebUtility.HtmlEncode(rawText);

        if (node.TryGetProperty("marks", out JsonElement marks))
        {
            foreach (JsonElement mark in marks.EnumerateArray())
            {
                string markType = mark.TryGetProperty("type", out JsonElement markTypeEl)
                    ? (markTypeEl.GetString() ?? string.Empty)
                    : string.Empty;

                content = ApplyMark(markType, mark, content);
            }
        }

        sb.Append(content);
    }

    private static string ApplyMark(string markType, JsonElement mark, string content)
    {
        return markType switch
        {
            "bold"   => $"<strong>{content}</strong>",
            "italic" => $"<em>{content}</em>",
            "strike" => $"<s>{content}</s>",
            "code"   => $"<code>{content}</code>",
            "link"   => BuildLinkMark(mark, content),
            _        => content,
        };
    }

    private static string BuildLinkMark(JsonElement mark, string content)
    {
        if (!mark.TryGetProperty("attrs", out JsonElement attrs)) return content;

        string href   = attrs.TryGetProperty("href",   out JsonElement hrefEl)   ? (hrefEl.GetString()   ?? "#") : "#";
        string target = attrs.TryGetProperty("target", out JsonElement targetEl) ? (targetEl.GetString() ?? string.Empty) : string.Empty;

        string relAttr    = target == "_blank" ? " rel=\"noopener noreferrer\"" : string.Empty;
        string targetAttr = string.IsNullOrEmpty(target) ? string.Empty : $" target=\"{WebUtility.HtmlEncode(target)}\"";

        return $"<a href=\"{WebUtility.HtmlEncode(href)}\"{targetAttr}{relAttr}>{content}</a>";
    }

    private static void RenderImageNode(JsonElement node, StringBuilder sb)
    {
        if (!node.TryGetProperty("attrs", out JsonElement attrs)) return;

        string src = attrs.TryGetProperty("src", out JsonElement srcEl) ? (srcEl.GetString() ?? string.Empty) : string.Empty;
        string alt = attrs.TryGetProperty("alt", out JsonElement altEl) ? (altEl.GetString() ?? string.Empty) : string.Empty;

        if (string.IsNullOrEmpty(src)) return;

        sb.Append($"<img src=\"{WebUtility.HtmlEncode(src)}\" alt=\"{WebUtility.HtmlEncode(alt)}\" />\n");
    }

    private static void RenderChildren(JsonElement node, StringBuilder sb)
    {
        if (!node.TryGetProperty("content", out JsonElement content)) return;

        foreach (JsonElement child in content.EnumerateArray())
        {
            RenderNode(child, sb);
        }
    }

    // List items wrap their first paragraph inline (no <p> tags) to match
    // standard HTML list rendering. Nested lists/paragraphs still render normally.
    private static void RenderListItemContent(JsonElement node, StringBuilder sb)
    {
        if (!node.TryGetProperty("content", out JsonElement content)) return;

        bool isFirst = true;
        foreach (JsonElement child in content.EnumerateArray())
        {
            if (isFirst
                && child.TryGetProperty("type", out JsonElement typeEl)
                && typeEl.GetString() == "paragraph")
            {
                RenderChildren(child, sb);
                isFirst = false;
            }
            else
            {
                RenderNode(child, sb);
                isFirst = false;
            }
        }
    }

    private static int? TryGetIntAttr(JsonElement node, string attrName)
    {
        if (!node.TryGetProperty("attrs", out JsonElement attrs)) return null;
        if (!attrs.TryGetProperty(attrName, out JsonElement val)) return null;
        return val.ValueKind == JsonValueKind.Number ? val.GetInt32() : null;
    }

    private static string? TryGetStringAttr(JsonElement node, string attrName)
    {
        if (!node.TryGetProperty("attrs", out JsonElement attrs)) return null;
        if (!attrs.TryGetProperty(attrName, out JsonElement val)) return null;
        return val.ValueKind == JsonValueKind.String ? val.GetString() : null;
    }
}
