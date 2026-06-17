using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Blogify.Tests;

public sealed class LocalizationResourceTests
{
    [Fact]
    public void EnglishAndTurkishResources_HaveSameKeys()
    {
        Dictionary<string, string> english = LoadResources("SharedResource.resx");
        Dictionary<string, string> turkish = LoadResources("SharedResource.tr.resx");

        Assert.Empty(english.Keys.Except(turkish.Keys).Order());
        Assert.Empty(turkish.Keys.Except(english.Keys).Order());
    }

    [Fact]
    public void LiteralLocalizerKeys_ExistInSharedResources()
    {
        HashSet<string> usedKeys = FindLiteralLocalizerKeys();
        Dictionary<string, string> english = LoadResources("SharedResource.resx");

        Assert.Empty(usedKeys.Except(english.Keys).Order());
    }

    [Fact]
    public void ResourceValues_AreNotBlank()
    {
        foreach ((string key, string value) in LoadResources("SharedResource.resx")
                     .Concat(LoadResources("SharedResource.tr.resx")))
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"Resource '{key}' has a blank value.");
        }
    }

    [Fact]
    public void EnglishAndTurkishResources_UseSameFormatPlaceholders()
    {
        Dictionary<string, string> english = LoadResources("SharedResource.resx");
        Dictionary<string, string> turkish = LoadResources("SharedResource.tr.resx");

        foreach (string key in english.Keys.Intersect(turkish.Keys))
        {
            string[] englishPlaceholders = FindFormatPlaceholders(english[key]);
            string[] turkishPlaceholders = FindFormatPlaceholders(turkish[key]);

            Assert.Equal(englishPlaceholders, turkishPlaceholders);
        }
    }

    private static Dictionary<string, string> LoadResources(string fileName)
    {
        string path = Path.Combine(ProjectRoot, "Blogify.Web", "Resources", fileName);
        XDocument document = XDocument.Load(path);

        return document
            .Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty);
    }

    private static HashSet<string> FindLiteralLocalizerKeys()
    {
        string webRoot = Path.Combine(ProjectRoot, "Blogify.Web");
        Regex localizerKeyPattern = new(@"\b[Ll]ocalizer\[""([^""]+)""", RegexOptions.Compiled);
        HashSet<string> keys = [];

        foreach (string file in Directory.EnumerateFiles(webRoot, "*.*", SearchOption.AllDirectories)
                     .Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                                    file.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string content = File.ReadAllText(file);
            foreach (Match match in localizerKeyPattern.Matches(content))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    private static string[] FindFormatPlaceholders(string value)
    {
        return Regex.Matches(value, @"\{(\d+)(?:[^}]*)\}")
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .Order()
            .ToArray();
    }

    private static string ProjectRoot
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Blogify.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new DirectoryNotFoundException("Could not locate the Blogify solution root.");
        }
    }
}
