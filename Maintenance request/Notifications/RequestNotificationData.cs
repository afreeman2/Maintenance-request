using System.ComponentModel.DataAnnotations.Schema;

namespace Maintenance_request.data;

public partial class T_Notif_Action_User
{
    #region بيانات عرض الإشعار دون تعديل مخطط القاعدة
    [NotMapped] public int NotificationOperation { get; set; }
    [NotMapped] public DateTime NotificationRevision { get; set; }
    #endregion
}
