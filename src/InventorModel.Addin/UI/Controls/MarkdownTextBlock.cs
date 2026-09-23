using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

internal sealed class MarkdownTextBlock : ContentControl
{
    private readonly FlowDocumentScrollViewer _viewer;
    private readonly DispatcherTimer _renderTimer;
    private readonly StringBuilder _markdown =
        new StringBuilder();

    private string _lastRenderedMarkdown =
        string.Empty;
    private string _uiLanguage =
        AiSettings.LanguageChinese;

    public MarkdownTextBlock()
    {
        HorizontalContentAlignment =
            HorizontalAlignment.Stretch;
        VerticalContentAlignment =
            VerticalAlignment.Top;
        HorizontalAlignment =
            HorizontalAlignment.Stretch;
        MinWidth = 0;

        _viewer =
            new FlowDocumentScrollViewer
            {
                IsSelectionEnabled = true,
                IsToolBarVisible = false,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled
            };

        _viewer.CommandBindings.Add(
            new CommandBinding(
                ApplicationCommands.Copy,
                Copy_Executed,
                Copy_CanExecute));

        _viewer.ContextMenu =
            BuildContextMenu();

        Content = _viewer;

        _renderTimer =
            new DispatcherTimer(
                DispatcherPriority.Background)
            {
                Interval =
                    TimeSpan.FromMilliseconds(160)
            };

        _renderTimer.Tick += (_, __) =>
        {
            _renderTimer.Stop();
            Render();
        };

        Unloaded += (_, __) =>
            _renderTimer.Stop();
    }

    public string Markdown =>
        _markdown.ToString();

    public string UiLanguage
    {
        get => _uiLanguage;
        set
        {
            _uiLanguage =
                AiSettings.NormalizeUiLanguage(value);
            _viewer.ContextMenu =
                BuildContextMenu();
        }
    }

    public void SetMarkdown(
        string value)
    {
        _markdown.Clear();
        _markdown.Append(
            value ?? string.Empty);
        ScheduleRender();
    }

    public void Append(
        string value)
    {
        if (!string.IsNullOrEmpty(value))
            _markdown.Append(value);

        ScheduleRender();
    }

    public void Clear()
    {
        _markdown.Clear();
        ScheduleRender();
    }

    public void Flush()
    {
        _renderTimer.Stop();
        Render();
    }

