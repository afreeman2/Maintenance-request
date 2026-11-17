using Maintenance_request.data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace Maintenance_request.converts
{
    internal class OpIdGroupConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                T_Notif_Action_User opId = (T_Notif_Action_User)value;

                switch (opId.OP_ID)
                {
                    case 1:
                        return "جاري الطلب";

                    case 2:
                        return "جاري التنفيذ";

                    case 3:
                        return "تم التنفيذ";
                }
            }

            return "غير معروف";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
