using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using InventorModel.Core.Ai;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

internal sealed class AiHistoryWindow : Window
{
    private readonly string _currentSessionDirectory;
    private readonly string _uiLanguage;
    private readonly ListView _list = new ListView();
    private readonly TextBlock _summary = new TextBlock();
    private readonly List<HistoryItem> _items = new List<HistoryItem>();

    public AiHistoryWindow(string currentSessionDirectory)
    {
        _currentSessionDirectory =
            Path.GetFullPath(currentSessionDirectory ?? string.Empty);
        _uiLanguage = AiSettings.Load().UiLanguage;

        Title = T("History.Title");
        Width = 820;
        Height = 560;
        MinWidth = 700;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        UiTheme.Apply(this);
        Content = BuildLayout();
        Loaded += (_, __) => RefreshItems();
    }

    private UIElement BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 10),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2)
        };
        header.SetResourceReference(Border.BackgroundProperty, "AppSurfaceBrush");
        header.SetResourceReference(Border.BorderBrushProperty, "AppBorderBrush");

        var title = new StackPanel();
        title.Children.Add(new TextBlock
        {
            Text = T("History.Header"),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold
        });
        _summary.Margin = new Thickness(0, 3, 0, 0);
        _summary.FontSize = 12;
        _summary.SetResourceReference(TextBlock.ForegroundProperty, "AppMutedTextBrush");
        title.Children.Add(_summary);
        header.Child = title;

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _list.SelectionMode = SelectionMode.Extended;
        _list.MouseDoubleClick += (_, __) => OpenSelected();
        Grid.SetRow(_list, 1);
        root.Children.Add(_list);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Orientation = Orientation.Horizontal };
        Button selectAll = FooterButton(T("History.SelectAll"));
        Button clear = FooterButton(T("History.Clear"));
        Button refresh = FooterButton(T("History.Refresh"));
        selectAll.Click += (_, __) => _list.SelectAll();
        clear.Click += (_, __) => _list.UnselectAll();
        refresh.Click += (_, __) => RefreshItems();
        left.Children.Add(selectAll);
        left.Children.Add(clear);
        left.Children.Add(refresh);
        footer.Children.Add(left);

        var right = new StackPanel { Orientation = Orientation.Horizontal };
        Button open = FooterButton(T("History.Open"));
        Button export = FooterButton(T("History.Export"));
        Button delete = FooterButton(T("History.Delete"));
        delete.Tag = "Danger";
        delete.Margin = new Thickness(0);

        open.Click += (_, __) => OpenSelected();
        export.Click += (_, __) => ExportSelected();
        delete.Click += (_, __) => DeleteSelected();

        right.Children.Add(open);
        right.Children.Add(export);
        right.Children.Add(delete);
        Grid.SetColumn(right, 1);
        footer.Children.Add(right);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private static Button FooterButton(string text) =>
        new Button
        {
            Content = text,
            Margin = new Thickness(0, 0, 8, 0)
        };

    private void RefreshItems()
    {
        _items.Clear();
        _list.Items.Clear();

        try
        {
            if (Directory.Exists(AiWorkspace.SessionsDirectory))
            {
                foreach (string history in Directory
                    .EnumerateFiles(AiWorkspace.SessionsDirectory, "history.md", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTime))
                {
                    HistoryItem item = HistoryItem.Load(history, _currentSessionDirectory);
                    _items.Add(item);
                    _list.Items.Add(CreateRow(item));
                }
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.History",
                "History sessions could not be enumerated.",
                ex);
        }

        _summary.Text = _items.Count == 0
            ? T("History.Empty")
            : UiText.Format(
                _uiLanguage,
                "History.Summary",
                _items.Count);
    }

    private ListViewItem CreateRow(HistoryItem item)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });

        var time = new TextBlock
        {
            Text = item.UpdatedText,
            VerticalAlignment = VerticalAlignment.Center
        };
        time.SetResourceReference(TextBlock.ForegroundProperty, "AppSecondaryTextBrush");
        grid.Children.Add(time);

        var model = new TextBlock
        {
            Text = item.Model,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(model, 1);
        grid.Children.Add(model);

        var path = new TextBlock
        {
            Text = item.SessionName,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = item.Directory,
            VerticalAlignment = VerticalAlignment.Center
        };
        path.SetResourceReference(TextBlock.ForegroundProperty, "AppMutedTextBrush");
        Grid.SetColumn(path, 2);
        grid.Children.Add(path);

        if (item.IsCurrent)
        {
            var current = new TextBlock
            {
                Text = T("History.Current"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11
            };
            current.SetResourceReference(TextBlock.ForegroundProperty, "AppBrandBrush");
            Grid.SetColumn(current, 3);
            grid.Children.Add(current);
        }

        return new ListViewItem
        {
            Content = grid,
            Tag = item
        };
    }

    private List<HistoryItem> SelectedItems() =>
        _list.SelectedItems
            .OfType<ListViewItem>()
            .Select(x => x.Tag as HistoryItem)
            .Where(x => x != null)
            .Cast<HistoryItem>()
            .ToList();

    private void OpenSelected()
    {
        HistoryItem? item = SelectedItems().FirstOrDefault();
        if (item == null)
            return;

        try
        {
            Process.Start("explorer.exe", "/select,\"" + item.HistoryPath + "\"");
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.History",
                "Selected history file could not be opened.",
                ex);
        }
    }

    private void DeleteSelected()
    {
        List<HistoryItem> selected = SelectedItems();
        if (selected.Count == 0)
            return;

        List<HistoryItem> deletable = selected.Where(x => !x.IsCurrent).ToList();
        if (deletable.Count == 0)
        {
            MessageBox.Show(
                this,
                T("History.CurrentProtected"),
                "InventorModel",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            UiText.Format(
                _uiLanguage,
                "History.DeleteConfirm",
                deletable.Count),
            "InventorModel",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        int deleted = 0;
        foreach (HistoryItem item in deletable)
        {
            try
            {
                if (Directory.Exists(item.Directory))
                    Directory.Delete(item.Directory, true);
                deleted++;
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "UI.History",
                    "A selected AI history session could not be deleted: " +
                    item.Directory,
                    ex);
            }
        }

        RefreshItems();

        if (deleted != deletable.Count || deletable.Count != selected.Count)
        {
            MessageBox.Show(
                this,
                UiText.Format(
                    _uiLanguage,
                    "History.Deleted",
                    deleted) +
                (deletable.Count != selected.Count
                    ? T("History.CurrentSkipped")
                    : string.Empty),
                "InventorModel",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void ExportSelected()
    {
        List<HistoryItem> selected = SelectedItems();
        if (selected.Count == 0)
            return;

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = T("History.ExportFolder"),
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        string exportRoot = Path.Combine(
            dialog.SelectedPath,
            "InventorModel-History-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(exportRoot);

        int exported = 0;
        foreach (HistoryItem item in selected)
        {
            try
            {
                string name = SafeFileName(item.SessionName) + ".md";
                string destination = UniqueFile(exportRoot, name);
                File.Copy(item.HistoryPath, destination, false);
                exported++;
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "UI.History",
                    "A selected AI history session could not be exported: " +
                    item.HistoryPath,
                    ex);
            }
        }

        try
        {
            Process.Start(
                "explorer.exe",
                "\"" + exportRoot + "\"");
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.History",
                "Export directory could not be opened.",
                ex);
        }

        if (exported != selected.Count)
        {
            MessageBox.Show(
                this,
                UiText.Format(
                    _uiLanguage,
                    "History.ExportPartial",
                    exported,
                    selected.Count - exported),
                "InventorModel",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private static string UniqueFile(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return path;

        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        for (int i = 2; i < 10000; i++)
        {
            path = Path.Combine(directory, baseName + "-" + i + extension);
            if (!File.Exists(path))
                return path;
        }

        return Path.Combine(directory, baseName + "-" + Guid.NewGuid().ToString("N") + extension);
    }

    private static string SafeFileName(string value)
    {
        var builder = new StringBuilder(value ?? "session");
        foreach (char invalid in Path.GetInvalidFileNameChars())
            builder.Replace(invalid, '_');
        return builder.ToString();
    }

    private string T(string key) =>
        UiText.Get(_uiLanguage, key);

    private sealed class HistoryItem
    {
        public string HistoryPath { get; private set; } = string.Empty;
        public string Directory { get; private set; } = string.Empty;
        public string SessionName { get; private set; } = string.Empty;
        public string Model { get; private set; } = "-";
        public DateTime Updated { get; private set; }
        public bool IsCurrent { get; private set; }

        public string UpdatedText => Updated.ToString("yyyy-MM-dd HH:mm");

        public static HistoryItem Load(string historyPath, string currentSessionDirectory)
        {
            string directory = Path.GetDirectoryName(historyPath) ?? string.Empty;
            var item = new HistoryItem
            {
                HistoryPath = historyPath,
                Directory = directory,
                Updated = File.GetLastWriteTime(historyPath),
                IsCurrent = SamePath(directory, currentSessionDirectory)
            };
            item.SessionName = Path.GetFileName(item.Directory);

            try
            {
                foreach (string line in File.ReadLines(historyPath).Take(12))
                {
                    if (line.StartsWith("- Model:", StringComparison.OrdinalIgnoreCase))
                        item.Model = line.Substring("- Model:".Length).Trim();
                }
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "UI.History",
                    "History metadata could not be read: " +
                    historyPath,
                    ex);
            }

            return item;
        }

        private static bool SamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            try
            {
                return string.Equals(
                    Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "UI.History",
                    "History paths could not be normalized for comparison.",
                    ex);
                return false;
            }
        }
    }
}
