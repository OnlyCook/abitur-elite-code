using Avalonia.Media;

namespace AbiturEliteCode;

internal static class Scheme
{
    public static readonly SolidColorBrush BrushBgPanel = SolidColorBrush.Parse("#202124"); // darker gray
    public static readonly SolidColorBrush BrushBgPanel2 = SolidColorBrush.Parse("#3C3C3C"); // lighter dark gray
    public static readonly SolidColorBrush BrushBgPanel3 = SolidColorBrush.Parse("#1A1A1A"); // very dark gray
    public static readonly SolidColorBrush BrushBgPanel4 = SolidColorBrush.Parse("#333333"); // not so very dark gray
    public static readonly SolidColorBrush BrushBgPanel5 = SolidColorBrush.Parse("#333"); // partially dark gray
    public static readonly SolidColorBrush BrushBgPanel6 = SolidColorBrush.Parse("#2b2b2b"); // pretty dark gray
    public static readonly SolidColorBrush BrushBgPanel7 = SolidColorBrush.Parse("#141414"); // darkest of gray
    public static readonly SolidColorBrush BrushBgPanel8 = SolidColorBrush.Parse("#444"); // partially++ dark gray
    public static readonly SolidColorBrush BrushBgPanel9 = SolidColorBrush.Parse("#666"); // devilish gray
    public static readonly SolidColorBrush BrushBgPanel10 = SolidColorBrush.Parse("#4A4A4A"); // mid-dark gray
    public static readonly SolidColorBrush BrushBgPanel11 = SolidColorBrush.Parse("#111111"); // >is it really dark gray?; <1;
    public static readonly SolidColorBrush BrushBgPanel12 = SolidColorBrush.Parse("#888"); // nearly half gray
    public static readonly SolidColorBrush BrushBgPanel13 = SolidColorBrush.Parse("#2D2D30"); // was blue dark gray
    public static readonly SolidColorBrush BrushBgPanel14 = SolidColorBrush.Parse("#313133"); // was blue too dark gray
    public static readonly SolidColorBrush BrushBgPanel15 = SolidColorBrush.Parse("#555"); // partially+++ dark gray
    public static readonly SolidColorBrush BrushBgPanel16 = SolidColorBrush.Parse("#303030"); // its all just gray
    public static readonly SolidColorBrush BrushBgPanel17 = SolidColorBrush.Parse("#3E3E42"); // medium-mid-dark gray
    public static readonly SolidColorBrush BrushBgPanel18 = SolidColorBrush.Parse("#464646"); // mid gray
    public static readonly SolidColorBrush BrushBgPanel19 = SolidColorBrush.Parse("#121212"); // splash screen dark gray
    public static readonly SolidColorBrush BrushTextNormal = SolidColorBrush.Parse("#E6E6E6"); // light gray
    public static readonly SolidColorBrush BrushTextNormal2 = SolidColorBrush.Parse("#555555"); // gray
    public static readonly SolidColorBrush BrushTextNormal3 = SolidColorBrush.Parse("#252526"); // dark gray
    public static readonly SolidColorBrush BrushTextNormal4 = SolidColorBrush.Parse("#3C3C41"); // medium gray
    public static readonly SolidColorBrush BrushTextNormal5 = SolidColorBrush.Parse("#B48EAD"); // light-medium gray
    public static readonly SolidColorBrush BrushTextNormal6 = SolidColorBrush.Parse("#D4D4D4"); // lightest gray
    public static readonly SolidColorBrush BrushTextNormal7 = SolidColorBrush.Parse("#CCCCCC"); // low level gray

