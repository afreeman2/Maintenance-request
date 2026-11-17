using System;
using System.Collections.Generic;
using System.Text;

namespace Maintenance_request.Notification
{
    public enum Notificationenum
    {
        Activated,
        UserCanceled,
        TimedOut
    }
    public enum NotificationType
    {
        Information,
        Success,
        Warning,
        Error
    }
}
