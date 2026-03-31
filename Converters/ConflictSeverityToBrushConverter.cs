using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using GtavModManager.Core;

namespace GtavModManager.Converters
{
    [ValueConversion(typeof(ConflictSeverity), typeof(Brush))]
    public class ConflictSeverityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConflictSeverity severity)
            {
                switch (severity)
                {
                    case ConflictSeverity.Error: return new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
                    case ConflictSeverity.Warning: return new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26));
                    default: return new SolidColorBrush(Color.FromRgb(0xA0, 0xA8, 0xC0));
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
