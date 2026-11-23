using System.IO.Compression;
using System.Xml.Linq;

namespace Audibly.ReadAlong;

/// <summary>A chapter of the reader: where it starts in the passage list, and how deep it sits in the table of contents.</summary>
public sealed record ReadingChapter(string Title, int Depth, int FirstPassage);

/// <summary>Where the reader is: the chapter, and how far through the book and the chapter (0 to 1), by text length.</summary>
public sealed record ReadingPosition(int Chapter, double BookFraction, double ChapterFraction);

// Chapters for the read-along reader, from the EPUB's own table of contents.
public static class ReadingChapters
{
    private const int FallbackTitleLength = 60;

    /// <summary>
    ///     Chapters from the EPUB 3 nav document or the EPUB 2 NCX. Without a usable table of contents every EPUB
    ///     file is one chapter, titled by its first passage.
    /// </summary>
    public static Task<IReadOnlyList<ReadingChapter>> CreateAsync(AlignmentMap map, CancellationToken token = default) =>
        Task.Run(() =>
        {
            IReadOnlyList<ReadingChapter> chapters;
            try
            {
                chapters = FromTableOfContents(map, token);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or System.Xml.XmlException
                                          or UnauthorizedAccessException)
            {
                chapters = [];
            }

            return chapters.Count > 0 ? chapters : ByFile(map.Passages);
        }, token);

    public static ReadingPosition Locate(IReadOnlyList<Passage> passages, IReadOnlyList<ReadingChapter> chapters,
        int topPassage)
    {
        if (passages.Count == 0) return new ReadingPosition(0, 0, 0);
        var top = Math.Clamp(topPassage, 0, passages.Count - 1);
        var starts = new long[passages.Count + 1];
        for (var i = 0; i < passages.Count; i++) starts[i + 1] = starts[i] + passages[i].Text.Length;
        var total = starts[^1];
        var book = Fraction(starts[top], total);
        if (chapters.Count == 0) return new ReadingPosition(0, book, book);

        var chapter = 0;
        for (var i = 0; i < chapters.Count; i++)
            if (chapters[i].FirstPassage <= top) chapter = i;
        var from = starts[Math.Clamp(chapters[chapter].FirstPassage, 0, passages.Count)];
        var to = starts[ChapterEnd(passages, chapters, chapter)];
        return new ReadingPosition(chapter, book, Fraction(starts[top] - from, to - from));
    }

    /// <summary>The first passage of the chapter that has narration, or null when the chapter has none.</summary>
    public static int? FirstCueIndex(IReadOnlyList<Passage> passages, IReadOnlyList<ReadingChapter> chapters,
        int chapter)
    {
        if (chapter < 0 || chapter >= chapters.Count) return null;
        for (var i = Math.Max(0, chapters[chapter].FirstPassage); i < ChapterEnd(passages, chapters, chapter); i++)
            if (passages[i].Cues.Count > 0)
                return i;
        return null;
    }

    private static int ChapterEnd(IReadOnlyList<Passage> passages, IReadOnlyList<ReadingChapter> chapters,
        int chapter) =>
        Math.Clamp(chapter + 1 < chapters.Count ? chapters[chapter + 1].FirstPassage : passages.Count,
            0, passages.Count);

    private static double Fraction(double part, double whole) => whole > 0 ? Math.Clamp(part / whole, 0, 1) : 0;

