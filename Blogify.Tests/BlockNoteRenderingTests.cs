using Blogify.Web.Services;

namespace Blogify.Tests;

public sealed class BlockNoteRenderingTests
{
    [Fact]
    public void Renderer_RendersLinksEmojiTablesCaptionsDividerAndTrustedEmbed()
    {
        const string json = """
        [
          {
            "type": "paragraph",
            "content": [
              { "type": "text", "text": "Hello " },
              {
                "type": "link",
                "href": "https://example.com",
                "content": [{ "type": "text", "text": "external link 😊" }]
              }
            ],
            "children": []
          },
          { "type": "pageBreak", "props": {}, "children": [] },
          { "type": "image", "props": { "url": "/media/photo.webp", "caption": "Image caption" }, "children": [] },
          {
            "type": "table",
            "content": {
              "rows": [
                { "cells": [[{ "type": "text", "text": "Head" }]] },
                { "cells": [[{ "type": "text", "text": "Cell" }]] }
              ]
            },
            "children": []
          },
          {
            "type": "embed",
            "props": {
              "url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
              "title": "Video title",
              "provider": "youtube"
            },
            "children": []
          }
        ]
        """;

        string html = new BlockNoteHtmlRenderer().Render(json);

        Assert.Contains("<a href=\"https://example.com\"", html);
        Assert.Contains("external link", html);
        Assert.Contains("&#", html);
        Assert.Contains("<hr />", html);
        Assert.Contains("<figcaption>Image caption</figcaption>", html);
        Assert.Contains("<th>Head</th>", html);
        Assert.Contains("<td>Cell</td>", html);
        Assert.Contains("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", html);
        Assert.Contains("class=\"blogify-embed blogify-embed-youtube\"", html);
    }

    [Fact]
    public void Renderer_FallsBackUnknownEmbedsAndBlocksUnsafeUrls()
    {
        const string json = """
        [
          {
            "type": "embed",
            "props": { "url": "https://example.com/post", "title": "Source", "provider": "link" },
            "children": []
          },
          {
            "type": "paragraph",
            "content": [
              {
                "type": "link",
                "href": "javascript:alert(1)",
                "content": [{ "type": "text", "text": "bad" }]
              }
            ],
            "children": []
          },
          { "type": "image", "props": { "url": "javascript:alert(1)", "caption": "Bad" }, "children": [] }
        ]
        """;

        string html = new BlockNoteHtmlRenderer().Render(json);

        Assert.Contains("<a href=\"https://example.com/post\"", html);
        Assert.Contains(">Source</a>", html);
        Assert.Contains("<a href=\"#\"", html);
        Assert.DoesNotContain("javascript:alert", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void Extractor_IncludesLinksTableCaptionsAndEmbedMetadata()
    {
        const string json = """
        [
          {
            "type": "paragraph",
            "content": [
              { "type": "text", "text": "Intro" },
              {
                "type": "link",
                "href": "https://example.com",
                "content": [{ "type": "text", "text": "linked text" }]
              }
            ],
            "children": []
          },
          { "type": "image", "props": { "url": "/media/photo.webp", "caption": "Image caption" }, "children": [] },
          {
            "type": "table",
            "content": {
              "rows": [
                { "cells": [[{ "type": "text", "text": "Head" }]] },
                { "cells": [[{ "type": "text", "text": "Cell" }]] }
              ]
            },
            "children": []
          },
          {
            "type": "embed",
            "props": { "url": "https://example.com/embed", "title": "Embed title", "provider": "link" },
            "children": []
          }
        ]
        """;

        string text = BlockNoteContentExtractor.ExtractPlainText(json);

        Assert.Contains("Intro", text);
        Assert.Contains("linked text", text);
        Assert.Contains("Image caption", text);
        Assert.Contains("Head", text);
        Assert.Contains("Cell", text);
        Assert.Contains("Embed title", text);
        Assert.Contains("https://example.com/embed", text);
    }
}
