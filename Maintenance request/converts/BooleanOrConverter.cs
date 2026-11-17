using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace Maintenance_request.converts
{
    class BooleanOrConverter : IMultiValueConverter

    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            foreach (Object value in values)
            {
                if ((Boolean)value == true)
                    return true;
            }

            return false;



        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

}