    private static IReadOnlyList<ReadingChapter> ByFile(IReadOnlyList<Passage> passages)
    {
        var chapters = new List<ReadingChapter>();
        for (var i = 0; i < passages.Count; i++)
        {
            if (i > 0 && passages[i].Href == passages[i - 1].Href) continue;
            var title = string.Join(' ', passages[i].Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (title.Length > FallbackTitleLength) title = title[..FallbackTitleLength].TrimEnd() + "…";
            chapters.Add(new ReadingChapter(title, 0, i));
        }

        return chapters;
    }

    private static IReadOnlyList<ReadingChapter> FromTableOfContents(AlignmentMap map, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(map.Epub.Path);
        var container = Load(zip, "META-INF/container.xml");
        var opfPath = container?.Descendants().FirstOrDefault(e => e.Name.LocalName == "rootfile")
            ?.Attribute("full-path")?.Value;
        if (opfPath is null || Load(zip, opfPath) is not { } opf) return [];
        var opfFolder = Folder(opfPath);
        var items = opf.Descendants().Where(e => e.Name.LocalName == "item").ToList();

        var entries = new List<(string Title, int Depth, string File, string? Fragment)>();
        var nav = items.FirstOrDefault(e =>
            (e.Attribute("properties")?.Value ?? "").Split(' ').Contains("nav"));
        if (nav?.Attribute("href")?.Value is { } navHref)
        {
            var navPath = Resolve(opfFolder, navHref);
            var toc = Load(zip, navPath)?.Descendants().FirstOrDefault(e => e.Name.LocalName == "nav" &&
                e.Attributes().Any(a => a.Name.LocalName == "type" && a.Value.Split(' ').Contains("toc")));
            var list = toc?.Elements().FirstOrDefault(e => e.Name.LocalName == "ol");
            if (list is not null) WalkNav(list, 0, Folder(navPath), entries);
        }

        if (entries.Count == 0)
        {
            var ncxId = opf.Descendants().FirstOrDefault(e => e.Name.LocalName == "spine")?.Attribute("toc")?.Value;
            var ncxHref = items.FirstOrDefault(e => e.Attribute("id")?.Value == ncxId)?.Attribute("href")?.Value;
            if (ncxId is not null && ncxHref is not null)
            {
                var ncxPath = Resolve(opfFolder, ncxHref);
                var navMap = Load(zip, ncxPath)?.Descendants().FirstOrDefault(e => e.Name.LocalName == "navMap");
                if (navMap is not null) WalkNcx(navMap, 0, Folder(ncxPath), entries);
            }
        }

        var files = map.Passages.Select(p => Resolve(opfFolder, p.Href)).ToArray();
        var chapters = new List<ReadingChapter>();
        foreach (var (title, depth, file, fragment) in entries)
        {
            token.ThrowIfCancellationRequested();
            var first = Array.IndexOf(files, file);
            if (first < 0 || string.IsNullOrEmpty(title)) continue;
            var start = fragment is null ? first : FragmentPassage(zip, file, fragment, map.Passages, files, first);
            chapters.Add(new ReadingChapter(title, depth, start));
        }

        // reading order, one chapter per starting point
        return chapters.OrderBy(c => c.FirstPassage).DistinctBy(c => c.FirstPassage).ToList();
    }

    private static void WalkNav(XElement list, int depth, string folder,
        List<(string, int, string, string?)> entries)
    {
        foreach (var item in list.Elements().Where(e => e.Name.LocalName == "li"))
        {
            var link = item.Elements().FirstOrDefault(e => e.Name.LocalName == "a");
            if (link?.Attribute("href")?.Value is { } href)
                entries.Add((Clean(link.Value), depth, Resolve(folder, href), Fragment(href)));
            foreach (var nested in item.Elements().Where(e => e.Name.LocalName == "ol"))
                WalkNav(nested, depth + 1, folder, entries);
        }
    }

    private static void WalkNcx(XElement parent, int depth, string folder,
        List<(string, int, string, string?)> entries)
    {
        foreach (var point in parent.Elements().Where(e => e.Name.LocalName == "navPoint"))
        {
            var label = point.Elements().FirstOrDefault(e => e.Name.LocalName == "navLabel");
            var src = point.Elements().FirstOrDefault(e => e.Name.LocalName == "content")?.Attribute("src")?.Value;
            if (label is not null && src is not null)
                entries.Add((Clean(label.Value), depth, Resolve(folder, src), Fragment(src)));
            WalkNcx(point, depth + 1, folder, entries);
        }
    }

    /// <summary>
    ///     The passage holding the element with the given id. The passages carry the aligner's own ids, which the
    ///     published file does not have, so the element is found by its text position in the file.
    /// </summary>
    private static int FragmentPassage(ZipArchive zip, string file, string fragment, IReadOnlyList<Passage> passages,
        string[] files, int first)
    {
        var body = Load(zip, file)?.Descendants().FirstOrDefault(e => e.Name.LocalName == "body");
        if (body is null) return first;
        var target = -1;
        var length = 0;
        foreach (var node in body.DescendantNodes())
        {
            if (node is XElement element && element.Attribute("id")?.Value == fragment)
            {
                target = length;
                break;
            }

            if (node is XText text) length += text.Value.Length;
        }

        if (target < 0) return first;
        // the passage holding the anchor, or the first one after it: anchors often sit on the
        // whitespace before a heading, which belongs to no passage
        var bodyText = body.Value;
        var position = 0;
        for (var i = first; i < passages.Count && files[i] == file; i++)
        {
            if (passages[i].Text.Length == 0) continue;
            var at = bodyText.IndexOf(passages[i].Text, position, StringComparison.Ordinal);
            if (at < 0) continue;
            position = at + passages[i].Text.Length;
            if (position > target) return i;
        }

        return first;
    }

    private static XDocument? Load(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? zip.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName, path, StringComparison.OrdinalIgnoreCase));
        if (entry is null || entry.Length > 20 * 1024 * 1024) return null;
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static string Clean(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? Fragment(string href) =>
        href.IndexOf('#') is var hash and >= 0 && hash < href.Length - 1
            ? Uri.UnescapeDataString(href[(hash + 1)..])
            : null;

    private static string Folder(string path) =>
        path.LastIndexOf('/') is var slash and >= 0 ? path[..slash] : "";

    /// <summary>A zip path for an href relative to a folder in the zip: decoded, without fragment and "..".</summary>
    private static string Resolve(string folder, string href)
    {
        var target = Uri.UnescapeDataString(href.Split('#')[0]).Replace('\\', '/');
        var parts = new List<string>();
        foreach (var part in (target.StartsWith('/') ? target : folder + "/" + target).Split('/'))
            if (part == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
            }
            else if (part is not ("" or "."))
                parts.Add(part);

        return string.Join('/', parts);
    }
}
