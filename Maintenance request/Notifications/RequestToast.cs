using Maintenance.Shared;

namespace Maintenance_request.Notifications;

// بيانات القالب المخصص؛ العملية الأصلية تبقى ظاهرة حتى لو استقرت حالة الطلب بعد الاستلام.
public sealed class RequestToast
{
    #region بيانات عنوان الإشعار وتفاصيله ولون العملية
    public int Operation { get; init; }
    public long RequestId { get; init; }
    public string Details { get; init; } = string.Empty;
    public string Title => RequestNotificationText.Status(Operation);
    public string Text => RequestNotificationText.Client(Operation);
    public string AccentColor => RequestNotificationAppearance.For(Operation).AccentColor;
    public string TintColor => RequestNotificationAppearance.For(Operation).TintColor;
    #endregion
}
