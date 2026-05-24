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
        { "✓", "em_success.svg" },
        { "🎉", "em_celebrate.svg" },
        { "🔓", "em_unlock.svg" },
        { "❌", "em_error.svg" },
        { "⚠", "em_warning.svg" }
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

        for (int i = startOffset; i < length; i++)
        {
            char c = document.GetCharAt(i);

            // standard 16-bit characters (fit in a single char)
            if (c == '✓' || c == '❌' || c == '⚠')
                return i;

            // surrogate pairs (emojis that require 2 chars in utf-16)
            if (char.IsHighSurrogate(c) && i + 1 < length)
            {
                char low = document.GetCharAt(i + 1);
                string pair = new string(new[] { c, low });

                if (pair == "🎉" || pair == "🔓")
                    return i;
            }
        }
        return -1;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        var document = CurrentContext.Document;
        char c = document.GetCharAt(offset);
        string emoji = c.ToString();
        int elementLength = 1;

        if (char.IsHighSurrogate(c) && offset + 1 < document.TextLength)
        {
            char low = document.GetCharAt(offset + 1);
            emoji = new string(new[] { c, low });
            elementLength = 2;
        }

        if (EmojiMap.TryGetValue(emoji, out string svgPath))
        {
            // multiply by 0.85 to comfortably fit within standard text ascent/descent
            // bounds, eliminating the upward line-height push
            var image = _loadIcon($"assets/emojis/{svgPath}", _iconSize * 0.85);

            image.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

            // replace negative top/bottom margin with a simple right margin
            // to space out the icon from following text safely without clipping bounds
            image.Margin = new Thickness(0, 0, 4, 0);

            return new InlineObjectElement(elementLength, image);
        }

        return null;
    }
}