    private void ScheduleRender()
    {
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private void Render()
    {
        if (_viewer.Selection != null &&
            !_viewer.Selection.IsEmpty)
        {
            ScheduleRender();
            return;
        }

        string text =
            _markdown.ToString();

        if (string.Equals(
                text,
                _lastRenderedMarkdown,
                StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _viewer.Document =
                BuildDocument(text);
            _lastRenderedMarkdown =
                text;
        }
        catch (Exception ex)
        {
            RuntimeLog.Error(
                "UI.Markdown",
                "Markdown rendering failed.",
                ex);

            _viewer.Document =
                BuildPlainTextDocument(text);
            _lastRenderedMarkdown =
                text;
        }
    }

    private FlowDocument BuildDocument(
        string markdown)
    {
        var document =
            CreateDocument();

        string normalized =
            (markdown ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        string[] lines =
            normalized.Split('\n');

        int index = 0;

        while (index < lines.Length)
        {
            string line =
                lines[index] ?? string.Empty;
            string trimmed =
                line.Trim();

            if (trimmed.Length == 0)
            {
                index++;
                continue;
            }

            if (trimmed.StartsWith(
                    "```",
                    StringComparison.Ordinal))
            {
                index =
                    AddCodeFence(
                        document,
                        lines,
                        index);
                continue;
            }

            if (TryParseImageLine(
                    trimmed,
                    out string alt,
                    out string imageTarget))
            {
                AddImageBlock(
                    document,
                    alt,
                    imageTarget);
                index++;
                continue;
            }

            if (IsTableStart(
                    lines,
                    index))
            {
                index =
                    AddTable(
                        document,
                        lines,
                        index);
                continue;
            }

            int heading =
                HeadingLevel(trimmed);

            if (heading > 0)
            {
                var paragraph =
                    CreateParagraph(
                        new Thickness(
                            0,
                            heading <= 2 ? 7 : 5,
                            0,
                            7));

                paragraph.FontSize =
                    heading switch
                    {
                        1 => 18,
                        2 => 16,
                        3 => 14,
                        _ => 12
                    };

                paragraph.FontWeight =
                    heading <= 2
                        ? FontWeights.SemiBold
                        : FontWeights.Normal;

                AddInlineMarkdown(
                    paragraph,
                    trimmed.Substring(heading)
                        .TrimStart());

                document.Blocks.Add(paragraph);
                index++;
                continue;
            }

            if (trimmed.StartsWith(
                    ">",
                    StringComparison.Ordinal))
            {
                var quote =
                    CreateParagraph(
                        new Thickness(
                            10,
                            2,
                            0,
                            8));

                quote.Foreground =
                    Brush(
                        "#667085",
                        Brushes.DimGray);
                quote.BorderBrush =
                    Brush(
                        "#2563EB",
                        Brushes.SteelBlue);
                quote.BorderThickness =
                    new Thickness(3, 0, 0, 0);
                quote.Padding =
                    new Thickness(10, 1, 0, 1);

                AddInlineMarkdown(
                    quote,
                    trimmed.TrimStart(
                        '>',
                        ' '));

                document.Blocks.Add(quote);
                index++;
                continue;
            }

            if (TryListLine(
                    trimmed,
                    out string marker,
                    out string body))
            {
                index =
                    AddList(
                        document,
                        lines,
                        index,
                        marker,
                        body);
                continue;
            }

            if (IsThematicBreak(trimmed))
            {
                document.Blocks.Add(
                    new BlockUIContainer(
                        new Border
                        {
                            Height = 1,
                            Margin =
                                new Thickness(
                                    0,
                                    5,
                                    0,
                                    9),
                            Background =
                                Brush(
                                    "#D9DEE8",
                                    Brushes.LightGray)
                        }));
                index++;
                continue;
            }

            var paragraphLines =
                new List<string>();

            while (index < lines.Length)
            {
                string current =
                    lines[index] ??
                    string.Empty;
                string currentTrimmed =
                    current.Trim();

                if (currentTrimmed.Length == 0 ||
                    currentTrimmed.StartsWith(
                        "```",
                        StringComparison.Ordinal) ||
                    HeadingLevel(currentTrimmed) > 0 ||
                    currentTrimmed.StartsWith(
                        ">",
                        StringComparison.Ordinal) ||
                    IsThematicBreak(currentTrimmed) ||
                    IsTableStart(lines, index) ||
                    TryParseImageLine(
                        currentTrimmed,
                        out _,
                        out _) ||
                    TryListLine(
                        currentTrimmed,
                        out _,
                        out _))
                {
                    break;
                }

                paragraphLines.Add(
                    current.Trim());
                index++;
            }

            if (paragraphLines.Count == 0)
            {
                index++;
                continue;
            }

            var paragraph =
                CreateParagraph(
                    new Thickness(
                        0,
                        0,
                        0,
                        7));

            AddInlineMarkdown(
                paragraph,
                string.Join(
                    " ",
                    paragraphLines));

            document.Blocks.Add(paragraph);
        }

        if (document.Blocks.Count == 0)
            document.Blocks.Add(
                new Paragraph());

        return document;
    }

    private FlowDocument BuildPlainTextDocument(
        string text)
    {
        var document =
            CreateDocument();

        document.Blocks.Add(
            new Paragraph(
                new Run(
                    text ?? string.Empty))
            {
                Margin =
                    new Thickness(0),
                LineHeight = 19
            });

        return document;
    }

    private FlowDocument CreateDocument() =>
        new FlowDocument
        {
            PagePadding =
                new Thickness(0),
            ColumnWidth =
                double.PositiveInfinity,
            FontFamily =
                new FontFamily(
                    "Microsoft YaHei"),
            FontSize = 12,
            Foreground =
                Brush(
                    "#1F2328",
                    Brushes.Black),
            TextAlignment =
                TextAlignment.Left
        };

    private int AddCodeFence(
        FlowDocument document,
        string[] lines,
        int start)
    {
        int index = start + 1;
        var code =
            new StringBuilder();

        while (index < lines.Length)
        {
            string value =
                lines[index] ??
                string.Empty;

            if (value.TrimStart()
                .StartsWith(
                    "```",
                    StringComparison.Ordinal))
            {
                index++;
                break;
            }

            if (code.Length > 0)
                code.AppendLine();

            code.Append(value);
            index++;
        }

        var paragraph =
            new Paragraph(
                new Run(code.ToString()))
            {
                Margin =
                    new Thickness(
                        0,
                        3,
                        0,
                        9),
                Padding =
                    new Thickness(
                        10,
                        8,
                        10,
                        8),
                FontFamily =
                    new FontFamily(
                        "Consolas"),
                FontSize = 11.5,
                LineHeight = 18,
                Background =
                    Brush(
                        "#F7F9FC",
                        Brushes.WhiteSmoke),
                BorderBrush =
                    Brush(
                        "#D9DEE8",
                        Brushes.LightGray),
                BorderThickness =
                    new Thickness(1)
            };

        document.Blocks.Add(paragraph);
        return index;
    }

    private int AddTable(
        FlowDocument document,
        string[] lines,
        int start)
    {
        string[] headers =
            SplitTableRow(lines[start]);
        int columns =
            headers.Length;

        var table =
            new Table
            {
                CellSpacing = 0,
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        9)
            };

        for (int i = 0;
             i < columns;
             i++)
        {
            table.Columns.Add(
                new TableColumn());
        }

        var group =
            new TableRowGroup();
        table.RowGroups.Add(group);

        group.Rows.Add(
            CreateTableRow(
                headers,
                true));

        int index = start + 2;

        while (index < lines.Length &&
               LooksLikeTableRow(
                   lines[index].Trim()))
        {
            string[] cells =
                SplitTableRow(
                    lines[index]);

            if (cells.Length != columns)
                break;

            group.Rows.Add(
                CreateTableRow(
                    cells,
                    false));
            index++;
        }

        document.Blocks.Add(table);
        return index;
    }

    private TableRow CreateTableRow(
        IReadOnlyList<string> cells,
        bool header)
    {
        var row =
            new TableRow();

        foreach (string value in cells)
        {
            var paragraph =
                CreateParagraph(
                    new Thickness(0));

            paragraph.Margin =
                new Thickness(0);

            if (header)
                paragraph.FontWeight =
                    FontWeights.SemiBold;

            AddInlineMarkdown(
                paragraph,
                value.Trim());

            var cell =
                new TableCell(paragraph)
                {
                    Padding =
                        new Thickness(
                            8,
                            5,
                            8,
                            5),
                    BorderBrush =
                        Brush(
                            "#D9DEE8",
                            Brushes.LightGray),
                    BorderThickness =
                        new Thickness(1),
                    Background =
                        header
                            ? Brush(
                                "#F2F5FA",
                                Brushes.WhiteSmoke)
                            : Brushes.Transparent
                };

            row.Cells.Add(cell);
        }

        return row;
    }

    private int AddList(
        FlowDocument document,
        string[] lines,
        int start,
        string firstMarker,
        string firstBody)
    {
        bool ordered =
            firstMarker != "•";

        var list =
            new System.Windows.Documents.List
            {
                MarkerStyle =
                    ordered
                        ? TextMarkerStyle.Decimal
                        : TextMarkerStyle.Disc,
                Margin =
                    new Thickness(
                        18,
                        0,
                        0,
                        8),
                Padding =
                    new Thickness(0)
            };

        int index = start;
        string marker =
            firstMarker;
        string body =
            firstBody;

        while (index < lines.Length)
        {
            if (index != start)
            {
                if (!TryListLine(
                        (lines[index] ?? string.Empty)
                        .Trim(),
                        out marker,
                        out body))
                {
                    break;
                }

                bool currentOrdered =
                    marker != "•";

                if (currentOrdered != ordered)
                    break;
            }

            var paragraph =
                CreateParagraph(
                    new Thickness(
                        0,
                        0,
                        0,
                        2));

            AddInlineMarkdown(
                paragraph,
                body);

            list.ListItems.Add(
                new ListItem(paragraph)
                {
                    Margin =
                        new Thickness(
                            0,
                            1,
                            0,
                            1)
                });

            index++;
        }

        document.Blocks.Add(list);
        return index;
    }

    private void AddImageBlock(
        FlowDocument document,
        string alt,
        string target)
    {
        BitmapSource? source =
            TryLoadImage(target);

        if (source == null)
        {
            var paragraph =
                CreateParagraph(
                    new Thickness(
                        0,
                        0,
                        0,
                        7));

            paragraph.Foreground =
                Brush(
                    "#667085",
                    Brushes.DimGray);

            paragraph.Inlines.Add(
                new Run(
                    string.IsNullOrWhiteSpace(alt)
                        ? "[image]"
                        : "[" + alt + "]"));

            document.Blocks.Add(paragraph);
            return;
        }

        var image =
            new Image
            {
                Source = source,
                MaxWidth = 640,
                MaxHeight = 420,
                Stretch =
                    Stretch.Uniform,
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                ToolTip = target
            };

        document.Blocks.Add(
            new BlockUIContainer(
                new Border
                {
                    Child = image,
                    Background =
                        Brushes.White,
                    BorderBrush =
                        Brush(
                            "#D9DEE8",
                            Brushes.LightGray),
                    BorderThickness =
                        new Thickness(1),
                    CornerRadius =
                        new CornerRadius(6),
                    Padding =
                        new Thickness(4),
                    Margin =
                        new Thickness(
                            0,
                            3,
                            0,
                            9)
                }));
    }

    private void AddInlineMarkdown(
        Paragraph paragraph,
        string text)
    {
        int index = 0;

        while (index < text.Length)
        {
            int next =
                FindNextInlineMarker(
                    text,
                    index);

            if (next < 0)
            {
                paragraph.Inlines.Add(
                    new Run(
                        text.Substring(index)));
                break;
            }

            if (next > index)
            {
                paragraph.Inlines.Add(
                    new Run(
                        text.Substring(
                            index,
                            next - index)));
            }

            if (TryPair(
                    text,
                    next,
                    "**",
                    out string bold,
                    out int end))
            {
                paragraph.Inlines.Add(
                    new Bold(
                        new Run(bold)));
                index = end;
                continue;
            }

            if (TryPair(
                    text,
                    next,
                    "~~",
                    out string strike,
                    out end))
            {
                paragraph.Inlines.Add(
                    new Run(strike)
                    {
                        TextDecorations =
                            TextDecorations.Strikethrough
                    });
                index = end;
                continue;
            }

            if (TryPair(
                    text,
                    next,
                    "`",
                    out string code,
                    out end))
            {
                paragraph.Inlines.Add(
                    new Run(code)
                    {
                        FontFamily =
                            new FontFamily(
                                "Consolas"),
                        Background =
                            Brush(
                                "#F2F4F7",
                                Brushes.WhiteSmoke)
                    });
                index = end;
                continue;
            }

            if (text[next] == '[' &&
                TryLink(
                    text,
                    next,
                    out string linkText,
                    out string url,
                    out end))
            {
                var hyperlink =
                    new Hyperlink(
                        new Run(linkText))
                    {
                        NavigateUri =
                            TryUri(url),
                        Foreground =
                            Brush(
                                "#2563EB",
                                Brushes.RoyalBlue),
                        TextDecorations =
                            null
                    };

                hyperlink.RequestNavigate +=
                    Hyperlink_RequestNavigate;

                paragraph.Inlines.Add(
                    hyperlink);
                index = end;
                continue;
            }

            if (text[next] == '*' &&
                TryPair(
                    text,
                    next,
                    "*",
                    out string italic,
                    out end))
            {
                paragraph.Inlines.Add(
                    new Italic(
                        new Run(italic)));
                index = end;
                continue;
            }

            paragraph.Inlines.Add(
                new Run(
                    text[next]
                    .ToString()));
            index = next + 1;
        }
    }

    private void Copy_CanExecute(
        object sender,
        CanExecuteRoutedEventArgs e)
    {
        e.CanExecute =
            _viewer.Selection != null &&
            !_viewer.Selection.IsEmpty;
        e.Handled = true;
    }

    private void Copy_Executed(
        object sender,
        ExecutedRoutedEventArgs e)
    {
        CopySelection();
        e.Handled = true;
    }

    private void CopySelection()
    {
        if (_viewer.Selection == null ||
            _viewer.Selection.IsEmpty)
        {
            return;
        }

        string text =
            _viewer.Selection.Text ??
            string.Empty;

        ClipboardAccess.TrySetText(
            text,
            out _);
    }

    private ContextMenu BuildContextMenu()
    {
        var menu =
            new ContextMenu();

        var copy =
            new MenuItem
            {
                Header =
                    UiText.IsEnglish(_uiLanguage)
                        ? "Copy"
                        : "复制"
            };
        copy.Click += (_, __) =>
            CopySelection();

        var selectAll =
            new MenuItem
            {
                Header =
                    UiText.IsEnglish(_uiLanguage)
                        ? "Select all"
                        : "全选"
            };
        selectAll.Click += (_, __) =>
        {
            if (_viewer.Document == null ||
                _viewer.Selection == null)
            {
                return;
            }

            _viewer.Selection.Select(
                _viewer.Document.ContentStart,
                _viewer.Document.ContentEnd);
        };

        var copyAll =
            new MenuItem
            {
                Header =
                    UiText.IsEnglish(_uiLanguage)
                        ? "Copy all"
                        : "复制全部"
            };
        copyAll.Click += (_, __) =>
            ClipboardAccess.TrySetText(
                Markdown,
                out _);

        menu.Items.Add(copy);
        menu.Items.Add(selectAll);
        menu.Items.Add(
            new Separator());
        menu.Items.Add(copyAll);

        return menu;
    }

    private void Hyperlink_RequestNavigate(
        object sender,
        RequestNavigateEventArgs e)
    {
        try
        {
            if (e.Uri == null)
                return;

            Process.Start(
                new ProcessStartInfo(
                    e.Uri.AbsoluteUri)
                {
                    UseShellExecute = true
                });

            e.Handled = true;
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.Markdown",
                "Markdown link could not be opened.",
                ex);
        }
    }

    private BitmapSource? TryLoadImage(
        string target)
    {
        try
        {
            string value =
                (target ?? string.Empty)
                .Trim();

            Uri? uri =
                TryUri(value);

            if (uri == null ||
                !uri.IsFile)
            {
                return null;
            }

            string path =
                uri.LocalPath;

            if (!File.Exists(path))
                return null;

            var image =
                new BitmapImage();
            image.BeginInit();
            image.CacheOption =
                BitmapCacheOption.OnLoad;
            image.UriSource =
                new Uri(
                    Path.GetFullPath(path),
                    UriKind.Absolute);
            image.EndInit();

            if (image.CanFreeze)
                image.Freeze();

            return image;
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.Markdown",
                "Markdown image could not be loaded.",
                ex);
            return null;
        }
    }

