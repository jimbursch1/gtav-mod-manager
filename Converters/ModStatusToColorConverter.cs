using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using GtavModManager.Core;

namespace GtavModManager.Converters
{
    [ValueConversion(typeof(ModStatus), typeof(Brush))]
    public class ModStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ModStatus status)
            {
                switch (status)
                {
                    case ModStatus.Enabled: return new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
                    case ModStatus.Disabled: return new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
                    default: return new SolidColorBrush(Color.FromRgb(0xA0, 0xA8, 0xC0));
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
