using DevExpress.CodeParser;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.POCO;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Maintenance_request.converts
{
    public class NotificationMainViewModel
    {
        [ServiceProperty(Key = "ServiceWithDefaultNotifications")]
        public virtual INotificationService DefaultNotificationService { get { return null; } }
        [ServiceProperty(Key = "ServiceWithCustomNotifications")]
        public virtual INotificationService CustomNotificationService { get { return null; } }

        public virtual INotificationService NotificationService => null;
        public int PredefinedNotificationId { get; set; }

        public NotificationMainViewModel()
        {
            PredefinedNotificationId = 100;
        }
        public void ShowDefaultNotification(string Caption, string Content)
        {
            INotification notification = DefaultNotificationService.CreatePredefinedNotification("Predefined Notification", "First line", String.Format("Second line. Time: {0}", DateTime.Now), null);
            notification.ShowAsync();
        }

        public void ShowCustomNotification(string Caption,string Content)
        {
            CustomNotificationViewModel vm = ViewModelSource.Create(() => new CustomNotificationViewModel());
            vm.Caption = Caption;
            vm.Content = Content;
            if (CustomNotificationService == null)
            {
                Maintenance_request.Support.UiMessages.Show("CustomNotificationService = NULL");
                return;
            }
            INotification notification = CustomNotificationService.CreateCustomNotification(vm);
            notification.ShowAsync().ContinueWith(OnNotificationShown, TaskScheduler.FromCurrentSynchronizationContext()); ;
        }
        public void ShowDefaultNotification()
        {
            INotification notification = DefaultNotificationService.CreatePredefinedNotification("Predefined Notification", "First line", String.Format("Second line. Time: {0}", DateTime.Now), null);
            notification.ShowAsync();
        }
        // أضف هذا السطر فقط لمنع خطأ الـ WPF Binding Engine نهائياً عند عدم وجود خاصية OP_ID
        public int? OP_ID { get; set; } = 0;

        public void ShowNotification()
        {
            string text1 = "Lorem ipsum dolor sit amet integer fringilla, dui eget ultrices cursus, justo tellus.";
            string text2 = "In ornare ante magna, eget volutpat mi bibendum a. Nam ut ullamcorper libero. Pellentesque habitant.";
            string text3 = "Quisque sapien odio, mollis tincidunt est id, fringilla euismod neque. Aenean adipiscing lorem dui, nec. ";

            INotification notification = DefaultNotificationService.CreatePredefinedNotification(text1, text2, text3, null);
            PredefinedNotificationId++;
            Show(notification);
        }

        public void Show(INotification notification)
        {
            CreateLogLine("Showing...");
            Observe(notification.ShowAsync());
        }
        #region متابعة الإشعار المعروض دون إعادة إظهاره
        // تعرض الواجهة التنبيه مرة واحدة ثم تمرر مهمته هنا لمعالجة النقرة أو الإغلاق.
        public void Observe(Task<NotificationResult> showing) => showing.ContinueWith(OnNotificationShown, TaskScheduler.FromCurrentSynchronizationContext());
        #endregion
        void OnNotificationShown(Task<NotificationResult> task)
        {
            try
            {
                if (task.IsFaulted || task.IsCanceled)
                    return;

                switch (task.Result)
                {
                    case NotificationResult.Activated:
                        CreateLogLine("Activated via Task");

                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            var mainWindow = Application.Current.Windows
                                .OfType<MainWindow>()
                                .FirstOrDefault();

                            mainWindow?.HandleNotificationClick();
                        });
                        break;

                    case NotificationResult.TimedOut:
                        CreateLogLine("Timed out");
                        break;

                    case NotificationResult.UserCanceled:
                        CreateLogLine("Canceled by user");
                        break;
                }
            }
            catch (Exception e)
            {
                CreateLogLine("Error in OnNotificationShown: " + e.Message);
            }
        }
        void CreateLogLine(string text)
        {
            System.Diagnostics.Debug.WriteLine(text);
        }
    }

}
