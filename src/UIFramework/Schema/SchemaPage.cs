using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Renders a page from its settings: section headings where the section changes, numbers three to a row,
    /// switches two to a row, everything else on its own line. Each kind maps to one of the <see cref="Fields"/>.
    /// </summary>
    public static class SchemaPage
    {
        private static readonly HashSet<string> NumberKinds = new HashSet<string> { "int", "long", "double", "percent", "duration" };

        public static UIElement Build(PageContext ctx, List<Setting> settings, string pageId = null)
        {
            var page = Ui.PageStack();
            var scroll = Ui.Scroll(page);
            if (ctx.Customizing)
            {
                FillCustomizing(ctx, page, settings);
                Customize.AcceptDrops(ctx, scroll, page, pageId ?? (settings.Count > 0 ? settings[0].Page : null));
            }
            else
                Fill(ctx, page, settings, true);
            return scroll;
        }

        /// <summary>The customise mode: one field per line, each in a draggable frame with its menu; hidden ones
        /// dimmed so they can be shown again.</summary>
        private static void FillCustomizing(PageContext ctx, StackPanel page, List<Setting> settings)
        {
            string section = null;
            string pageId = settings.Count > 0 ? settings[0].Page : "";
            foreach (var setting in settings.OrderBy(s => s.Order))
            {
                if (setting.Section != null && setting.Section != section)
                {
                    section = setting.Section;
                    page.Children.Add(Ui.Section(section, page.Children.Count == 0));
                }
                var element = Render(ctx, setting);
                if (element != null)
                    page.Children.Add(Customize.Wrap(ctx, setting, element, pageId));
            }
            if (settings.Count == 0)
                page.Children.Add(Ui.Note("No fields on this page. Drop one here from another page, or move one with its menu."));
        }

        /// <summary>Fields the spec put on a standard page, above that page's own content.</summary>
        public static UIElement Prepend(PageContext ctx, List<Setting> settings, UIElement inner)
        {
            if (settings.Count == 0 || inner == null)
                return inner;
            var top = Ui.PageStack();
            top.Margin = new Thickness(16, 2, 16, 0);
            if (ctx.Customizing)
                FillCustomizing(ctx, top, settings);
            else
                Fill(ctx, top, settings, true);
            var dock = new DockPanel();
            DockPanel.SetDock(top, Dock.Top);
            dock.Children.Add(top);
            dock.Children.Add(inner);
            return dock;
        }

        private static void Fill(PageContext ctx, StackPanel page, List<Setting> settings, bool firstHeading)
        {
            string section = null;
            var row = new List<FrameworkElement>();
            string rowKind = null;
            int rowMax = 1;

            void Flush()
            {
                if (row.Count == 0) return;
                if (row.Count == 1 && rowKind != "bool")
                    page.Children.Add(row[0]);
                else
                {
                    var cells = row.ToList();
                    while (cells.Count < rowMax && rowKind != "bool")
                        cells.Add(new TextBlock());
                    page.Children.Add(Ui.Row(cells.ToArray()));
                }
                row.Clear();
                rowKind = null;
            }

            foreach (var setting in settings.Where(s => !s.Hidden).OrderBy(s => s.Order))
            {
                if (setting.Section != null && setting.Section != section)
                {
                    Flush();
                    section = setting.Section;
                    page.Children.Add(Ui.Section(section, firstHeading && page.Children.Count == 0));
                }

                var element = Render(ctx, setting);
                if (element == null)
                    continue;

                string kind = NumberKinds.Contains(setting.Kind) ? "number" : setting.Kind == "bool" ? "bool" : "single";
                int max = kind == "number" ? 3 : kind == "bool" ? 2 : 1;
                if (kind == "single" || (rowKind != null && rowKind != kind) || row.Count >= rowMax)
                    Flush();
                rowKind = kind;
                rowMax = max;
                row.Add(element);
                if (kind == "single")
                    Flush();
            }
            Flush();
        }

        public static FrameworkElement Render(PageContext ctx, Setting s)
        {
            var f = ctx.Fields;
            string label = s.Label ?? Conventions.Humanize(s.Key);
            string help = s.Help ?? "";
            if (s.ReadOnly)
            {
                var value = new TextBlock { Text = f.String(s.Key), FontSize = 13, Foreground = Theme.LabelFg, Margin = new Thickness(0, 4, 0, 4) };
                return Ui.Labeled(label, value);
            }

            FrameworkElement element;
            switch (s.Kind)
            {
                case "bool":
                    element = f.Toggle(label, s.Key, help);
                    break;
                case "int":
                    element = s.Options != null && s.Options.Count > 0
                        ? f.IntCombo(label, s.Key, s.Options.Select(o => (int.TryParse(o.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0, o.Label)).ToArray(), help)
                        : f.IntField(label, s.Key, help, s.UnlimitedAtZero);
                    break;
                case "long":
                    element = f.LongField(label, s.Key, help, s.UnlimitedAtZero);
                    break;
                case "duration":
                    element = f.IntField(label + " (" + (s.Unit ?? "s") + ")", s.Key, help, s.UnlimitedAtZero);
                    break;
                case "double":
                    if (s.Options != null && s.Options.Count > 0)
                        element = f.DoubleCombo(label, s.Key, s.Options.Select(o => (double.TryParse(o.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0.0, o.Label)).ToArray(), help);
                    else
                    {
                        element = f.DoubleField(label + (s.Unit != null ? " (" + s.Unit + ")" : ""), s.Key, help);
                        SetRange(ctx, s);
                    }
                    break;
                case "percent":
                    element = f.PercentField(label, s.Key, s.Factor ?? 1.0, help, s.Min ?? 0, s.Max ?? 100);
                    break;
                case "options":
                    var options = (s.Options ?? new List<SettingOption>()).Select(o => (o.Value, o.Label)).ToArray();
                    element = options.Length == 0 ? f.TextField(label, s.Key, help) : f.StringComboField(label, s.Key, options, help);
                    break;
                case "namelist":
                    element = Stack(Ui.SmallLabel(label, 6), f.NameList(label, s.Key, "Add a name and press Enter", help));
                    break;
                case "grouplist":
                    element = Stack(Ui.SmallLabel(label, 6), f.NameList(label, s.Key, "Add a Streamer.bot group", help));
                    break;
                case "colour":
                case "color":
                    element = f.ColourField(label, s.Key, help);
                    break;
                case "path":
                    element = PathField(ctx, s, label, help);
                    break;
                case "text":
                case "template":
                    var box = f.TemplateBox(label, s.Key);
                    box.ToolTip = help;
                    element = Ui.Labeled(label, box);
                    break;
                case "action":
                    element = ActionButton(ctx, s, label, help);
                    break;
                case "table":
                    element = Table(ctx, s, label, help);
                    break;
                case "list":
                {
                    var editor = new ListEditor(s.Key, label, s.Columns, f.Token(s.Key) as JArray, ctx.Defaults?[s.Key] as JArray, help);
                    editor.Margin = new Thickness(0, 8, 0, 0);
                    element = string.IsNullOrEmpty(help) ? (FrameworkElement)editor : Stack(Ui.Note(help), editor);
                    break;
                }
                default:
                    element = f.TextField(label, s.Key, help);
                    break;
            }

            if (s.Requires != null)
            {
                var target = element;
                ctx.AfterBuild.Add(() =>
                {
                    if (!ctx.Parts.Switches.TryGetValue(s.Requires, out var gate))
                        return;
                    void Apply() => target.IsEnabled = gate.IsChecked == true;
                    gate.Checked += (sender, e) => Apply();
                    gate.Unchecked += (sender, e) => Apply();
                    Apply();
                });
            }
            return element;
        }

        private static void SetRange(PageContext ctx, Setting s)
        {
            if (!s.Min.HasValue && !s.Max.HasValue) return;
            if (ctx.Parts.Boxes.TryGetValue(s.Key, out var box) && box.Tag is FieldTag tag)
            {
                if (s.Min.HasValue) tag.Min = s.Min.Value;
                if (s.Max.HasValue) tag.Max = s.Max.Value;
            }
        }

        private static StackPanel Stack(params UIElement[] children)
        {
            var panel = new StackPanel();
            foreach (var child in children)
                panel.Children.Add(child);
            return panel;
        }

        private static FrameworkElement PathField(PageContext ctx, Setting s, string label, string help)
        {
            var box = ctx.Fields.SettingBox(label, s.Key, help + "\nA relative path starts in the Streamer.bot folder.");
            var open = Ui.IconButton(Icons.Folder, Icons.Font, "Open this folder");
            open.Margin = new Thickness(6, 0, 0, 0);
            open.Click += (sender, e) =>
            {
                string path = box.Text.Trim().Trim('"');
                if (path.Length == 0) return;
                if (!System.IO.Path.IsPathRooted(path))
                    path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                Web.OpenFolder(path, ctx.Log, ctx.Spec.LogPrefix);
            };
            var dock = new DockPanel();
            DockPanel.SetDock(open, Dock.Right);
            dock.Children.Add(open);
            dock.Children.Add(box);
            return Ui.Labeled(label, dock);
        }

        private static FrameworkElement ActionButton(PageContext ctx, Setting s, string label, string help)
        {
            var button = Ui.TextButton(label, false);
            button.Margin = new Thickness(0, 8, 0, 0);
            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.ToolTip = help;
            button.Click += (sender, e) =>
            {
                if (s.Confirm != null &&
                    MessageBox.Show(s.Confirm, label, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
                try
                {
                    string result = ctx.Hooks.Test(s.Key, ctx.Settings);
                    ctx.SetStatus(result ?? (label + ": done"), Theme.SuccessText, null);
                }
                catch (Exception ex)
                {
                    ctx.SetStatus(label + " failed", Theme.DangerText, ex.Message);
                }
            };
            return button;
        }

        private static FrameworkElement Table(PageContext ctx, Setting s, string label, string help)
        {
            var columns = s.Columns ?? new List<SettingColumn>();
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                RowHeaderWidth = 0,
                MaxHeight = 320,
                FontSize = 12,
                Background = new SolidColorBrush(Theme.ContentBg),
                RowBackground = new SolidColorBrush(Theme.ContentBg),
                Foreground = Theme.LabelFg,
                BorderBrush = new SolidColorBrush(Theme.Line),
                BorderThickness = new Thickness(1),
                HorizontalGridLinesBrush = new SolidColorBrush(Theme.Line),
                ColumnHeaderStyle = Ui.HeaderStyle(),
                ToolTip = help
            };
            var cellText = new Style(typeof(TextBlock));
            cellText.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6, 4, 6, 4)));
            foreach (var column in columns)
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = column.Label ?? column.Key,
                    Binding = new Binding("[" + column.Key + "]"),
                    Width = DataGridLength.Auto,
                    MinWidth = 50,
                    ElementStyle = cellText
                });

            var empty = Ui.Note("Nothing here yet.");
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            var host = new Grid();
            host.Children.Add(grid);
            host.Children.Add(empty);

            void Load()
            {
                var rows = new List<Dictionary<string, string>>();
                try
                {
                    string json = ctx.Hooks.ReadTable(s.Key);
                    if (!string.IsNullOrWhiteSpace(json) && JToken.Parse(json) is JArray array)
                        foreach (var item in array.OfType<JObject>())
                            rows.Add(item.Properties().ToDictionary(p => p.Name, p => p.Value?.ToString() ?? ""));
                }
                catch (Exception ex)
                {
                    ctx.Log.Warn(ctx.Spec.LogPrefix + " Table '" + s.Key + "' could not be read: " + ex.Message);
                }
                grid.ItemsSource = rows;
                empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            var reload = Ui.IconButton(Icons.Refresh, Icons.Font, "Read again");
            reload.Click += (sender, e) => Load();
            var head = new DockPanel { Margin = new Thickness(0, 6, 0, 4) };
            DockPanel.SetDock(reload, Dock.Right);
            head.Children.Add(reload);
            head.Children.Add(Ui.SmallLabel(label, 0));
            ctx.Parts.OnShow(s.Page ?? "", Load);
            Load();
            return Stack(head, host);
        }
    }
}