    private static Brush Brush(
        string hex,
        Brush fallback)
    {
        try
        {
            return (Brush)
                new BrushConverter()
                .ConvertFromString(hex);
        }
        catch
        {
            return fallback;
        }
    }

    private static int HeadingLevel(
        string text)
    {
        int level = 0;

        while (level < text.Length &&
               level < 6 &&
               text[level] == '#')
        {
            level++;
        }

        return level > 0 &&
               level < text.Length &&
               char.IsWhiteSpace(text[level])
            ? level
            : 0;
    }

    private static bool TryListLine(
        string text,
        out string marker,
        out string body)
    {
        marker = string.Empty;
        body = text;

        if (text.StartsWith(
                "- ",
                StringComparison.Ordinal) ||
            text.StartsWith(
                "* ",
                StringComparison.Ordinal) ||
            text.StartsWith(
                "+ ",
                StringComparison.Ordinal))
        {
            marker = "•";
            body =
                text.Substring(2);
            return true;
        }

        Match numbered =
            Regex.Match(
                text,
                @"^(\d+)[.)]\s+(.+)$");

        if (!numbered.Success)
            return false;

        marker =
            numbered.Groups[1].Value +
            ".";
        body =
            numbered.Groups[2].Value;
        return true;
    }

    private static bool IsTableStart(
        string[] lines,
        int index)
    {
        if (index + 1 >= lines.Length)
            return false;

        return LooksLikeTableRow(
                   (lines[index] ??
                    string.Empty).Trim()) &&
               IsTableSeparator(
                   (lines[index + 1] ??
                    string.Empty).Trim());
    }

