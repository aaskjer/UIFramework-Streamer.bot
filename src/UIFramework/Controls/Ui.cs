using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Run = System.Windows.Documents.Run;
using Hyperlink = System.Windows.Documents.Hyperlink;
using TextElement = System.Windows.Documents.TextElement;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace UIFramework
{
    /// <summary>
    /// The furniture every page is built from: headings, notes, rows, pills, buttons, links and the control
    /// templates that give text boxes, switches and combos the window's look. Everything here reads the theme
    /// at call time, so a page built after a theme switch comes out in the other palette.
    /// </summary>
    public static class Ui
    {
        public static StackPanel PageStack() => new StackPanel { Margin = new Thickness(16, 2, 16, 20) };

        public static ScrollViewer Scroll(UIElement content)
        {
            return new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }

        public static TextBlock Section(string text, bool first = false)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.MutedFg,
                Margin = new Thickness(0, first ? 12 : 20, 0, 7)
            };
        }

        public static TextBlock Note(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = Theme.MutedFg,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
        }

        public static TextBlock SmallLabel(string text, double top)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = Theme.SubFg,
                Margin = new Thickness(0, top, 0, 4)
            };
        }

        public static FrameworkElement Labeled(string label, FrameworkElement control)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = Theme.SubFg,
                Margin = new Thickness(0, 0, 0, 4),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            panel.Children.Add(control);
            AutomationProperties.SetName(control, label);
            return panel;
        }

        /// <summary>Drops a switch down by the height of the labels beside it, so a Row of fields and switches
        /// lines up along one baseline instead of stepping.</summary>
        public static FrameworkElement Held(FrameworkElement control)
        {
            control.Margin = new Thickness(0, 17, 0, 0);
            control.VerticalAlignment = VerticalAlignment.Top;
            return control;
        }

        public static Grid Row(params FrameworkElement[] cells) => Row(cells.Select(c => 1.0).ToArray(), cells);

        public static Grid Row(double[] weights, params FrameworkElement[] cells)
        {
            var grid = new Grid();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0)
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weights[i], GridUnitType.Star) });
                Grid.SetColumn(cells[i], i * 2);
                grid.Children.Add(cells[i]);
            }
            return grid;
        }

        public static (int Value, string Label)[] Range(int from, int to)
        {
            var items = new (int, string)[to - from + 1];
            for (int i = 0; i < items.Length; i++)
                items[i] = (from + i, (from + i).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return items;
        }

        public static Border Pill(string text, Color background, Color foreground)
        {
            var pill = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 2, 9, 3),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { FontSize = 11 }
            };
            SetPill(pill, text, background, foreground);
            return pill;
        }

        public static void SetPill(Border pill, string text, Color background, Color foreground)
        {
            var label = (TextBlock)pill.Child;
            label.Text = text;
            label.Foreground = new SolidColorBrush(foreground);
            pill.Background = new SolidColorBrush(background);
        }

        public static (Color Background, Color Foreground) PillColours(PillStyle style)
        {
            switch (style)
            {
                case PillStyle.Success: return (Theme.SuccessSoft, Theme.SuccessText);
                case PillStyle.Warning: return (Theme.WarningSoft, Theme.WarningText);
                case PillStyle.Accent: return (Theme.AccentSoft, Theme.AccentText);
                case PillStyle.Muted: return (Theme.Surface, Theme.SubColor);
                default: return (Colors.Transparent, Theme.SubColor);
            }
        }

        public static TextBlock CommandRow(string command, string what)
        {
            var block = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 3)
            };
            block.Inlines.Add(new Run(command) { FontFamily = new FontFamily("Consolas"), Foreground = Theme.LabelFg });
            block.Inlines.Add(new Run("  " + what) { Foreground = Theme.MutedFg });
            return block;
        }

        public static TextBlock LinkText(string text, string url, string tooltip, Action<string> open)
        {
            var link = new Hyperlink { Foreground = new SolidColorBrush(Theme.AccentText), Cursor = Cursors.Hand };
            link.Inlines.Add(new Run(text));
            link.Inlines.Add(new Run(" " + Icons.Open) { FontFamily = Icons.Font, FontSize = 10 });
            link.Click += (s, e) => open(url);
            var block = new TextBlock { FontSize = 13, ToolTip = tooltip ?? url };
            block.Inlines.Add(link);
            AutomationProperties.SetName(block, text);
            return block;
        }

        public static TextBlock LinkLine(string text, string url, Action<string> open)
        {
            var line = LinkText(text, url, null, open);
            line.Margin = new Thickness(0, 3, 0, 3);
            return line;
        }

        public static Button TextButton(string text, bool primary)
        {
            return new Button
            {
                Content = text,
                Height = 28,
                MinWidth = 64,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                Foreground = primary ? Brushes.White : Theme.LabelFg,
                FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
                Template = primary
                    ? ButtonTemplate(Theme.Primary, Theme.PrimaryHover, Theme.Primary, 6, new Thickness(14, 0, 14, 0))
                    : ButtonTemplate(Colors.Transparent, Theme.Hover, Theme.LineStrong, 6, new Thickness(12, 0, 12, 0))
            };
        }

        public static Button IconButton(string glyph, FontFamily font, string tooltip)
        {
            var button = new Button
            {
                Content = glyph,
                Width = 28,
                Height = 28,
                FontSize = 14,
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Foreground = Theme.SubFg,
                Template = ButtonTemplate(Colors.Transparent, Theme.Hover, Theme.LineStrong, 6, new Thickness(0))
            };
            if (font != null)
                button.FontFamily = font;
            AutomationProperties.SetName(button, tooltip);
            return button;
        }

        public static Button ChipButton(string text, string tooltip)
        {
            var chip = new Button
            {
                Content = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Height = 24,
                Margin = new Thickness(0, 0, 6, 6),
                Focusable = false,
                Cursor = Cursors.Hand,
                Foreground = Theme.LabelFg,
                ToolTip = tooltip,
                Template = ButtonTemplate(Colors.Transparent, Theme.Hover, Theme.LineStrong, 5, new Thickness(8, 0, 8, 0))
            };
            AutomationProperties.SetName(chip, "Insert " + text);
            return chip;
        }

        public static void StyleColors(TextBox box)
        {
            box.Background = new SolidColorBrush(Theme.InputBg);
            box.Foreground = new SolidColorBrush(Theme.InputFg);
            box.BorderBrush = new SolidColorBrush(Theme.InputBorder);
            box.CaretBrush = new SolidColorBrush(Theme.InputFg);
        }

        public static void StyleInput(TextBox box)
        {
            StyleColors(box);
            box.Height = 28;
            box.Padding = new Thickness(4, 0, 4, 0);
            box.VerticalContentAlignment = VerticalAlignment.Center;
        }

        public static void DigitsOnly(TextBox box)
        {
            box.PreviewTextInput += (s, e) => e.Handled = !FieldBinding.IsDigits(e.Text);
            box.PreviewKeyDown += (s, e) => { if (e.Key == Key.Space) e.Handled = true; };
            DataObject.AddPastingHandler(box, (s, e) =>
            {
                if (e.DataObject.GetDataPresent(DataFormats.Text))
                {
                    var paste = (string)e.DataObject.GetData(DataFormats.Text);
                    if (!FieldBinding.IsDigits(paste))
                        e.CancelCommand();
                }
                else
                    e.CancelCommand();
            });
        }

        public static void DecimalsOnly(TextBox box)
        {
            box.PreviewTextInput += (s, e) => e.Handled = !e.Text.All(c => (c >= '0' && c <= '9') || c == '.' || c == ',');
        }

        /// <summary>A grey hint inside an empty box.</summary>
        public static Grid WithHint(TextBox box, string hint)
        {
            var text = new TextBlock
            {
                Text = hint,
                FontSize = 12,
                Foreground = Theme.MutedFg,
                Margin = new Thickness(7, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            var host = new Grid();
            host.Children.Add(box);
            host.Children.Add(text);
            text.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            box.TextChanged += (s, e) => text.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            return host;
        }

        public static ComboBox PlainCombo(string name, params string[] options)
        {
            var combo = new ComboBox
            {
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Template = ComboTemplate(),
                ItemContainerStyle = ComboItemStyle(),
                Foreground = new SolidColorBrush(Theme.InputFg)
            };
            foreach (var option in options)
                combo.Items.Add(option);
            if (options.Length > 0)
                combo.SelectedIndex = 0;
            AutomationProperties.SetName(combo, name);
            return combo;
        }

        public static void InsertAtCaret(TextBox box, string text, bool atEnd)
        {
            int start = atEnd ? box.Text.Length : Math.Min(box.SelectionStart, box.Text.Length);
            int length = atEnd ? 0 : Math.Min(box.SelectionLength, box.Text.Length - start);
            box.Text = box.Text.Remove(start, length).Insert(start, text);
            box.CaretIndex = start + text.Length;
            box.Focus();
        }

        /// <summary>Click, then drag while the button is held: the point is handed over as long as the mouse is
        /// captured.</summary>
        public static void Draggable(UIElement area, Action<Point> pick)
        {
            area.MouseLeftButtonDown += (s, e) =>
            {
                area.CaptureMouse();
                pick(e.GetPosition(area));
                e.Handled = true;
            };
            area.MouseMove += (s, e) =>
            {
                if (area.IsMouseCaptured)
                    pick(e.GetPosition(area));
            };
            area.MouseLeftButtonUp += (s, e) =>
            {
                if (area.IsMouseCaptured)
                    area.ReleaseMouseCapture();
            };
        }

        // ---- Templates ----------------------------------------------------------------------------------

        public static ControlTemplate ButtonTemplate(Color background, Color hover, Color border, double radius, Thickness padding)
        {
            var face = new FrameworkElementFactory(typeof(Border), "Face");
            face.SetValue(Border.BackgroundProperty, new SolidColorBrush(background));
            face.SetValue(Border.BorderBrushProperty, new SolidColorBrush(border));
            face.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            face.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            face.SetValue(Border.PaddingProperty, padding);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            face.AppendChild(content);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = face };
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hover), "Face"));
            var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.8, "Face"));
            template.Triggers.Add(over);
            template.Triggers.Add(pressed);
            return template;
        }

        public static ControlTemplate SwitchTemplate()
        {
            var track = new FrameworkElementFactory(typeof(Border), "Track");
            track.SetValue(FrameworkElement.WidthProperty, 30.0);
            track.SetValue(FrameworkElement.HeightProperty, 18.0);
            track.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            track.SetValue(Border.BackgroundProperty, new SolidColorBrush(Theme.ToggleOff));
            track.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            var knob = new FrameworkElementFactory(typeof(Ellipse), "Knob");
            knob.SetValue(FrameworkElement.WidthProperty, 14.0);
            knob.SetValue(FrameworkElement.HeightProperty, 14.0);
            knob.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 2, 0));
            knob.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            knob.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            knob.SetValue(System.Windows.Shapes.Shape.FillProperty, Brushes.White);
            track.AppendChild(knob);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            var root = new FrameworkElementFactory(typeof(StackPanel));
            root.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            root.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
            root.AppendChild(track);
            root.AppendChild(content);
            var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = root };
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85, "Track"));
            var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            on.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Theme.Accent), "Track"));
            on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "Knob"));
            template.Triggers.Add(over);
            template.Triggers.Add(on);
            return template;
        }

        public static ControlTemplate ComboTemplate()
        {
            var face = new FrameworkElementFactory(typeof(Border), "Face");
            face.SetValue(Border.BackgroundProperty, new SolidColorBrush(Theme.InputBg));
            face.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Theme.InputBorder));
            face.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            face.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            var chevron = new FrameworkElementFactory(typeof(TextBlock));
            chevron.SetValue(TextBlock.TextProperty, Icons.ChevronDown);
            chevron.SetValue(TextBlock.FontFamilyProperty, Icons.Font);
            chevron.SetValue(TextBlock.FontSizeProperty, 9.0);
            chevron.SetValue(TextBlock.ForegroundProperty, Theme.SubFg);
            chevron.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            chevron.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            chevron.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 9, 0));
            face.AppendChild(chevron);
            var toggleTemplate = new ControlTemplate(typeof(ToggleButton)) { VisualTree = face };
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Theme.Accent), "Face"));
            toggleTemplate.Triggers.Add(over);

            var toggle = new FrameworkElementFactory(typeof(ToggleButton));
            toggle.SetValue(Control.TemplateProperty, toggleTemplate);
            toggle.SetValue(UIElement.FocusableProperty, false);
            toggle.SetValue(ButtonBase.ClickModeProperty, ClickMode.Press);
            toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen") { RelativeSource = RelativeSource.TemplatedParent, Mode = BindingMode.TwoWay });

            var shown = new FrameworkElementFactory(typeof(ContentPresenter));
            shown.SetBinding(ContentPresenter.ContentProperty, new Binding("SelectionBoxItem") { RelativeSource = RelativeSource.TemplatedParent });
            shown.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 26, 0));
            shown.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            shown.SetValue(UIElement.IsHitTestVisibleProperty, false);
            shown.SetValue(TextElement.ForegroundProperty, new SolidColorBrush(Theme.InputFg));

            var items = new FrameworkElementFactory(typeof(ItemsPresenter));
            items.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Contained);
            var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
            scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scroll.AppendChild(items);
            var drop = new FrameworkElementFactory(typeof(Border));
            drop.SetValue(Border.BackgroundProperty, new SolidColorBrush(Theme.PopupBg));
            drop.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Theme.LineStrong));
            drop.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            drop.SetValue(Border.PaddingProperty, new Thickness(0, 3, 0, 3));
            drop.SetBinding(FrameworkElement.MinWidthProperty, new Binding("ActualWidth") { RelativeSource = RelativeSource.TemplatedParent });
            drop.SetBinding(FrameworkElement.MaxHeightProperty, new Binding("MaxDropDownHeight") { RelativeSource = RelativeSource.TemplatedParent });
            drop.AppendChild(scroll);
            var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.None);
            popup.SetValue(UIElement.FocusableProperty, false);
            popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen") { RelativeSource = RelativeSource.TemplatedParent });
            popup.AppendChild(drop);

            var root = new FrameworkElementFactory(typeof(Grid));
            root.AppendChild(toggle);
            root.AppendChild(shown);
            root.AppendChild(popup);
            return new ControlTemplate(typeof(ComboBox)) { VisualTree = root };
        }

        public static Style ComboItemStyle()
        {
            var style = new Style(typeof(ComboBoxItem));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
            return style;
        }

        public static Style HeaderStyle()
        {
            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Theme.ContentBg)));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, Theme.SubFg));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 5, 6, 5)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Theme.Line)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            return headerStyle;
        }
    }
}
