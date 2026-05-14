using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AbiturEliteCode;

public static class MarkdownRenderer
{
    private static readonly Regex MarkdownInlineRegex = new Regex(
        @"(?<bold>\*\*(?<boldtext>.*?)\*\*)|(?<underline>__(?<underlinetext>.*?)__)|(?<italic>_(?<italictext>.*?)_)|(?<kbd><kbd>(?<kbdtext>.*?)</kbd>)|(?<code>`(?<codetext>.*?)`)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static void RenderMarkdownToPanel(StackPanel panel, string text, bool isSqlMode = false, bool useSelectableText = true)
    {
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        bool inCodeBlock = false;
        StringBuilder codeBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    inCodeBlock = false;
                    var codeContent = codeBuilder.ToString().TrimEnd('\r', '\n');
                    panel.Children.Add(CreateCodeBlock(codeContent, isSqlMode));
                    codeBuilder.Clear();
                }
                else
                {
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBuilder.AppendLine(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                panel.Children.Add(new Control { Height = 8 });
                continue;
            }

            string trimmedLine = line.TrimStart();
            int headerLevel = 0;
            bool isList = false;
            string processedLine = line;

            if (trimmedLine.StartsWith("### "))
            {
                headerLevel = 3;
                processedLine = trimmedLine.Substring(4);
            }
            else if (trimmedLine.StartsWith("## "))
            {
                headerLevel = 2;
                processedLine = trimmedLine.Substring(3);
            }
            else if (trimmedLine.StartsWith("# "))
            {
                headerLevel = 1;
                processedLine = trimmedLine.Substring(2);
            }
            else if (trimmedLine.StartsWith("- "))
            {
                isList = true;
                processedLine = trimmedLine.Substring(2);
            }

            TextBlock textBlock = useSelectableText
                ? new SelectableTextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 2)
                }
                : new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.LightGray,
                    Margin = new Thickness(0, 2)
                }; // cursed

            if (headerLevel == 1)
            {
                textBlock.FontSize = 18;
                textBlock.FontWeight = FontWeight.Bold;
                textBlock.Margin = new Thickness(0, 10, 0, 5);
            }
            else if (headerLevel == 2)
            {
                textBlock.FontSize = 16;
                textBlock.FontWeight = FontWeight.SemiBold;
                textBlock.Margin = new Thickness(0, 8, 0, 4);
                if (!useSelectableText) textBlock.Foreground = Brushes.White;
            }
            else if (headerLevel == 3)
            {
                textBlock.FontSize = 14;
                textBlock.FontWeight = FontWeight.Medium;
                textBlock.Margin = new Thickness(0, 6, 0, 2);
                if (!useSelectableText) textBlock.Foreground = SolidColorBrush.Parse("#B0B0B0");
            }

            if (isList)
            {
                textBlock.Margin = new Thickness(15, 0, 0, 0);
                textBlock.Inlines!.Add(new Run("• ")
                {
                    FontWeight = FontWeight.Bold
                });
            }

            ParseInlines(textBlock.Inlines!, processedLine);
            panel.Children.Add(textBlock);
        }

        if (inCodeBlock && codeBuilder.Length > 0)
        {
            panel.Children.Add(CreateCodeBlock(codeBuilder.ToString().TrimEnd('\r', '\n'), isSqlMode));
        }
    }

    private static void ParseInlines(InlineCollection inlines, string text)
    {
        int currentIndex = 0;

        foreach (Match match in MarkdownInlineRegex.Matches(text))
        {
            if (match.Index > currentIndex)
                inlines.Add(new Run(text.Substring(currentIndex, match.Index - currentIndex)));

            if (match.Groups["bold"].Success)
            {
                var bold = new Bold();
                bold.Inlines.Add(new Run(match.Groups["boldtext"].Value));
                inlines.Add(bold);
            }
            else if (match.Groups["underline"].Success)
            {
                var underline = new Underline();
                underline.Inlines.Add(new Run(match.Groups["underlinetext"].Value));
                inlines.Add(underline);
            }
            else if (match.Groups["italic"].Success)
            {
                var italic = new Italic();
                italic.Inlines.Add(new Run(match.Groups["italictext"].Value));
                inlines.Add(italic);
            }
            else if (match.Groups["kbd"].Success)
            {
                inlines.Add(CreateInlineBadge(match.Groups["kbdtext"].Value, "#3C3C3C", "#555555", Brushes.White));
            }
            else if (match.Groups["code"].Success)
            {
                inlines.Add(CreateInlineBadge(match.Groups["codetext"].Value, "#2D2D30", "Transparent", SolidColorBrush.Parse("#DCDCAA")));
            }

            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < text.Length)
            inlines.Add(new Run(text.Substring(currentIndex)));
    }

    private static InlineUIContainer CreateInlineBadge(string text, string bgColor, string borderColor, IBrush fgColor)
    {
        var border = new Border
        {
            Background = SolidColorBrush.Parse(bgColor),
            BorderBrush = borderColor == "Transparent" ? Brushes.Transparent : SolidColorBrush.Parse(borderColor),
            BorderThickness = new Thickness(borderColor == "Transparent" ? 0 : 1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1),
            Margin = new Thickness(2, 0, 2, -1),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontFamily = FontFamily.Parse("Consolas, Courier New, monospace"),
                Foreground = fgColor,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        return new InlineUIContainer(border)
        {
            BaselineAlignment = BaselineAlignment.Bottom
        };
    }

    private static Border CreateCodeBlock(string code, bool isSqlMode)
    {
        var codeBlockEditor = new TextEditor
        {
            Document = new TextDocument(code),
            SyntaxHighlighting = isSqlMode ? SqlCodeEditor.GetDarkSqlHighlighting() : CsharpCodeEditor.GetDarkCsharpHighlighting(),
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            FontSize = 13,
            IsReadOnly = true,
            ShowLineNumbers = false,
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Padding = new Thickness(10, 6, 10, 6),
            MinHeight = 0
        };
        codeBlockEditor.Options.ShowSpaces = false;
        codeBlockEditor.Options.ShowTabs = false;
        codeBlockEditor.Options.HighlightCurrentLine = false;

        return new Border
        {
            Background = SolidColorBrush.Parse("#1A1A1A"),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Margin = new Thickness(0, 5, 0, 5),
            BorderBrush = SolidColorBrush.Parse("#333"),
            BorderThickness = new Thickness(1),
            Child = codeBlockEditor
        };
    }
}