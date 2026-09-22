using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal sealed record MessageLayout(IReadOnlyList<string[]> Pages, int Scale, TimeSpan Duration)
{
    internal const int MaxCharacters = 120;
    internal const int SecondsPerPage = 4;

    internal int PageAt(TimeSpan elapsed) => Math.Clamp(
        (int)(elapsed.TotalSeconds / Duration.TotalSeconds * Pages.Count), 0, Pages.Count - 1);

    internal IReadOnlyList<MatrixTextRun> TextRuns(TimeSpan elapsed, Rgba32 color)
    {
        var lines = Pages[PageAt(elapsed)];
        var lineHeight = RailDmiText.Height * Scale;
        var gap = Scale == 2 ? 4 : 3;
        var totalHeight = lines.Length * lineHeight + (lines.Length - 1) * gap;
        var top = ((Pages.Count > 1 ? 28 : 32) - totalHeight) / 2;
        return lines.Select((line, index) => new MatrixTextRun(line,
            (64 - RailDmiText.MeasureWidth(line) * Scale) / 2, top + index * (lineHeight + gap), color, Scale)).ToArray();
    }

    internal static bool TryCreate(string message, TimeSpan? duration, out MessageLayout layout, out string error)
    {
        layout = null!;
        error = "";
        if (string.IsNullOrWhiteSpace(message))
            error = "Message text is required.";
        else if (message.Trim().Length > MaxCharacters)
            error = $"Message too long. Maximum is {MaxCharacters} characters.";
        else if (duration is { } value && (value < TimeSpan.FromSeconds(1) || value > SceneTiming.MaxSceneDuration))
            error = "Duration must be between 1 and 20 seconds.";
        if (error.Length > 0) return false;

        var normalized = MatrixTextLayout.Normalize(message);
        if (normalized.Length == 0)
        {
            error = "Message must contain visible characters.";
            return false;
        }
        var largeLines = MatrixTextLayout.Wrap(normalized, MatrixTextLayout.TextWidth / 2);
        var useLarge = largeLines.Count <= 2 &&
            normalized.Split(' ').All(word => RailDmiText.MeasureWidth(word) <= MatrixTextLayout.TextWidth / 2);
        var pages = useLarge ? new[] { largeLines.ToArray() } :
            BalancePages(MatrixTextLayout.Wrap(normalized, MatrixTextLayout.TextWidth));
        var recommended = TimeSpan.FromSeconds(pages.Length * SecondsPerPage);
        if (recommended > SceneTiming.MaxSceneDuration)
        {
            error = "Message needs more than 20 seconds to read. Please split it into shorter messages.";
            return false;
        }
        if (pages.Length > 1 && duration < recommended)
        {
            error = $"This message needs at least {recommended.TotalSeconds:0} seconds for {pages.Length} pages.";
            return false;
        }
        layout = new MessageLayout(pages, useLarge ? 2 : 1, duration ?? recommended);
        return true;
    }

    internal static MessageLayout Create(string message, TimeSpan? duration)
    {
        if (TryCreate(message, duration, out var layout, out var error)) return layout;
        throw new ArgumentException(error, nameof(message));
    }

    internal static string[][] BalancePages(IReadOnlyList<string> lines)
    {
        var pageCount = (lines.Count + 2) / 3;
        var pages = new string[pageCount][];
        var used = 0;
        for (var page = 0; page < pageCount; page++)
        {
            // Avoid a final orphan line when the earlier pages can share the space.
            var count = (int)Math.Ceiling((lines.Count - used) / (double)(pageCount - page));
            if (page < pageCount - 1)
            {
                var remainingPages = pageCount - page - 1;
                var boundary = Enumerable.Range(1, Math.Min(3, lines.Count - used))
                    .Where(size => lines.Count - used - size >= remainingPages &&
                                   lines.Count - used - size <= remainingPages * 3 &&
                                   EndsWithAnySentenceMark(lines[used + size - 1]))
                    .OrderBy(size => Math.Abs(size - count)).FirstOrDefault();
                if (boundary > 0) count = boundary;
            }
            pages[page] = lines.Skip(used).Take(count).ToArray();
            used += count;
        }
        return pages;
    }

    private static bool EndsWithAnySentenceMark(string line) => line.Length > 0 && line[^1] is '.' or '!' or '?';
}
