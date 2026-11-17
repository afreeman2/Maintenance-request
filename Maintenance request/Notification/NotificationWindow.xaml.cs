using DevExpress.Mvvm;
using MahApps.Metro.Controls;
using Maintenance_request.Notifications;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;


namespace Maintenance_request.Notification
{

    public partial class NotificationWindow : MetroWindow
    {

        private readonly TaskCompletionSource<NotificationResult> source;


        public NotificationModel Model { get; }



        public NotificationWindow(
            NotificationModel model,
            TaskCompletionSource<NotificationResult> source)
        {

            InitializeComponent();


            this.Model = model;

            this.source = source;


            DataContext = model;


            Loaded += Window_Loaded;

        }




        private void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {


            BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(
                    0,
                    1,
                    TimeSpan.FromMilliseconds(250)));



            var timer =
            new System.Windows.Threading.DispatcherTimer();


            timer.Interval =
                Model.Duration;



            timer.Tick += (s, a) =>
            {

                timer.Stop();


                source.TrySetResult(
                    NotificationResult.TimedOut);


                Close();

            };


            timer.Start();

        }





        private void Border_Click(
            object sender,
            MouseButtonEventArgs e)
        {

            source.TrySetResult(
                NotificationResult.Activated);


            Close();

        }





        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {

            source.TrySetResult(
                NotificationResult.UserCanceled);


            Close();

        }


    }

}