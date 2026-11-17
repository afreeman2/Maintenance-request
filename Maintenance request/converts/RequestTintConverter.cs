using Maintenance.Shared;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Maintenance_request.converts;

// بطاقة الطلب وصفه في القائمة يستعملان لون العملية نفسه الذي يعرضه التنبيه العائم.
public sealed class RequestTintConverter : IValueConverter
{
    #region تحويل نوع العملية إلى خلفية الطلب
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int operation = value is int number ? number : RequestProtocol.New;
        return new BrushConverter().ConvertFromString(RequestNotificationAppearance.For(operation).TintColor)!;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
    #endregion
}
