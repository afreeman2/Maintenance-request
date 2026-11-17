using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Maintenance_request.converts
{


    [Guid("42F6D91C-B68F-462E-9D61-AA1B612C3277")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class Activatorx : ToastNotificationActivator
    {
        public override void OnActivate(string arguments, Dictionary<string, string> data)
        {
            try
            {

                Application.Current.Dispatcher.Invoke(() => {
                    MainWindow.SendActivatorMessage(arguments);
                });

                //Application.Current?.Dispatcher.Invoke(() =>
                //{
                //    var mainWindow = Application.Current.Windows
                //        .OfType<MainWindow>()
                //        .FirstOrDefault();

                //    if (mainWindow != null)
                //    {
                //        mainWindow.HandleNotificationClick();
                //    }
                //    else
                //    {
                //        App.LaunchedFromNotification = true;
                //    }
                //});
            }
            catch (Exception ex)
            {
                Maintenance_request.Support.UiMessages.Show(ex.Message);
            }


        }
    }
}
