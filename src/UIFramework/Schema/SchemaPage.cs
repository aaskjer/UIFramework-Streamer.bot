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
    /// switches two to a row, settings naming the same <c>row</c> side by side, everything else on its own line.
    /// Each kind maps to one of the <see cref="Fields"/>.
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
            // The page as the window shows it, rows and all, every field in a draggable frame; hidden ones dimmed.
            string pageId = settings.Count > 0 ? settings[0].Page : "";
            Fill(ctx, page, settings, true, (s, element) => Customize.Wrap(ctx, s, element, pageId));
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

        private static void ShowAnswerBox(StackPanel panel)
        {
            if (panel.Parent is Border box && Equals(box.Tag, AnswerBox))
                box.Visibility = panel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Marks the box an action's answer goes into, so a row can put it under itself.</summary>
        private const string AnswerBox = "action-answer";

        private static void Fill(PageContext ctx, StackPanel page, List<Setting> settings, bool firstHeading,
            Func<Setting, FrameworkElement, FrameworkElement> wrap = null)
        {
            string section = null;
            var row = new List<FrameworkElement>();
            string rowKind = null;
            int rowMax = 1;
            FrameworkElement heading = null;
            var sectionFields = new List<FrameworkElement>();

            void Flush()
            {
                if (row.Count == 0) return;
                // A number on its own keeps a third of the width, as it would beside two others.
                if (row.Count == 1 && rowKind != "bool" && rowKind != "number")
                    page.Children.Add(row[0]);
                else
                {
                    var cells = row.ToList();
                    while (cells.Count < rowMax && rowKind == "number")
                        cells.Add(new TextBlock());
                    // In a named row a button takes the room it needs, and an answer box goes under the whole row.
                    var answers = new List<FrameworkElement>();
                    var weights = cells.Select(c => 1.0).ToArray();
                    if (rowKind != null && rowKind.StartsWith("row ", StringComparison.Ordinal))
                        for (int i = 0; i < cells.Count; i++)
                        {
                            if (cells[i] is StackPanel stack && stack.Children.Count == 2 && stack.Children[0] is Button
                                && stack.Children[1] is Border box && Equals(box.Tag, AnswerBox))
                            {
                                stack.Children.Remove(box);
                                // Under the row, the answer still follows its action: hidden while the action is.
                                var holder = new Border { Child = box };
                                holder.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding("Visibility") { Source = stack });
                                answers.Add(holder);
                            }
                            if (cells[i] is Button || (cells[i] is StackPanel only && only.Children.Count == 1 && only.Children[0] is Button))
                                weights[i] = 0;
                        }
                    page.Children.Add(Ui.Row(weights, cells.ToArray()));
                    foreach (var box in answers)
                        page.Children.Add(box);
                }
                row.Clear();
                rowKind = null;
            }

            // A heading whose every field is hidden (requires with hide:) hides with them.
            void WatchSection()
            {
                if (heading == null || sectionFields.Count == 0) return;
                VisibilityWatch.ShowWhileAnyVisible(heading, sectionFields.ToList());
            }

            foreach (var setting in settings.Where(s => wrap != null || !s.Hidden).OrderBy(s => s.Order))
            {
                if (setting.Section != null && setting.Section != section)
                {
                    Flush();
                    WatchSection();
                    section = setting.Section;
                    heading = Ui.Section(section, firstHeading && page.Children.Count == 0);
                    sectionFields.Clear();
                    page.Children.Add(heading);
                }

                var element = Render(ctx, setting);
                if (element == null)
                    continue;
                if (wrap != null)
                    element = wrap(setting, element);
                sectionFields.Add(element);

                string kind = NumberKinds.Contains(setting.Kind) ? "number" : setting.Kind == "bool" ? "bool" : "single";
                int max = kind == "number" ? 3 : kind == "bool" ? 2 : 1;
                if (!string.IsNullOrEmpty(setting.Row))
                {
                    // Settings naming the same row, one after the other, share it whatever their kind.
                    kind = "row " + setting.Row;
                    max = int.MaxValue;
                    InRow(element);
                }
                if (kind == "single" || (rowKind != null && rowKind != kind) || row.Count >= rowMax)
                    Flush();
                rowKind = kind;
                rowMax = max;
                row.Add(element);
                if (kind == "single")
                    Flush();
            }
            Flush();
            WatchSection();
        }

        /// <summary>A button or a switch beside labelled fields sits on their baseline.</summary>
        private static void InRow(FrameworkElement element)
        {
            if (element is Button button)
                button.VerticalAlignment = VerticalAlignment.Bottom;
            else if (element is CheckBox toggle)
                Ui.Held(toggle);
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
                {
                    var toggle = f.Toggle(label, s.Key, help);
                    if (s.TwitchOnly) Fields.TwitchOnly(toggle);
                    element = toggle;
                    break;
                }
                case "int":
                    element = s.Options != null && s.Options.Count > 0
                        ? f.IntCombo(label, s.Key, s.Options.Select(o => (int.TryParse(o.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0, o.Label)).ToArray(), help)
                        : f.IntField(label, s.Key, help, s.UnlimitedAtZero);
                    SetRange(ctx, s);
                    break;
                case "long":
                    element = f.LongField(label, s.Key, help, s.UnlimitedAtZero);
                    SetRange(ctx, s);
                    break;
                case "duration":
                    element = f.DurationField(label, s.Key, help, s.Unit, s.UnlimitedAtZero);
                    SetRange(ctx, s);
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
                    element = options.Length == 0 ? (s.Masked ? f.MaskedField(label, s.Key, help) : f.TextField(label, s.Key, help)) : f.StringComboField(label, s.Key, options, help);
                    break;
                case "namelist":
                    element = Stack(Ui.SmallLabel(label, 6), f.NameList(label, s.Key, Text.T("field.namelist.hint"), help));
                    break;
                case "grouplist":
                    element = Stack(Ui.SmallLabel(label, 6), f.NameList(label, s.Key, Text.T("field.grouplist.hint"), help));
                    break;
                case "colour":
                case "color":
                    element = f.ColourField(label, s.Key, help);
                    break;
                case "path":
                    element = PathField(ctx, s, label, help);
                    break;
                case "emote":
                    element = EmoteField(ctx, s, label, help);
                    break;
                case "slider":
                {
                    bool stops = s.Options != null && s.Options.Count > 0;
                    bool whole = s.Default == null || s.Default.Type != JTokenType.Float;
                    element = f.SliderField(label, s.Key, help, s.Options, s.Min ?? 0, s.Max ?? (stops ? s.Options.Count - 1 : 100), s.Step ?? (whole ? 1 : 0.1), whole, s.Unit);
                    break;
                }
                case "note":
                {
                    var note = Ui.NoteWithLinks(string.IsNullOrEmpty(help) ? (s.Default?.Type == JTokenType.String ? (string)s.Default : "") : help, ctx.OpenUrl);
                    note.Margin = new Thickness(0, 8, 0, 0);
                    element = note;
                    break;
                }
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
                    var editor = new ListEditor(s.Key, label, Translated(ctx, s.Columns), f.Token(s.Key) as JArray, ctx.Defaults?[s.Key] as JArray, help, ctx.Hooks);
                    ctx.Parts.Lists[s.Key] = editor;
                    editor.Margin = new Thickness(0, 8, 0, 0);
                    element = string.IsNullOrEmpty(help) ? (FrameworkElement)editor : Stack(Ui.Note(help), editor);
                    break;
                }
                default:
                    element = s.Masked ? f.MaskedField(label, s.Key, help) : f.TextField(label, s.Key, help);
                    break;
            }
            if (s.Transient)
                FieldBinding.MarkTransient(element);
            if (s.Live && s.Kind != "action")
            {
                // A field that asks the host whenever it changes; `set` in the answer fills in other fields (a preset slider).
                var source = element;
                ctx.AfterBuild.Add(() => FieldBinding.HookChangeTracking(source, () => Ask(ctx, s, label, null)));
            }

            // Greyed out while `requires` does not hold; hidden while `showWhen`, the section's condition or a
            // `requires` with "hide:" in front does not. All of the hiding conditions must hold to show the field.
            var target = element;
            string grey = null;
            var show = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.Requires))
            {
                string condition = s.Requires.Trim();
                if (condition.StartsWith("hide:", StringComparison.OrdinalIgnoreCase)) show.Add(condition.Substring(5).Trim());
                else grey = condition;
            }
            if (!string.IsNullOrWhiteSpace(s.ShowWhen)) show.Add(s.ShowWhen.Trim());
            if (!string.IsNullOrWhiteSpace(s.SectionShowWhen)) show.Add(s.SectionShowWhen.Trim());
            if (ctx.Customizing)
                return element;
            if (grey != null)
                ctx.AfterBuild.Add(() => Requirement.Wire(ctx.Fields, ctx.Parts, grey, on => Grey(target, !on)));
            if (show.Count > 0)
            {
                string all = string.Join(" && ", show);
                ctx.AfterBuild.Add(() => Requirement.Wire(ctx.Fields, ctx.Parts, all, on => target.Visibility = on ? Visibility.Visible : Visibility.Collapsed));
            }
            return element;
        }

        /// <summary>Greys a field out, or back in. Disabled alone looks the same as enabled on some kinds (a list, a
        /// slider, a label above a box), so the whole field is also dimmed.</summary>
        public static void Grey(UIElement field, bool grey)
        {
            field.IsEnabled = !grey;
            field.Opacity = grey ? 0.5 : 1.0;
        }

        /// <summary>The columns with their labels in the window's language: a declared label the conventions or the
        /// game's own translations know is replaced, the rest stays as declared.</summary>
        private static List<SettingColumn> Translated(PageContext ctx, List<SettingColumn> columns)
        {
            var lang = ctx.Spec.Conventions?.Lang;
            if (columns == null || lang == null || !lang.Translates)
                return columns;
            return columns.Select(c => new SettingColumn { Key = c.Key, Kind = c.Kind, Label = lang.Text(c.Label), Platform = c.Platform, ReadOnly = c.ReadOnly }).ToList();
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

        /// <summary>A text box for one emote with its picture beside it (see <see cref="EmotePictures"/>).</summary>
        private static FrameworkElement EmoteField(PageContext ctx, Setting s, string label, string help)
        {
            var box = ctx.Fields.SettingBox(label, s.Key, help);
            string platform = (s.Platform ?? "twitch").ToLowerInvariant();
            if (platform == "youtube")
                return Ui.Labeled(label, box);
            var image = EmotePictures.Picture(platform, box.Text, ctx.Hooks);
            image.Margin = new Thickness(0, 2, 6, 0);
            box.LostFocus += (sender, e) => EmotePictures.Show(image, platform, box.Text, ctx.Hooks);
            var dock = new DockPanel();
            DockPanel.SetDock(image, Dock.Left);
            dock.Children.Add(image);
            dock.Children.Add(box);
            return Ui.Labeled(label, dock);
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

        /// <summary>An action: a button that asks the host. With <c>live</c> the host gets the values as they stand
        /// in the window; with <c>panel</c> its answer goes into a panel under the button; with <c>auto</c> there is
        /// no button, the host is asked when the page is shown and after every change, a moment after the last.</summary>
        private static FrameworkElement ActionButton(PageContext ctx, Setting s, string label, string help)
        {
            StackPanel panel = null;
            if (s.Panel || s.Auto)
                panel = new StackPanel();

            void Run() => Ask(ctx, s, label, panel);

            if (s.Auto)
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                timer.Tick += (sender, e) => { timer.Stop(); Run(); };
                ctx.OnAnyChange(() => { timer.Stop(); timer.Start(); });
                ctx.Parts.OnShow(s.Page ?? "", () => { timer.Stop(); Run(); });
                var box = new Border
                {
                    Background = new SolidColorBrush(Theme.Surface),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 8, 0, 0),
                    ToolTip = string.IsNullOrEmpty(help) ? null : help,
                    Child = panel
                };
                return Stack(Ui.SmallLabel(label, 0), box);
            }

            var button = Ui.TextButton(label, false);
            button.Margin = new Thickness(0, 8, 0, 0);
            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.ToolTip = help;
            button.Click += (sender, e) =>
            {
                if (s.Confirm != null &&
                    MessageBox.Show(s.Confirm, label, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
                Run();
            };
            if (panel == null)
                return button;
            // The answer box shows once there is an answer in it, not as an empty bar before the first click.
            var result = new Border
            {
                Tag = AnswerBox,
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Theme.Surface),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 8, 0, 0),
                Child = panel
            };
            return Stack(button, result);
        }

        /// <summary>Asks the host about a setting and places the answer: "set" (values for other fields of the window,
        /// applied as if typed, nothing saved) and "status" (a line for the footer) are taken out of a JSON answer; the
        /// rest goes to the panel when there is one, to the footer otherwise.</summary>
        private static void Ask(PageContext ctx, Setting s, string label, StackPanel panel)
        {
            try
            {
                var settings = s.Live && ctx.LiveSettings != null ? ctx.LiveSettings() : ctx.Settings;
                string result = ctx.Hooks.Test(s.Key, settings);
                JObject structured = ResultPanel.TryParse(result);
                if (structured?["set"] is JObject values)
                    LiveValues.Apply(ctx.Parts, values);
                string status = (string)structured?["status"];
                string clipboard = (string)structured?["clipboard"];
                if (clipboard != null)
                {
                    // The window's thread is an STA one, which the clipboard wants; the game's is not.
                    try { Clipboard.SetText(clipboard); }
                    catch (Exception ex) { status = (status ?? "") + " (the clipboard was busy: " + ex.Message + ")"; }
                }
                if (panel != null)
                {
                    ResultPanel.Render(panel, result ?? "", ctx.Hooks);
                    ShowAnswerBox(panel);
                }
                else if (structured != null)
                {
                    if (status != null || s.Kind == "action")
                        ctx.SetStatus(status ?? (label + ": done"), Theme.SuccessText, null);
                }
                else if (result != null || s.Kind == "action")
                    ctx.SetStatus(result ?? (label + ": done"), Theme.SuccessText, null);
            }
            catch (Exception ex)
            {
                if (panel != null) { ResultPanel.Render(panel, label + " failed: " + ex.Message); ShowAnswerBox(panel); }
                else ctx.SetStatus(label + " failed", Theme.DangerText, ex.Message);
            }
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