    public static readonly SolidColorBrush BrushTextHighlight = SolidColorBrush.Parse("#6495ED"); // light blue
    public static readonly SolidColorBrush BrushTextHighlight2 = SolidColorBrush.Parse("#007ACC"); // blue
    public static readonly SolidColorBrush BrushBadgeDefault = SolidColorBrush.Parse("#1E1E1E"); // very-ish dark gray
    public static readonly SolidColorBrush BrushRowDefault = SolidColorBrush.Parse("#2A2A2A"); // pretty much dark gray
    public static readonly SolidColorBrush BrushTextTitle = SolidColorBrush.Parse("#32A852"); // green
    public static readonly SolidColorBrush BrushTextTitleAlpha = SolidColorBrush.Parse("#3332A852"); // semi-transparent green
    public static readonly SolidColorBrush BrushGlobalFg = SolidColorBrush.Parse("#1c9c9c"); // cyan
    public static readonly SolidColorBrush BrushGlobalBg = SolidColorBrush.Parse("#1d8080"); // darkened cyan
    public static readonly SolidColorBrush BrushDeniedFg = SolidColorBrush.Parse("#FF5555"); // light red
    public static readonly SolidColorBrush BrushDeniedBg = SolidColorBrush.Parse("#d44c0d"); // redder red
    public static readonly SolidColorBrush BrushHardPassFg = SolidColorBrush.Parse("#FF2222"); // unapproachable red
    public static readonly SolidColorBrush BrushPressedDenialBg = SolidColorBrush.Parse("#8B0000"); // dark red
    public static readonly SolidColorBrush BrushApprovedBg = SolidColorBrush.Parse("#2e8b57"); // pleasent greenery
    public static readonly SolidColorBrush BrushUpvoteFg = SolidColorBrush.Parse("#256495ED"); // should be blue
    public static readonly SolidColorBrush BrushAiModeFg = SolidColorBrush.Parse("#0088e3"); // sky blue
    public static readonly SolidColorBrush BrushYouTubeRed = SolidColorBrush.Parse("#b00000");
    public static readonly SolidColorBrush BrushMicrosoftPurple = SolidColorBrush.Parse("#5D3FD3");
    public static readonly SolidColorBrush BrushMySQLDocsBlue = SolidColorBrush.Parse("#0078D4");
    public static readonly SolidColorBrush BrushDopamineEnducingGold = SolidColorBrush.Parse("#FFD700");
    public static readonly SolidColorBrush BrushFeedbackPink = SolidColorBrush.Parse("#A870A8");

    // difficulty colors
    public static readonly SolidColorBrush BrushDiffEasy = SolidColorBrush.Parse("#28a745"); // lighter green
    public static readonly SolidColorBrush BrushDiffMid = SolidColorBrush.Parse("#d1833b"); // orange
    public static readonly SolidColorBrush BrushDiffHard = SolidColorBrush.Parse("#B43232"); // red (also general error color)
    public static readonly SolidColorBrush BrushDiffAbi = SolidColorBrush.Parse("#8A2BE2"); // purple
    public static readonly SolidColorBrush BrushDiffEasyBg = SolidColorBrush.Parse("#1a3320"); // darkened lighter green
    public static readonly SolidColorBrush BrushDiffMidBg = SolidColorBrush.Parse("#362512"); // darkened orange
    public static readonly SolidColorBrush BrushDiffHardBg = SolidColorBrush.Parse("#331a1a"); // darkened red
    public static readonly SolidColorBrush BrushDiffAbiBg = SolidColorBrush.Parse("#2d1a33"); // darkened purple
    public static readonly SolidColorBrush BrushDiffFallbackBg = SolidColorBrush.Parse("#191919"); // darkk gray

    // custom tri state checkbox colors
    public static readonly SolidColorBrush BrushTriCheckEnlight = SolidColorBrush.Parse("#22FFFFFF"); // lightness
    public static readonly SolidColorBrush BrushTriCheckIgnoreFg = SolidColorBrush.Parse("#E05555"); // not too light red
    public static readonly SolidColorBrush BrushCheckIgnoreHoverFg = SolidColorBrush.Parse("#F07070"); // too light red
    public static readonly SolidColorBrush BrushTriCheckIgnoreBg = SolidColorBrush.Parse("#3C1A1A"); // dark brown-red
    public static readonly SolidColorBrush BrushTriCheckIncludeHoverFg = SolidColorBrush.Parse("#88B4F0"); // sky light blue
    public static readonly SolidColorBrush BrushTriCheckBg = SolidColorBrush.Parse("#BBBBBB"); // more light than not gray

