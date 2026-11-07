using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace UIFramework
{
    /// <summary>
    /// Shows an element while any of some others is visible (a section heading over its fields). A binding, not a
    /// DependencyPropertyDescriptor.AddValueChanged: that one is held by a static table for as long as the process
    /// runs, so every rebuild of a page (a language switch, a theme switch, the customise mode) kept the old page,
    /// window and all, alive in Streamer.bot. A binding lets go with the elements.
    /// </summary>
    public static class VisibilityWatch
    {
        public static void ShowWhileAnyVisible(FrameworkElement target, IEnumerable<FrameworkElement> sources)
        {
            var binding = new MultiBinding { Converter = AnyVisible.Instance, Mode = BindingMode.OneWay };
            foreach (var source in sources)
                binding.Bindings.Add(new Binding { Source = source, Path = new PropertyPath(UIElement.VisibilityProperty), Mode = BindingMode.OneWay });
            if (binding.Bindings.Count > 0)
                target.SetBinding(UIElement.VisibilityProperty, binding);
        }

        private sealed class AnyVisible : IMultiValueConverter
        {
            public static readonly AnyVisible Instance = new AnyVisible();

            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
                values.Any(v => v is Visibility visibility && visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
        }
    }
}
