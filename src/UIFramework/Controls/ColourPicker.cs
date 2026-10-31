using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace UIFramework
{
    /// <summary>
    /// A field for saturation and brightness and a hue bar, both to click or drag; sliders for red, green and
    /// blue; a row of ready colours. Every change goes straight into the hex box, and typing in the hex box moves
    /// the picker along. One instance per window, so opening a second picker closes the first.
    /// </summary>
    public sealed class ColourPicker
    {
        public static readonly string[] Presets =
            { "#F1C40F", "#E67E22", "#E74C3C", "#E91E63", "#9B59B6", "#5865F2", "#3498DB", "#1ABC9C", "#2ECC71", "#95A5A6" };

        private Popup _popup;

        public void Open(TextBox target, FrameworkElement anchor)
        {
            if (_popup != null)
                _popup.IsOpen = false;
            if (!Colours.TryParseHex(target.Text, out Color start))
                start = Color.FromRgb(0xF1, 0xC4, 0x0F);
            Colours.RgbToHsv(start, out double hue, out double saturation, out double brightness);
            Color current = start;
            bool writing = false;

            const double fieldWidth = 220, fieldHeight = 140, trackWidth = 172;
            var hueLayer = new Border { Width = fieldWidth, Height = fieldHeight };
            var whiteLayer = new Border { Width = fieldWidth, Height = fieldHeight, Background = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) };
            var blackLayer = new Border { Width = fieldWidth, Height = fieldHeight, Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) };
            var fieldMarker = new Ellipse { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
            var field = new Canvas { Name = "ColourField", Width = fieldWidth, Height = fieldHeight, Background = Brushes.Transparent, ClipToBounds = true, Cursor = Cursors.Cross };
            field.Children.Add(hueLayer);
            field.Children.Add(whiteLayer);
            field.Children.Add(blackLayer);
            field.Children.Add(fieldMarker);

            var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            for (int i = 0; i <= 6; i++)
                rainbow.GradientStops.Add(new GradientStop(Colours.HsvToRgb(i * 60, 1, 1), i / 6.0));
            var hueMarker = new Border { Width = 22, Height = 5, Background = Brushes.White, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), IsHitTestVisible = false };
            var hueBar = new Canvas { Name = "HueBar", Width = 18, Height = fieldHeight, Background = rainbow, Margin = new Thickness(10, 0, 0, 0), Cursor = Cursors.Hand };
            hueBar.Children.Add(hueMarker);

            var tracks = new Border[3];
            var thumbs = new Border[3];
            var values = new TextBlock[3];
            var sliders = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            string[] names = { "R", "G", "B" };
            Action<int, double> setChannel = null;
            for (int i = 0; i < 3; i++)
            {
                int channel = i;
                tracks[i] = new Border { Width = trackWidth, Height = 12, CornerRadius = new CornerRadius(6), VerticalAlignment = VerticalAlignment.Center };
                thumbs[i] = new Border { Width = 8, Height = 18, CornerRadius = new CornerRadius(3), Background = Brushes.White, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), IsHitTestVisible = false };
                Canvas.SetTop(thumbs[i], -3);
                var slider = new Canvas { Name = "Slider" + names[i], Width = trackWidth, Height = 12, Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(0, 4, 0, 4) };
                slider.Children.Add(tracks[i]);
                slider.Children.Add(thumbs[i]);
                values[i] = new TextBlock { Width = 34, FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = Theme.LabelFg, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                var line = new DockPanel();
                var label = new TextBlock { Text = names[i], Width = 16, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Theme.SubFg, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(label, Dock.Left);
                DockPanel.SetDock(values[i], Dock.Right);
                line.Children.Add(label);
                line.Children.Add(values[i]);
                line.Children.Add(slider);
                sliders.Children.Add(line);
                Ui.Draggable(slider, at => setChannel(channel, Math.Max(0, Math.Min(1, at.X / trackWidth)) * 255));
                slider.MouseWheel += (s, e) => setChannel(channel, Colours.ChannelOf(current, channel) + Math.Sign(e.Delta));
            }

            var preview = new Border { Width = 34, Height = 26, CornerRadius = new CornerRadius(4), BorderBrush = new SolidColorBrush(Theme.LineStrong), BorderThickness = new Thickness(1) };
            var hexText = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 13, Foreground = Theme.LabelFg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };

            Action<bool> show = write =>
            {
                current = Colours.HsvToRgb(hue, saturation, brightness);
                hueLayer.Background = new SolidColorBrush(Colours.HsvToRgb(hue, 1, 1));
                Canvas.SetLeft(fieldMarker, saturation * fieldWidth - 6);
                Canvas.SetTop(fieldMarker, (1 - brightness) * fieldHeight - 6);
                fieldMarker.Stroke = brightness > 0.6 && saturation < 0.4 ? Brushes.Black : Brushes.White;
                Canvas.SetLeft(hueMarker, -2);
                Canvas.SetTop(hueMarker, hue / 360 * fieldHeight - 2.5);
                for (int i = 0; i < 3; i++)
                {
                    tracks[i].Background = new LinearGradientBrush(Colours.WithChannel(current, i, 0), Colours.WithChannel(current, i, 255), 0);
                    Canvas.SetLeft(thumbs[i], Colours.ChannelOf(current, i) / 255.0 * trackWidth - 4);
                    values[i].Text = Colours.ChannelOf(current, i).ToString(CultureInfo.InvariantCulture);
                }
                preview.Background = new SolidColorBrush(current);
                hexText.Text = Colours.ToHex(current);
                if (write && !string.Equals(target.Text.Trim(), Colours.ToHex(current), StringComparison.OrdinalIgnoreCase))
                {
                    writing = true;
                    target.Text = Colours.ToHex(current);
                    writing = false;
                }
            };
            Action<Color> showColour = colour =>
            {
                Colours.RgbToHsv(colour, out double h, out double s, out double v);
                if (s > 0)
                    hue = h;
                saturation = s;
                brightness = v;
                show(true);
            };
            setChannel = (channel, value) => showColour(Colours.WithChannel(current, channel, (int)Math.Round(Math.Max(0, Math.Min(255, value)))));
            Ui.Draggable(field, at =>
            {
                saturation = Math.Max(0, Math.Min(1, at.X / fieldWidth));
                brightness = 1 - Math.Max(0, Math.Min(1, at.Y / fieldHeight));
                show(true);
            });
            Ui.Draggable(hueBar, at =>
            {
                hue = Math.Max(0, Math.Min(359.9, at.Y / fieldHeight * 360));
                show(true);
            });

            var presets = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (string hex in Presets)
            {
                Colours.TryParseHex(hex, out Color colour);
                var chip = new Border
                {
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(0, 0, 6, 0),
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(colour),
                    BorderBrush = new SolidColorBrush(Theme.LineStrong),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    ToolTip = hex
                };
                AutomationProperties.SetName(chip, hex);
                chip.MouseLeftButtonUp += (s, e) => showColour(colour);
                presets.Children.Add(chip);
            }

            var done = Ui.TextButton("Done", true);
            done.Margin = new Thickness(0);
            var bottom = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(done, Dock.Right);
            bottom.Children.Add(done);
            bottom.Children.Add(preview);
            bottom.Children.Add(hexText);

            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(field);
            top.Children.Add(hueBar);
            var body = new StackPanel();
            body.Children.Add(top);
            body.Children.Add(sliders);
            body.Children.Add(presets);
            body.Children.Add(bottom);

            var popup = new Popup
            {
                PlacementTarget = anchor,
                Placement = PlacementMode.Bottom,
                VerticalOffset = 6,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = new Border
                {
                    Background = new SolidColorBrush(Theme.PopupBg),
                    BorderBrush = new SolidColorBrush(Theme.LineStrong),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Child = body
                }
            };
            TextChangedEventHandler typed = null;
            typed = (s, e) =>
            {
                if (!popup.IsOpen)
                {
                    target.TextChanged -= typed;
                    return;
                }
                if (!writing && Colours.TryParseHex(target.Text, out Color colour) && colour != current)
                {
                    Colours.RgbToHsv(colour, out double h, out double sat, out double v);
                    if (sat > 0)
                        hue = h;
                    saturation = sat;
                    brightness = v;
                    show(false);
                }
            };
            target.TextChanged += typed;
            popup.Closed += (s, e) =>
            {
                target.TextChanged -= typed;
                if (_popup == popup)
                    _popup = null;
            };
            done.Click += (s, e) => popup.IsOpen = false;
            show(false);
            _popup = popup;
            popup.IsOpen = true;
        }
    }
}