    // tab dock manager colors
    public static readonly SolidColorBrush BrushTabDockHighlight = SolidColorBrush.Parse("#1A6495ED"); // bluish cyan
    public static readonly SolidColorBrush BrushTabDockHighlight2 = SolidColorBrush.Parse("#256495ED"); // greenish cyan

    // editor tools colors
    public static readonly SolidColorBrush BrushEditorUnusedCode = SolidColorBrush.Parse("#60D4D4D4"); // faded light gray (unused code dimming)
    public static readonly SolidColorBrush BrushEditorUnusedCode2 = SolidColorBrush.Parse("#66D4D4D4"); // slightly less faded light gray
    public static readonly SolidColorBrush BrushEditorEscapeSequence = SolidColorBrush.Parse("#D7BA7D"); // escape sequence gold
    public static readonly SolidColorBrush BrushEditorVimCaret = SolidColorBrush.Parse("#88FFFFFF"); // semi-transparent white (vim block caret)
    public static readonly SolidColorBrush BrushEditorSelection = SolidColorBrush.Parse("#264F78"); // selection blue
    public static readonly SolidColorBrush BrushEditorSelectionFaded = SolidColorBrush.Parse("#33007ACC"); // faded selection blue
    public static readonly SolidColorBrush BrushEditorWhiteAlpha30 = SolidColorBrush.Parse("#30FFFFFF"); // 30-alpha white overlay
    public static readonly SolidColorBrush BrushEditorWhiteAlpha35 = SolidColorBrush.Parse("#35FFFFFF"); // 35-alpha white overlay
    public static readonly SolidColorBrush BrushEditorGray50 = SolidColorBrush.Parse("#808080"); // mid grayish
    public static readonly SolidColorBrush BrushEditorGray60 = SolidColorBrush.Parse("#969696"); // lighter mid gray
    public static readonly SolidColorBrush BrushEditorGray61 = SolidColorBrush.Parse("#9B9B9B"); // slightly lighter mid gray
    public static readonly SolidColorBrush BrushEditorGray63 = SolidColorBrush.Parse("#A0A0A0"); // almost light gray
    public static readonly SolidColorBrush BrushEditorPurpleKeyword = SolidColorBrush.Parse("#C586C0"); // keyword purple

    // sql syntax highlight colors
    public static readonly SolidColorBrush BrushSqlComment = SolidColorBrush.Parse("#6A9955"); // comment green
    public static readonly SolidColorBrush BrushSqlString = SolidColorBrush.Parse("#CE9178"); // string orange-brown
    public static readonly SolidColorBrush BrushSqlNumber = SolidColorBrush.Parse("#B5CEA8"); // number pale green
    public static readonly SolidColorBrush BrushSqlKeyword = SolidColorBrush.Parse("#569CD6"); // keyword blue
    public static readonly SolidColorBrush BrushSqlVariable = SolidColorBrush.Parse("#9CDCFE"); // variable light cyan
    public static readonly SolidColorBrush BrushSqlType = SolidColorBrush.Parse("#4EC9B0"); // seal type teal

    // markdown renderer colors
    public static readonly SolidColorBrush BrushMarkdownH3 = SolidColorBrush.Parse("#B0B0B0"); // generic light gray
    public static readonly SolidColorBrush BrushMarkdownInlineCode = SolidColorBrush.Parse("#DCDCAA"); // enlighting green

    // ui status / misc
    public static readonly SolidColorBrush BrushStatusErrorText = SolidColorBrush.Parse("#FF6B6B"); // soft warning red
    public static readonly SolidColorBrush BrushConsoleBg = SolidColorBrush.Parse("#101010"); // near-black console background

    // nasa be hiring me starting from the first glance at this file...
}