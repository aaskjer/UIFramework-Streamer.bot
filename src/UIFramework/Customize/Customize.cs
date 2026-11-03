using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// The customise mode: fields get a grip and a menu (rename, hide, move, reset), rail entries too (rename,
    /// icon, hide, reset), both can be dragged to reorder or a field dropped on a page. Every change goes into
    /// the layout file at once and the window rebuilds from the four layers. Values cannot be edited while the
    /// mode is on; that keeps "where it sits" apart from "what it is".
    /// </summary>
    public static class Customize
    {
        private const string FieldFormat = "UIFramework.Field";
        private const string PageFormat = "UIFramework.Page";

        // ---- Fields ------------------------------------------------------------------------------------

        public static FrameworkElement Wrap(PageContext ctx, Setting setting, FrameworkElement element, string pageId)
        {
            element.IsEnabled = false;
            var grip = new TextBlock
            {
                Text = "⋮⋮",
                FontSize = 14,
                Foreground = Theme.SubFg,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.SizeAll,
                ToolTip = "Drag to another place on this page, or onto a page in the rail"
            };
            var tag = new TextBlock
            {
                Text = setting.Hidden ? "hidden" : "",
                FontSize = 11,
                Foreground = Theme.MutedFg,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var dock = new DockPanel();
            DockPanel.SetDock(grip, Dock.Left);
            DockPanel.SetDock(tag, Dock.Right);
            dock.Children.Add(grip);
            dock.Children.Add(tag);
            dock.Children.Add(element);

            var box = new Border
            {
                Tag = setting.Key,
                Child = dock,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 2, 0, 2),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Theme.Line),
                Background = new SolidColorBrush(Theme.Surface),
                Opacity = setting.Hidden ? 0.5 : 1.0,
                AllowDrop = true,
                Cursor = Cursors.Hand,
                ToolTip = setting.Key
            };

            var menu = new ContextMenu();
            menu.Items.Add(Item("Rename…", () =>
            {
                string name = Prompt(box, "Rename '" + setting.Label + "'", "Label", setting.Label);
                if (name == null) return;
                ctx.Layout.SetField(setting.Key, "label", name.Trim().Length == 0 ? null : (JToken)name.Trim());
                Commit(ctx, pageId);
            }));
            menu.Items.Add(Item(setting.Hidden ? "Show" : "Hide", () =>
            {
                ctx.Layout.SetField(setting.Key, "hidden", setting.Hidden ? null : (JToken)true);
                Commit(ctx, pageId);
            }));
            bool message = setting.Kind == "template" && setting.Page == "messages";
            if (message)
            {
                grip.ToolTip = "Drag to another place in the list";
                menu.Items.Add(new MenuItem { Header = "Stays on the Messages page", IsEnabled = false });
            }
            else
            {
                var move = new MenuItem { Header = "Move to page" };
                foreach (var page in ctx.Spec.Pages.Where(p => p.Build != null && p.Id != "about" && p.Id != "help" && p.Id != "log" && p.Id != "messages"))
                {
                    string targetId = page.Id;
                    var entry = Item(page.Title, () =>
                    {
                        ctx.Layout.SetField(setting.Key, "page", targetId);
                        ctx.Layout.SetField(setting.Key, "order", null);
                        Commit(ctx, targetId);
                    });
                    entry.IsEnabled = targetId != pageId;
                    move.Items.Add(entry);
                }
                menu.Items.Add(move);
            }
            menu.Items.Add(new Separator());
            var reset = Item("Reset this field", () => { ctx.Layout.ResetField(setting.Key); Commit(ctx, pageId); });
            reset.IsEnabled = ctx.Layout.Field(setting.Key) != null;
            menu.Items.Add(reset);
            box.ContextMenu = menu;

            // Drag: the key travels; drop on another field reorders, drop on a rail page moves.
            Point? pressed = null;
            box.PreviewMouseLeftButtonDown += (s, e) => pressed = e.GetPosition(box);
            box.PreviewMouseMove += (s, e) =>
            {
                if (pressed == null || e.LeftButton != MouseButtonState.Pressed) return;
                var delta = e.GetPosition(box) - pressed.Value;
                if (Math.Abs(delta.X) < 6 && Math.Abs(delta.Y) < 6) return;
                pressed = null;
                DragDrop.DoDragDrop(box, new DataObject(FieldFormat, setting.Key), DragDropEffects.Move);
            };
            box.PreviewMouseLeftButtonUp += (s, e) => pressed = null;
            // The drop itself is taken by the page (see AcceptDrops), so it lands wherever the mouse is let go.
            box.DragOver += (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent(FieldFormat) ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            return box;
        }

        /// <summary>Makes a customise page take a dragged field anywhere. Let go over another field, the dragged
        /// one takes that field's place: after it when it came from above, before it when it came from below.
        /// Let go over a heading or a gap, it goes between the fields there; below the last field, to the end.
        /// The page scrolls slowly while the drag is at its top or bottom edge.</summary>
        public static void AcceptDrops(PageContext ctx, ScrollViewer scroll, StackPanel page, string pageId)
        {
            if (string.IsNullOrEmpty(pageId)) return;
            page.Background = Brushes.Transparent;
            page.AllowDrop = true;
            scroll.AllowDrop = true;
            scroll.PreviewDragOver += (s, e) =>
            {
                if (!e.Data.GetDataPresent(FieldFormat)) return;
                double y = e.GetPosition(scroll).Y;
                if (y < 20) scroll.ScrollToVerticalOffset(scroll.VerticalOffset - 5);
                else if (y > scroll.ActualHeight - 20) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 5);
            };
            scroll.DragOver += (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent(FieldFormat) ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            scroll.Drop += (s, e) =>
            {
                if (!e.Data.GetDataPresent(FieldFormat)) return;
                string dragged = (string)e.Data.GetData(FieldFormat);
                double y = e.GetPosition(page).Y;
                e.Handled = true;
                var boxes = page.Children.OfType<Border>().Where(b => b.Tag is string).ToList();
                int from = boxes.FindIndex(b => (string)b.Tag == dragged);
                int over = boxes.FindIndex(b =>
                {
                    double top = b.TranslatePoint(new Point(0, 0), page).Y;
                    return y >= top && y < top + b.ActualHeight;
                });
                if (over >= 0 && over == from) return;
                string before = null;
                if (over >= 0 && from >= 0)
                {
                    // Over another field of this page: take its place, after it from above, before it from below.
                    int at = over > from ? over + 1 : over;
                    before = at < boxes.Count ? (string)boxes[at].Tag : null;
                }
                else
                {
                    // Over a heading, a gap or a field dragged in from another page: between the fields by height.
                    foreach (var box in boxes)
                    {
                        double top = box.TranslatePoint(new Point(0, 0), page).Y;
                        if (y < top + box.ActualHeight / 2)
                        {
                            before = (string)box.Tag;
                            break;
                        }
                    }
                }
                ReorderFields(ctx, pageId, dragged, before);
            };
        }

        /// <summary>Puts the dragged field in front of the target (null: at the end) and writes an order for every
        /// field on the page. Nothing is written when that leaves the page as it is.</summary>
        private static void ReorderFields(PageContext ctx, string pageId, string dragged, string before)
        {
            var onPage = ctx.Spec.ResolvedSettings
                .Where(s => s.Page == pageId)
                .OrderBy(s => s.Order).Select(s => s.Key).ToList();
            var draggedSetting = ctx.Spec.ResolvedSettings.FirstOrDefault(s => s.Key == dragged);
            if (draggedSetting == null || dragged == before) return;
            // A message template stays on the Messages page, and nothing else is dropped between the messages.
            if ((draggedSetting.Kind == "template" && draggedSetting.Page == "messages") != (pageId == "messages")) return;
            var wanted = new List<string>(onPage);
            wanted.Remove(dragged);
            int at = before == null ? wanted.Count : wanted.IndexOf(before);
            if (at < 0) at = wanted.Count;
            wanted.Insert(at, dragged);
            if (draggedSetting.Page == pageId && wanted.SequenceEqual(onPage)) return;
            if (draggedSetting.Page != pageId)
                ctx.Layout.SetField(dragged, "page", pageId);
            for (int i = 0; i < wanted.Count; i++)
                ctx.Layout.SetField(wanted[i], "order", i * 10);
            Commit(ctx, pageId);
        }

        /// <summary>The Messages page in the customise mode: one line per template, in groups, each draggable and
        /// with the field menu, so messages can be reordered, renamed and hidden like any other field.</summary>
        public static UIElement MessagesPage(PageContext ctx)
        {
            var page = Ui.PageStack();
            page.Children.Add(Ui.Note("Drag a message to another place in the list; right-click it to rename, hide or reset it. Messages stay on this page."));
            string group = null;
            var templates = ctx.Spec.ResolvedSettings.Where(s => s.Kind == "template" && s.Page == "messages").OrderBy(s => s.Order).ToList();
            foreach (var setting in templates)
            {
                string mine = setting.Group ?? "Messages";
                if (mine != group)
                {
                    group = mine;
                    page.Children.Add(Ui.Section(group, false));
                }
                string text = (ctx.Settings?[setting.Key]?.Type == JTokenType.String ? (string)ctx.Settings[setting.Key] : "") ?? "";
                text = text.Replace("\r", " ").Replace("\n", " ").Trim();
                var line = new DockPanel();
                var label = new TextBlock { Text = setting.Label ?? setting.Key, FontSize = 13, Foreground = Theme.LabelFg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), MinWidth = 180 };
                var value = new TextBlock { Text = text, FontSize = 12, Foreground = Theme.MutedFg, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                DockPanel.SetDock(label, Dock.Left);
                line.Children.Add(label);
                line.Children.Add(value);
                page.Children.Add(Wrap(ctx, setting, line, "messages"));
            }
            if (templates.Count == 0)
                page.Children.Add(Ui.Note("This host has no message templates."));
            var scroll = Ui.Scroll(page);
            AcceptDrops(ctx, scroll, page, "messages");
            return scroll;
        }

        // ---- Rail --------------------------------------------------------------------------------------

        public static void AttachRail(PageContext ctx, PageSpec page, Border item, Func<List<PageSpec>> topPages)
        {
            if (page.Hidden) item.Opacity = 0.5;
            item.AllowDrop = true;
            item.ToolTip = page.Title + "  ·  right-click to rename, hide or reset; drag to reorder";
            bool fixedPage = page.Id == "about";

            var menu = new ContextMenu();
            menu.Items.Add(Item("Rename…", () =>
            {
                string name = Prompt(item, "Rename the page '" + page.Title + "'", "Title", page.Title);
                if (name == null) return;
                ctx.Layout.SetPage(page.Id, "title", name.Trim().Length == 0 ? null : (JToken)name.Trim());
                Commit(ctx, page.Id);
            }));
            menu.Items.Add(Item("Change icon…", () =>
            {
                string icon = Prompt(item, "Icon for '" + page.Title + "'", "An emoji, or a name like gear, dice, coins, clock, shield, chat, trophy, monitor, list, book, info, cards, chart", page.Glyph);
                if (icon == null) return;
                ctx.Layout.SetPage(page.Id, "icon", icon.Trim().Length == 0 ? null : (JToken)icon.Trim());
                Commit(ctx, page.Id);
            }));
            var hide = Item(page.Hidden ? "Show" : "Hide", () =>
            {
                ctx.Layout.SetPage(page.Id, "hidden", page.Hidden ? null : (JToken)true);
                Commit(ctx, page.Hidden ? page.Id : null);
            });
            hide.IsEnabled = !fixedPage;
            menu.Items.Add(hide);
            menu.Items.Add(new Separator());
            var reset = Item("Reset this page", () => { ctx.Layout.ResetPage(page.Id); Commit(ctx, page.Id); });
            reset.IsEnabled = ctx.Layout.Page(page.Id) != null;
            menu.Items.Add(reset);
            item.ContextMenu = menu;

            Point? pressed = null;
            item.PreviewMouseLeftButtonDown += (s, e) => pressed = e.GetPosition(item);
            item.PreviewMouseMove += (s, e) =>
            {
                if (pressed == null || e.LeftButton != MouseButtonState.Pressed || page.Bottom) return;
                var delta = e.GetPosition(item) - pressed.Value;
                if (Math.Abs(delta.X) < 6 && Math.Abs(delta.Y) < 6) return;
                pressed = null;
                DragDrop.DoDragDrop(item, new DataObject(PageFormat, page.Id), DragDropEffects.Move);
            };
            item.PreviewMouseLeftButtonUp += (s, e) => pressed = null;
            item.DragOver += (s, e) =>
            {
                bool ok = (e.Data.GetDataPresent(PageFormat) && !page.Bottom) || (e.Data.GetDataPresent(FieldFormat) && page.Build != null && !fixedPage && page.Id != "help" && page.Id != "log" && page.Id != "messages");
                e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            item.Drop += (s, e) =>
            {
                if (e.Data.GetDataPresent(FieldFormat))
                {
                    string key = (string)e.Data.GetData(FieldFormat);
                    ctx.Layout.SetField(key, "page", page.Id);
                    ctx.Layout.SetField(key, "order", null);
                    Commit(ctx, page.Id);
                }
                else if (e.Data.GetDataPresent(PageFormat))
                {
                    string dragged = (string)e.Data.GetData(PageFormat);
                    if (dragged == page.Id) return;
                    var order = topPages().Select(p => p.Id).ToList();
                    order.Remove(dragged);
                    int at = order.IndexOf(page.Id);
                    order.Insert(at < 0 ? order.Count : at, dragged);
                    for (int i = 0; i < order.Count; i++)
                        ctx.Layout.SetPage(order[i], "order", i * 10);
                    Commit(ctx, dragged);
                }
                e.Handled = true;
            };
        }

        // ---- The bar under the header ------------------------------------------------------------------

        public static FrameworkElement Bar(PageContext ctx, Action done, Action exportSettings = null, Action importSettings = null)
        {
            var text = new TextBlock
            {
                FontSize = 12,
                Foreground = Theme.LabelFg,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            void DrawText()
            {
                int n = ctx.Layout.ChangeCount;
                text.Text = "Customise: drag a field to reorder it or onto a page in the rail to move it; right-click a field or a page to rename, hide or reset it. " +
                            (n == 0 ? "Nothing changed yet." : n == 1 ? "1 change saved." : n + " changes saved.");
            }
            DrawText();

            var accentFields = new Fields(ctx.Parts, new JObject { ["accent"] = ctx.Layout.Accent }, new JObject { ["accent"] = "" }, ctx.OpenUrl);
            var accent = accentFields.ColourField("Accent colour", "accent",
                "The highlight colour of this and every other UIFramework window: the blue buttons, switches that are on, the active page in the rail, links and this bar. Empty: the default blue.");
            if (ctx.Parts.Boxes.TryGetValue("accent", out var accentBox))
            {
                accentBox.Tag = null;   // not a setting; never saved into the host's file
                var fields = accentFields;
                // The picker writes into the box while it is open, and the box never had the focus then: the colour
                // is taken once the picker has closed, when typing pauses, on Enter and on leaving the box.
                var settle = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                void CommitAccent()
                {
                    if (fields != null && fields.PickerOpen) { settle.Stop(); settle.Start(); return; }
                    settle.Stop();
                    string hex = accentBox.Text.Trim();
                    if (hex == (ctx.Layout.Accent ?? "")) return;
                    if (hex.Length > 0 && !Colours.TryParseHex(hex, out _)) return;
                    ctx.Layout.Accent = hex;
                    Commit(ctx, null);
                }
                settle.Tick += (s, e) => CommitAccent();
                accentBox.TextChanged += (s, e) => { settle.Stop(); settle.Start(); };
                accentBox.LostFocus += (s, e) => CommitAccent();
                accentBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitAccent(); };
                ctx.Parts.Boxes.Remove("accent");
            }

            var language = Ui.PlainCombo("Language", "en", "de");
            language.SelectedItem = ctx.Layout.Language == "de" ? "de" : "en";
            language.Width = 70;
            language.SelectionChanged += (s, e) =>
            {
                string code = language.SelectedItem as string ?? "en";
                if (code == (ctx.Layout.Language ?? "en")) return;
                ctx.Layout.Language = code == "en" ? null : code;
                Commit(ctx, null);
            };

            var reset = Ui.TextButton("Reset layout", false);
            reset.Click += (s, e) =>
            {
                if (ctx.Layout.ChangeCount == 0) return;
                if (MessageBox.Show("Forget every renamed, moved and hidden page and field? The theme stays.", "Reset layout",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                ctx.Layout.ResetLayout();
                Commit(ctx, null);
            };
            var open = Ui.TextButton("Open file", false);
            open.ToolTip = ctx.Layout.Path;
            open.Click += (s, e) => Web.OpenUrl(ctx.Layout.Path, ctx.Log, ctx.Spec.LogPrefix);
            var export = Ui.TextButton("Export", false);
            export.ToolTip = "Write the saved settings to a file of your choosing";
            export.Click += (s, e) => exportSettings?.Invoke();
            var import = Ui.TextButton("Import", false);
            import.ToolTip = "Replace the saved settings with an exported file, or a settings file; the file as it was is kept as .bak";
            import.Click += (s, e) => importSettings?.Invoke();
            var finish = Ui.TextButton("Done", true);
            finish.Click += (s, e) => done();

            var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            controls.Children.Add(Ui.Labeled("Language", language));
            controls.Children.Add(reset);
            if (exportSettings != null) controls.Children.Add(export);
            if (importSettings != null) controls.Children.Add(import);
            controls.Children.Add(open);
            controls.Children.Add(finish);

            var top = new DockPanel();
            DockPanel.SetDock(controls, Dock.Right);
            top.Children.Add(controls);
            top.Children.Add(text);
            var panel = new StackPanel();
            panel.Children.Add(top);
            accent.Margin = new Thickness(0, 8, 0, 0);
            panel.Children.Add(accent);

            return new Border
            {
                Padding = new Thickness(16, 10, 14, 10),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Theme.Accent),
                Background = new SolidColorBrush(Theme.AccentSoft),
                Child = panel
            };
        }

        // ---- Helpers -----------------------------------------------------------------------------------

        private static void Commit(PageContext ctx, string pageId)
        {
            try { ctx.Layout.Save(); }
            catch (Exception ex)
            {
                MessageBox.Show("The layout could not be written:\n" + ex.Message, "Customise", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ctx.Reopen?.Invoke(pageId);
        }

        private static MenuItem Item(string header, Action click)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => click();
            return item;
        }

        /// <summary>A small input dialog in the window's look. Null when cancelled.</summary>
        public static string Prompt(FrameworkElement owner, string title, string label, string current)
        {
            var box = new TextBox { Text = current ?? "", MinWidth = 320 };
            Ui.StyleInput(box);
            var ok = Ui.TextButton("OK", true);
            var cancel = Ui.TextButton("Cancel", false);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            var body = new StackPanel { Margin = new Thickness(16) };
            body.Children.Add(Ui.Labeled(label, box));
            body.Children.Add(buttons);
            var dialog = new Window
            {
                Title = title,
                Content = body,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(owner),
                Background = new SolidColorBrush(Theme.ContentBg),
                FontSize = 13,
                ShowInTaskbar = false
            };
            bool accepted = false;
            ok.Click += (s, e) => { accepted = true; dialog.Close(); };
            cancel.Click += (s, e) => dialog.Close();
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { accepted = true; dialog.Close(); }
                else if (e.Key == Key.Escape) dialog.Close();
            };
            dialog.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
            dialog.ShowDialog();
            return accepted ? box.Text : null;
        }
    }
}