    private static bool LooksLikeTableRow(
        string text) =>
        text.Length >= 3 &&
        text.Contains("|");

    private static bool IsTableSeparator(
        string text)
    {
        if (!text.Contains("|") ||
            text.IndexOf(
                '-',
                StringComparison.Ordinal) < 0)
        {
            return false;
        }

        string[] cells =
            SplitTableRow(text);

        return cells.Length > 0 &&
               cells.All(cell =>
                   Regex.IsMatch(
                       cell.Trim(),
                       @"^:?-{3,}:?$"));
    }

    private static string[] SplitTableRow(
        string text)
    {
        string value =
            (text ?? string.Empty)
            .Trim();

        if (value.StartsWith(
                "|",
                StringComparison.Ordinal))
        {
            value =
                value.Substring(1);
        }

        if (value.EndsWith(
                "|",
                StringComparison.Ordinal))
        {
            value =
                value.Substring(
                    0,
                    value.Length - 1);
        }

        return value
            .Split('|')
            .Select(x => x.Trim())
            .ToArray();
    }

    private static bool IsThematicBreak(
        string text) =>
        Regex.IsMatch(
            text,
            @"^\s*([-*_]\s*){3,}$");

    private static bool TryParseImageLine(
        string text,
        out string alt,
        out string target)
    {
        Match match =
            Regex.Match(
                text,
                @"^!\[([^\]]*)\]\(([^)]+)\)$");

        if (!match.Success)
        {
            alt = string.Empty;
            target = string.Empty;
            return false;
        }

        alt =
            match.Groups[1].Value;
        target =
            match.Groups[2].Value;
        return true;
    }

