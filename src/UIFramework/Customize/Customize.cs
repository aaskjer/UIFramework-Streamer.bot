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
        // The layout file of the window a drag started in: with two games' windows open, a field or page dragged
        // from one is not dropped into the other (where a key of the same name would be moved instead).
        private const string OwnerFormat = "UIFramework.Layout";

        private static DataObject DragData(PageContext ctx, string format, string id)
        {
            var data = new DataObject(format, id);
            data.SetData(OwnerFormat, ctx.Layout?.Path ?? "");
            return data;
        }

        /// <summary>The field key or page id being dragged, when it is one of this window's; null otherwise.</summary>
        private static string Dragged(PageContext ctx, IDataObject data, string format)
        {
            if (!data.GetDataPresent(format) || !data.GetDataPresent(OwnerFormat)) return null;
            if (!string.Equals(data.GetData(OwnerFormat) as string, ctx.Layout?.Path ?? "", StringComparison.OrdinalIgnoreCase)) return null;
            return data.GetData(format) as string;
        }

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
                ToolTip = Text.T("cust.drag.field")
            };
            var tag = new TextBlock
            {
                Text = setting.Hidden ? Text.T("cust.hidden") : "",
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
            menu.Items.Add(Item(Text.T("cust.rename"), () =>
            {
                string name = Prompt(box, Text.T("cust.rename.field", setting.Label), Text.T("cust.label"), setting.Label);
                if (name == null) return;
                ctx.Layout.SetField(setting.Key, "label", name.Trim().Length == 0 ? null : (JToken)name.Trim());
                Commit(ctx, pageId);
            }));
            menu.Items.Add(Item(setting.Hidden ? Text.T("cust.show") : Text.T("cust.hide"), () =>
            {
                ctx.Layout.SetField(setting.Key, "hidden", setting.Hidden ? null : (JToken)true);
                Commit(ctx, pageId);
            }));
            bool message = setting.Kind == "template" && setting.Page == "messages";
            if (message)
            {
                grip.ToolTip = Text.T("cust.drag.message");
                menu.Items.Add(new MenuItem { Header = Text.T("cust.staysMessages"), IsEnabled = false });
            }
            else
            {
                var move = new MenuItem { Header = Text.T("cust.moveTo") };
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
            var reset = Item(Text.T("cust.resetField"), () => { ctx.Layout.ResetField(setting.Key); Commit(ctx, pageId); });
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
                DragDrop.DoDragDrop(box, DragData(ctx, FieldFormat, setting.Key), DragDropEffects.Move);
            };
            box.PreviewMouseLeftButtonUp += (s, e) => pressed = null;
            // The drop itself is taken by the page (see AcceptDrops), so it lands wherever the mouse is let go. While
            // a field is dragged over this one, an accent edge shows where it would go: left or right of it on the same
            // line, above or below it on a line of its own.
            var line = new SolidColorBrush(Theme.Line);
            var accent = new SolidColorBrush(Theme.Accent);
            void Plain() { box.BorderBrush = line; box.BorderThickness = new Thickness(1); }
            box.DragOver += (s, e) =>
            {
                string over = Dragged(ctx, e.Data, FieldFormat);
                bool field = over != null && over != setting.Key;
                e.Effects = over != null ? DragDropEffects.Move : DragDropEffects.None;
                if (field)
                {
                    box.BorderBrush = accent;
                    switch (Zone(box, e.GetPosition(box)))
                    {
                        case DropZone.Left: box.BorderThickness = new Thickness(4, 1, 1, 1); break;
                        case DropZone.Right: box.BorderThickness = new Thickness(1, 1, 4, 1); break;
                        case DropZone.Above: box.BorderThickness = new Thickness(1, 4, 1, 1); break;
                        default: box.BorderThickness = new Thickness(1, 1, 1, 4); break;
                    }
                }
                e.Handled = true;
            };
            box.DragLeave += (s, e) => Plain();
            box.Drop += (s, e) => Plain();
            return box;
        }

        private enum DropZone { Left, Right, Above, Below }

        /// <summary>Where a drop over a field lands: the outer quarters of its width put the dragged field beside it,
        /// the rest above or below it on a line of its own.</summary>
        private static DropZone Zone(FrameworkElement box, Point at)
        {
            double w = box.ActualWidth, h = box.ActualHeight;
            if (w > 0 && at.X < w * 0.25) return DropZone.Left;
            if (w > 0 && at.X > w * 0.75) return DropZone.Right;
            return h > 0 && at.Y < h / 2 ? DropZone.Above : DropZone.Below;
        }

        /// <summary>The field frame under a point of the page, wherever it sits (a line of its own or a row).</summary>
        private static Border FieldAt(StackPanel page, Point at)
        {
            Border found = null;
            VisualTreeHelper.HitTest(page, null, result =>
            {
                for (DependencyObject d = result.VisualHit; d != null && d != page; d = VisualTreeHelper.GetParent(d))
                    if (d is Border b && b.Tag is string && b.AllowDrop)
                    {
                        found = b;
                        return HitTestResultBehavior.Stop;
                    }
                return HitTestResultBehavior.Continue;
            }, new PointHitTestParameters(at));
            return found;
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
                if (Dragged(ctx, e.Data, FieldFormat) == null) return;
                double y = e.GetPosition(scroll).Y;
                if (y < 20) scroll.ScrollToVerticalOffset(scroll.VerticalOffset - 5);
                else if (y > scroll.ActualHeight - 20) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 5);
            };
            scroll.DragOver += (s, e) =>
            {
                e.Effects = Dragged(ctx, e.Data, FieldFormat) != null ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            scroll.Drop += (s, e) =>
            {
                string dragged = Dragged(ctx, e.Data, FieldFormat);
                if (dragged == null) return;
                double y = e.GetPosition(page).Y;
                e.Handled = true;

                // Over another field: beside it on its line (left or right quarter), or above or below it on a line
                // of its own.
                var target = FieldAt(page, e.GetPosition(page));
                if (target != null && (string)target.Tag != dragged)
                {
                    var zone = Zone(target, e.GetPosition(target));
                    PlaceField(ctx, pageId, dragged, (string)target.Tag, zone == DropZone.Left || zone == DropZone.Above, zone == DropZone.Left || zone == DropZone.Right);
                    return;
                }

                var boxes = Frames(page).ToList();
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

        /// <summary>Every field frame of a page in reading order, rows included.</summary>
        private static IEnumerable<Border> Frames(Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is Border b && b.Tag is string)
                    yield return b;
                else if (child is Panel inner)
                    foreach (var nested in Frames(inner))
                        yield return nested;
            }
        }

        /// <summary>Puts the dragged field before or after the target. Beside it: both on the target's line (its row,
        /// or a new one named after it). Above or below it: the dragged field on a line of its own.</summary>
        private static void PlaceField(PageContext ctx, string pageId, string dragged, string target, bool before, bool beside)
        {
            var targetSetting = ctx.Spec.ResolvedSettings.FirstOrDefault(s => s.Key == target);
            var draggedSetting = ctx.Spec.ResolvedSettings.FirstOrDefault(s => s.Key == dragged);
            if (targetSetting == null || draggedSetting == null) return;
            if ((draggedSetting.Kind == "template" && draggedSetting.Page == "messages") != (pageId == "messages")) return;
            if (beside)
            {
                string row = targetSetting.Row;
                if (!SharesRow(row))
                {
                    row = "row-" + target;
                    ctx.Layout.SetField(target, "row", row);
                }
                ctx.Layout.SetField(dragged, "row", row);
            }
            else
                ctx.Layout.SetField(dragged, "row", "");
            var onPage = ctx.Spec.ResolvedSettings.Where(s => s.Page == pageId).OrderBy(s => s.Order).Select(s => s.Key).ToList();
            onPage.Remove(dragged);
            int at = onPage.IndexOf(target);
            int first = at, last = at;
            if (!beside && at >= 0 && SharesRow(targetSetting.Row))
            {
                // Above or below a field that shares a line with others: above or below that whole line, so the row
                // stays together.
                string RowOf(string key) => ctx.Spec.ResolvedSettings.FirstOrDefault(s => s.Key == key)?.Row;
                while (first > 0 && RowOf(onPage[first - 1]) == targetSetting.Row) first--;
                while (last < onPage.Count - 1 && RowOf(onPage[last + 1]) == targetSetting.Row) last++;
            }
            onPage.Insert(at < 0 ? onPage.Count : before ? first : last + 1, dragged);
            if (draggedSetting.Page != pageId)
                ctx.Layout.SetField(dragged, "page", pageId);
            for (int i = 0; i < onPage.Count; i++)
                ctx.Layout.SetField(onPage[i], "order", i * 10);
            Commit(ctx, pageId);
        }

        /// <summary>A row others can share: named, not the "~key" a field on a line of its own carries.</summary>
        private static bool SharesRow(string row) => !string.IsNullOrEmpty(row) && !row.StartsWith("~", StringComparison.Ordinal);

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
            page.Children.Add(Ui.Note(Text.T("cust.messages.note")));
            string group = null;
            var templates = ctx.Spec.ResolvedSettings.Where(s => s.Kind == "template" && s.Page == "messages").OrderBy(s => s.Order).ToList();
            foreach (var setting in templates)
            {
                string mine = setting.Group ?? Text.T("cust.messages.group");
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
                page.Children.Add(Ui.Note(Text.T("cust.noTemplates")));
            var scroll = Ui.Scroll(page);
            AcceptDrops(ctx, scroll, page, "messages");
            return scroll;
        }

        // ---- Rail --------------------------------------------------------------------------------------

        public static void AttachRail(PageContext ctx, PageSpec page, Border item, Func<List<PageSpec>> topPages)
        {
            if (page.Hidden) item.Opacity = 0.5;
            item.AllowDrop = true;
            item.ToolTip = Text.T("cust.page.tip", page.Title);
            bool fixedPage = page.Id == "about";

            var menu = new ContextMenu();
            menu.Items.Add(Item(Text.T("cust.rename"), () =>
            {
                string name = Prompt(item, Text.T("cust.rename.page", page.Title), Text.T("cust.title"), page.Title);
                if (name == null) return;
                ctx.Layout.SetPage(page.Id, "title", name.Trim().Length == 0 ? null : (JToken)name.Trim());
                Commit(ctx, page.Id);
            }));
            menu.Items.Add(Item(Text.T("cust.changeIcon"), () =>
            {
                string icon = Prompt(item, Text.T("cust.iconFor", page.Title), Text.T("cust.iconHelp"), page.Glyph);
                if (icon == null) return;
                ctx.Layout.SetPage(page.Id, "icon", icon.Trim().Length == 0 ? null : (JToken)icon.Trim());
                Commit(ctx, page.Id);
            }));
            var hide = Item(page.Hidden ? Text.T("cust.show") : Text.T("cust.hide"), () =>
            {
                ctx.Layout.SetPage(page.Id, "hidden", page.Hidden ? null : (JToken)true);
                Commit(ctx, page.Hidden ? page.Id : null);
            });
            hide.IsEnabled = !fixedPage;
            menu.Items.Add(hide);
            menu.Items.Add(new Separator());
            var reset = Item(Text.T("cust.resetPage"), () => { ctx.Layout.ResetPage(page.Id); Commit(ctx, page.Id); });
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
                DragDrop.DoDragDrop(item, DragData(ctx, PageFormat, page.Id), DragDropEffects.Move);
            };
            item.PreviewMouseLeftButtonUp += (s, e) => pressed = null;
            // A field this page can take: one of this window's, not a message template (those stay on Messages), and
            // not already here. The drop checks it again; DragOver alone does not stop every drop.
            Setting FieldFor(DragEventArgs e)
            {
                string key = Dragged(ctx, e.Data, FieldFormat);
                if (key == null || page.Build == null || fixedPage || page.Id == "help" || page.Id == "log" || page.Id == "messages") return null;
                var setting = ctx.Spec.ResolvedSettings.FirstOrDefault(x => x.Key == key);
                if (setting == null || (setting.Kind == "template" && setting.Page == "messages") || setting.Page == page.Id) return null;
                return setting;
            }
            item.DragOver += (s, e) =>
            {
                bool ok = (Dragged(ctx, e.Data, PageFormat) != null && !page.Bottom) || FieldFor(e) != null;
                e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            item.Drop += (s, e) =>
            {
                e.Handled = true;
                if (e.Data.GetDataPresent(FieldFormat))
                {
                    var setting = FieldFor(e);
                    if (setting == null) return;
                    ctx.Layout.SetField(setting.Key, "page", page.Id);
                    ctx.Layout.SetField(setting.Key, "order", null);
                    // A row is a line of the page it was on: the field arrives on a line of its own.
                    if (!string.IsNullOrEmpty(setting.Row)) ctx.Layout.SetField(setting.Key, "row", "");
                    Commit(ctx, page.Id);
                }
                else if (e.Data.GetDataPresent(PageFormat))
                {
                    string dragged = Dragged(ctx, e.Data, PageFormat);
                    if (dragged == null || dragged == page.Id || page.Bottom) return;
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

        /// <summary>A small label to the left of a control, on one line with it.</summary>
        private static FrameworkElement Beside(string label, FrameworkElement control, double gapBefore)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = Theme.SubFg,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(gapBefore, 0, 6, 0)
            });
            row.Children.Add(control);
            return row;
        }

        public static FrameworkElement Bar(PageContext ctx, Action done, Action exportSettings = null, Action importSettings = null)
        {
            var text = new TextBlock
            {
                FontSize = 11,
                Foreground = Theme.LabelFg,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };
            void DrawText()
            {
                int n = ctx.Layout.ChangeCount;
                text.Text = Text.T("cust.bar") + (n == 0 ? Text.T("cust.changes.none") : n == 1 ? Text.T("cust.changes.one") : Text.T("cust.changes.many", n));
            }
            DrawText();

            var accentFields = new Fields(ctx.Parts, new JObject { ["accent"] = ctx.Layout.Accent }, new JObject { ["accent"] = "" }, ctx.OpenUrl);
            var accent = accentFields.ColourField(Text.T("cust.accent"), "accent", Text.T("cust.accent.tip"), labelled: false);
            if (ctx.Parts.Boxes.TryGetValue("accent", out var accentBox))
            {
                accentBox.Tag = null;   // not a setting; never saved into the host's file
                accentBox.Width = 76;   // a hex code is seven characters; the bar is one row
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

            var language = LanguagePicker(ctx.Spec, ctx.Layout, () => Commit(ctx, null));

            // The help of every field as a line under it instead of in its tooltip; the window is built again with it.
            // A switch of its own on the hint's line: the top row is full with the colour, the language and the buttons.
            var helpLines = new CheckBox
            {
                Content = Text.T("cust.helpLines"),
                IsChecked = ctx.Layout.HelpLines,
                ToolTip = Text.T("cust.helpLines.tip"),
                Template = Ui.SwitchTemplate(),
                Foreground = Theme.LabelFg,
                FontSize = 11,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            System.Windows.Automation.AutomationProperties.SetName(helpLines, Text.T("cust.helpLines"));
            helpLines.Click += (s, e) =>
            {
                ctx.Layout.HelpLines = helpLines.IsChecked == true;
                Commit(ctx, null);
            };

            // Icon buttons like the footer's: the texts are their tooltips, so the bar stays one row wide.
            var reset = Ui.IconButton(Icons.Undo, Icons.Font, Text.T("cust.resetLayout"));
            reset.Click += (s, e) =>
            {
                if (ctx.Layout.ChangeCount == 0) return;
                if (MessageBox.Show(Text.T("cust.resetLayout.confirm"), Text.T("cust.resetLayout"),
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                ctx.Layout.ResetLayout();
                Commit(ctx, null);
            };
            var open = Ui.IconButton(Icons.Open, Icons.Font, Text.T("cust.openFile") + "\n" + ctx.Layout.Path);
            open.Click += (s, e) => Web.OpenUrl(ctx.Layout.Path, ctx.Log, ctx.Spec.LogPrefix);
            var export = Ui.IconButton(Icons.Export, Icons.Font, Text.T("cust.export") + ": " + Text.T("cust.export.tip"));
            export.Click += (s, e) => exportSettings?.Invoke();
            var import = Ui.IconButton(Icons.Import, Icons.Font, Text.T("cust.import") + ": " + Text.T("cust.import.tip"));
            import.Click += (s, e) => importSettings?.Invoke();
            var finish = Ui.TextButton(Text.T("btn.done"), true);
            finish.Margin = new Thickness(8, 0, 0, 0);
            finish.Click += (s, e) => done();

            // One row: the colour and the language on the left, the buttons on the right (wrapping under them
            // when the window is narrow), and the hint as one line of its own underneath, over the full width.
            var choices = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            choices.Children.Add(Beside(Text.T("cust.accent"), accent, 0));
            if (language != null)
                choices.Children.Add(Beside(Text.T("cust.language"), language, 12));
            var controls = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            foreach (var button in new[] { reset, export, import, open })
            {
                button.Margin = new Thickness(1, 0, 1, 0);
                button.Foreground = Theme.LabelFg;
            }
            controls.Children.Add(reset);
            if (exportSettings != null) controls.Children.Add(export);
            if (importSettings != null) controls.Children.Add(import);
            controls.Children.Add(open);
            controls.Children.Add(finish);

            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(controls, 1);
            top.Children.Add(choices);
            top.Children.Add(controls);
            // Under the row: the help switch, then the hint with the count of changes, wrapping beside it.
            var second = new DockPanel { Margin = new Thickness(0, 8, 0, 0), LastChildFill = true };
            DockPanel.SetDock(helpLines, Dock.Left);
            second.Children.Add(helpLines);
            text.Margin = new Thickness(0);
            text.VerticalAlignment = VerticalAlignment.Center;
            second.Children.Add(text);
            var panel = new StackPanel();
            panel.Children.Add(top);
            panel.Children.Add(second);

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

        /// <summary>The languages the window offers, or null when there is only English to pick.</summary>
        public static ComboBox LanguagePicker(WindowSpec spec, LayoutFile layout, Action changed)
        {
            // A game that names its window's language has no picker: what the user picked would not apply.
            if (spec.Languages == null || spec.Languages.Count < 2 || !string.IsNullOrEmpty(spec.Language))
                return null;
            var codes = spec.Languages.ToArray();
            var language = Ui.PlainCombo(Text.T("cust.language"), codes.Select(LanguageName).ToArray());
            string current = layout.Language;
            language.SelectedIndex = Math.Max(0, Array.IndexOf(codes, current != null && codes.Contains(current) ? current : "en"));
            language.Width = 110;
            language.SelectionChanged += (s, e) =>
            {
                string code = language.SelectedIndex >= 0 ? codes[language.SelectedIndex] : "en";
                if (code == (layout.Language ?? "en")) return;
                layout.Language = code == "en" ? null : code;
                changed();
            };
            return language;
        }

        /// <summary>A language by its own name ("Deutsch", "Español"); the code where Windows does not know it.</summary>
        public static string LanguageName(string code)
        {
            try
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo(code);
                string name = culture.NativeName;
                bool unknown = string.IsNullOrEmpty(name) || culture.ThreeLetterISOLanguageName == "ivl" || culture.EnglishName.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase);
                return unknown ? code : culture.TextInfo.ToTitleCase(name);
            }
            catch (Exception) { return code; }
        }

        private static void Commit(PageContext ctx, string pageId)
        {
            try { ctx.Layout.Save(); }
            catch (Exception ex)
            {
                MessageBox.Show(Text.T("cust.layoutFailed.text", ex.Message), Text.T("cust.layoutFailed.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
            var ok = Ui.TextButton(Text.T("btn.ok"), true);
            var cancel = Ui.TextButton(Text.T("btn.cancel"), false);
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
