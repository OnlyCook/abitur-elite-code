using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using System;
using System.Collections.Generic;

namespace AbiturEliteCode;

public class ConsoleColorizingTransformer : DocumentColorizingTransformer
{
    public List<(int StartOffset, int EndOffset, IBrush Color)> ColorSpans { get; } = new();

    protected override void ColorizeLine(DocumentLine line)
    {
        foreach (var span in ColorSpans)
        {
            if (span.StartOffset <= line.EndOffset && span.EndOffset >= line.Offset)
            {
                int start = Math.Max(span.StartOffset, line.Offset);
                int end = Math.Min(span.EndOffset, line.EndOffset);

                if (start < end)
                {
                    ChangeLinePart(start, end, element =>
                    {
                        element.TextRunProperties.SetForegroundBrush(span.Color);
                    });
                }
            }
        }
    }
}

public class EmojiElementGenerator : VisualLineElementGenerator
{
    private static readonly Dictionary<string, string> EmojiMap = new()
    {
        { "@S", "em_success.svg" },
        { "@P", "em_celebrate.svg" },
        { "@U", "em_unlock.svg" },
        { "@E", "em_error.svg" },
        { "@W", "em_warning.svg" }
    };

    private readonly Func<string, double, Image> _loadIcon;
    private readonly double _iconSize;

    public EmojiElementGenerator(Func<string, double, Image> loadIcon, double iconSize = 14)
    {
        _loadIcon = loadIcon;
        _iconSize = iconSize;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var document = CurrentContext.Document;
        int length = document.TextLength;

        // Need at least 2 characters to match the @-tags
        for (int i = startOffset; i < length - 1; i++)
        {
            char c = document.GetCharAt(i);

            if (c == '@')
            {
                char next = document.GetCharAt(i + 1);
                if (next == 'S' || next == 'P' || next == 'U' || next == 'E' || next == 'W')
                    return i;
            }
        }
        return -1;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        var document = CurrentContext.Document;

        if (offset + 1 < document.TextLength)
        {
            char c = document.GetCharAt(offset);
            char next = document.GetCharAt(offset + 1);

            if (c == '@')
            {
                string key = new string(new[] { c, next });

                if (EmojiMap.TryGetValue(key, out string svgPath))
                {
                    // multiply by 0.85 to comfortably fit within standard text ascent/descent
                    // bounds, eliminating the upward line-height push
                    var image = _loadIcon($"assets/emojis/{svgPath}", _iconSize * 0.85);

                    image.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

                    // replace negative top/bottom margin with a simple right margin
                    // to space out the icon from following text safely without clipping bounds
                    image.Margin = new Thickness(0, 0, 4, 0);

                    // Consumes 2 characters in the document
                    return new InlineObjectElement(2, image);
                }
            }
        }

        return null;
    }
}