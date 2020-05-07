using System;
using System.Windows;
using System.Windows.Data;

namespace GPhotosMirror.Converters
{
    class InvertedBooleanToVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
                          System.Globalization.CultureInfo culture)
        {
            bool sender = (bool)value;
            if (!sender)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }


        public object ConvertBack(object value, Type targetType, object parameter,
                        System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
