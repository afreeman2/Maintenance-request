using Maintenance_request.Notification;
using System;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace Maintenance_request.Notifications
{
    public class NotificationModel
    {

        public string Title { get; set; } = "";

        public string Message { get; set; } = "";


        // أي بيانات تريد تمريرها
        public object? Tag { get; set; }



        public NotificationType Type { get; set; }



        public TimeSpan Duration { get; set; }
            = TimeSpan.FromSeconds(5);



        public Brush Color
        {
            get
            {
                return Type switch
                {
                    NotificationType.Success =>
                    Brushes.SeaGreen,

                    NotificationType.Warning =>
                    Brushes.DarkOrange,

                    NotificationType.Error =>
                    Brushes.Firebrick,

                    _ =>
                    Brushes.DodgerBlue
                };
            }
        }

    }
}