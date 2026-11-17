using DevExpress.Mvvm;
using Maintenance_request.Notifications;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;


namespace Maintenance_request.Notification
{

    public static class NotificationManager
    {


        private static readonly List<Window> Active =
        new();



        public static Task<NotificationResult> ShowAsync(
            NotificationModel model)
        {


            var tcs =
            new TaskCompletionSource<NotificationResult>();



            Application.Current.Dispatcher.Invoke(() =>
            {


                var window =
                new NotificationWindow(model, tcs);



                Position(window);



                Active.Add(window);



                window.Closed += (s, e) =>
                {
                    Active.Remove(window);
                };



                window.Show();


            });



            return tcs.Task;


        }






        private static void Position(
            Window window)
        {


            double bottom = 30;



            foreach (var item in Active)
            {
                bottom += item.Height + 10;
            }



            window.Left =
            SystemParameters.WorkArea.Right
            - window.Width
            - 20;



            window.Top =
            SystemParameters.WorkArea.Bottom
            - bottom;


        }


    }


}