    private static int FindNextInlineMarker(
        string text,
        int start)
    {
        int result = -1;

        foreach (char marker in
                 new[]
                 {
                     '*',
                     '~',
                     '`',
                     '['
                 })
        {
            int found =
                text.IndexOf(
                    marker,
                    start);

            if (found >= 0 &&
                (result < 0 ||
                 found < result))
            {
                result = found;
            }
        }

        return result;
    }

    private static bool TryPair(
        string text,
        int start,
        string marker,
        out string body,
        out int end)
    {
        body = string.Empty;
        end = start;

        if (!text.Substring(start)
            .StartsWith(
                marker,
                StringComparison.Ordinal))
        {
            return false;
        }

        int close =
            text.IndexOf(
                marker,
                start + marker.Length,
                StringComparison.Ordinal);

        if (close <=
            start + marker.Length)
        {
            return false;
        }

        body =
            text.Substring(
                start + marker.Length,
                close -
                start -
                marker.Length);

        end =
            close +
            marker.Length;
        return true;
    }

    private static bool TryLink(
        string text,
        int start,
        out string label,
        out string url,
        out int end)
    {
        label = string.Empty;
        url = string.Empty;
        end = start;

        int closeLabel =
            text.IndexOf(
                ']',
                start + 1);

        if (closeLabel < 0 ||
            closeLabel + 1 >= text.Length ||
            text[closeLabel + 1] != '(')
        {
            return false;
        }

        int closeUrl =
            text.IndexOf(
                ')',
                closeLabel + 2);

        if (closeUrl < 0)
            return false;

        label =
            text.Substring(
                start + 1,
                closeLabel -
                start -
                1);

        url =
            text.Substring(
                closeLabel + 2,
                closeUrl -
                closeLabel -
                2);

        end =
            closeUrl + 1;
        return true;
    }

    private static Uri? TryUri(
        string value)
    {
        string text =
            (value ?? string.Empty)
            .Trim();

        if (text.Length == 0)
            return null;

        try
        {
            if (Path.IsPathRooted(text))
            {
                string rooted =
                    Path.GetFullPath(text);

                return new Uri(
                    rooted,
                    UriKind.Absolute);
            }
        }
        catch
        {
            // Continue with URI parsing.
        }

        if (Uri.TryCreate(
                text,
                UriKind.Absolute,
                out Uri? uri))
        {
            return uri;
        }

        try
        {
            string full =
                Path.GetFullPath(text);
            return new Uri(
                full,
                UriKind.Absolute);
        }
        catch
        {
            return null;
        }
    }

    private static Paragraph CreateParagraph(
        Thickness margin) =>
        new Paragraph
        {
            Margin = margin,
            LineHeight = 19
        };
}
