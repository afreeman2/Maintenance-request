using DevExpress.Mvvm;
using DevExpress.Mvvm.UI;
using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;

namespace Maintenance_request.converts
{
    public class NotificationServicex
    {
        //protected virtual INotificationService iNotificationService => null;
        //public int PredefinedNotificationId { get; set; }

        //private readonly MainWindow _mainUI;
        //private CancellationTokenSource _cts;
        //private long _lastChangeVersion = 0;
        //T_Notif_Action_User item;
        //public NotificationServicex(MainWindow mainUI)
        //{
        //    _mainUI = mainUI;
        //    PredefinedNotificationId = 100;
        //}

        //public void Start()
        //{
        //    _cts = new CancellationTokenSource();
        //    var token = _cts.Token;

        //    Task.Run(async () => await ExecuteAsync(token), token);
        //}

        //public void Stop()
        //{
        //    _cts?.Cancel();
        //}

        //private async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    _lastChangeVersion = await GetCurrentTableVersionAsync(stoppingToken);

        //    while (!stoppingToken.IsCancellationRequested)
        //    {
        //        try
        //        {
        //            await ProcessDatabaseChangesAsync(stoppingToken);
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine($"Error in Notification Service: {ex.Message}");
        //        }

        //        try
        //        {
        //            await Task.Delay(1000, stoppingToken);
        //        }
        //        catch (TaskCanceledException)
        //        {
        //            break;
        //        }
        //    }
        //}

        //private async Task<long> GetCurrentTableVersionAsync(CancellationToken cancellationToken)
        //{
        //    DbContextHelper.InitOptions(ConfigurationManager.AppSettings["connstring"] + App.DB_US + ";Password=" + App.saPassword + "; TrustServerCertificate=True;");
        //    using (var db = new Cont_maintContext(DbContextHelper.Options))
        //    {
        //        var version = await db.Database
        //            .SqlQueryRaw<long>("SELECT ISNULL(CHANGE_TRACKING_CURRENT_VERSION(), 0) AS Value")
        //            .FirstOrDefaultAsync(cancellationToken);

        //        return version;
        //    }
        //}

        //private async Task ProcessDatabaseChangesAsync(CancellationToken cancellationToken)
        //{
        //    DbContextHelper.InitOptions(ConfigurationManager.AppSettings["connstring"] + App.DB_US + ";Password=" + App.saPassword + "; TrustServerCertificate=True;");

        //    List<ChangeRecord> changes;

        //    using (var db = new Cont_maintContext(DbContextHelper.Options))
        //    {
        //        var query = $@"
        //            SELECT 
        //                CT.SYS_CHANGE_OPERATION,
        //                ISNULL(T.DOC_ID, 0) AS DOC_ID,
        //                ISNULL(T.DOC_Number, '') AS DOC_Number,
        //                ISNULL(T.OP_ID, 0) AS OP_ID
        //            FROM CHANGETABLE(CHANGES T_Notif_Action, {_lastChangeVersion}) AS CT
        //            LEFT JOIN T_Notif_Action T ON CT.ID = T.ID";

        //        changes = await db.Database
        //            .SqlQueryRaw<ChangeRecord>(query)
        //            .ToListAsync(cancellationToken);
        //    }

        //    if (changes == null || !changes.Any()) return;

        //    _lastChangeVersion = await GetCurrentTableVersionAsync(cancellationToken);

        //    await Application.Current.Dispatcher.InvokeAsync(async () =>
        //    {
        //        foreach (var change in changes)
        //        {
        //            await HandleChangeAsync(change);
        //        }
        //    });
        //}

        //private async Task HandleChangeAsync(ChangeRecord change)
        //{
        //    DbContextHelper.InitOptions(ConfigurationManager.AppSettings["connstring"] + App.DB_US + ";Password=" + App.saPassword + "; TrustServerCertificate=True;");
        //    using (var db = new Cont_maintContext(DbContextHelper.Options))
        //    {
        //        var notificationItem = await db.T_Notif_Action_Users
        //            .FirstOrDefaultAsync(x => x.ID == change.DOC_ID && x.User_ID == App.curr_user.User_ID);

        //        if (notificationItem == null) return;

        //        switch (change.SYS_CHANGE_OPERATION)
        //        {
        //            case "I":
        //                await HandleInsertAsync(db, notificationItem);
        //                break;

        //            case "U":
        //                await HandleUpdateAsync(db, notificationItem);
        //                break;

        //            case "D":
        //                HandleDelete(change);
        //                break;
        //        }
        //    }
        //}

        //#region معالجة العمليات وتحديث الواجهة

        ///// <summary>
        ///// دالة غير معطلة (Non-blocking) لإظهار الإشعار والانتظار في مسار مستقل عند النقر
        ///// </summary>
        //private void ShowToastAndListen(string title, string body, string id)
        //{
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        try
        //        {
        //            //var notification = _mainUI.notificationService.CreatePredefinedNotification(title, body, id);

        //            //notification.ShowAsync();
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine($"Error showing notification: {ex.Message}");
        //        }
        //    });
        //}

        //private async Task HandleInsertAsync(Cont_maintContext db, T_Notif_Action_User notificationItem)
        //{
        //    try
        //    {

        //        ShowToastAndListen("تطبيق طلب الصيانة", "تم ارسال طلب الصيانة", notificationItem.ID.ToString());

        //        notificationItem.state = 1;
        //        _mainUI.list_request.Add(notificationItem);
        //        _mainUI.nofig.Visibility = Visibility.Visible;

        //        await db.SaveChangesAsync();
        //    }
        //    catch (DbUpdateConcurrencyException ex)
        //    {
        //        Debug.WriteLine($"Concurrency issue on insert: {ex.Message}");
        //    }

        //    _mainUI.count = _mainUI.list_request?.Count ?? 0;
        //    UpdateUIElements();
        //}

        //private async Task HandleUpdateAsync(Cont_maintContext db, T_Notif_Action_User notificationItem)
        //{
        //    var targetItem = _mainUI.list_request.FirstOrDefault(j => j.ID == notificationItem.ID);

        //    if (targetItem == null) return;

        //    // تعديل بيانات فقط
        //    if (notificationItem.OP_ID == 1)
        //    {
        //        targetItem.Description = notificationItem.Description;
        //        targetItem.Serv_type = notificationItem.Serv_type;
        //        targetItem.Serv_num = notificationItem.Serv_num;
        //    }

        //    // جاري تنفيذ طلب الصيانة
        //    if (notificationItem.OP_ID == 2 && notificationItem.state == 0)
        //    {
        //        ShowToastAndListen("منظومة الخدمات والصيانة", "جاري الأن تنفيذ طلب الصيانة الخاص بك", notificationItem.ID.ToString());

        //        targetItem.OP_ID = 2;
        //        notificationItem.state = 1;
        //        await db.SaveChangesAsync();
        //    }

        //    // تم تنفيذ طلب الصيانة
        //    if (notificationItem.OP_ID == 3 && notificationItem.state == 0)
        //    {
        //        ShowToastAndListen("منظومة الخدمات والصيانة", "تم تنفيذ طلب الصيانة الخاص بك", notificationItem.ID.ToString());

        //        targetItem.OP_ID = 3;
        //        notificationItem.state = 1;
        //        await db.SaveChangesAsync();
        //    }

        //    // تم إلغاء تنفيذ طلب الصيانة
        //    if (notificationItem.OP_ID == 7 && notificationItem.state == 0)
        //    {
        //        ShowToastAndListen("منظومة الخدمات والصيانة", "تم إلغاء تنفيذ طلب الصيانة الخاص بك", notificationItem.ID.ToString());

        //        targetItem.OP_ID = 1;
        //        notificationItem.state = 1;
        //        notificationItem.OP_ID = 1;
        //        await db.SaveChangesAsync();
        //    }

        //    // حذف الطلب
        //    if (notificationItem.OP_ID == 4)
        //    {
        //        _mainUI.list_request.Remove(targetItem);
        //        _mainUI.count = _mainUI.list_request?.Count ?? 0;

        //        if (_mainUI.count == 0)
        //        {
        //            _mainUI.nofig.Visibility = Visibility.Hidden;
        //            _mainUI.Notifi_Show.IsOpen = false;
        //        }
        //    }

        //    // إلغاء إنجاز طلب الصيانة
        //    if ((notificationItem.OP_ID == 8 || notificationItem.OP_ID == 2) && notificationItem.state == 0)
        //    {
        //        ShowToastAndListen("منظومة الخدمات والصيانة", "تم إلغاء إنجاز طلب الصيانة الخاص بك", notificationItem.ID.ToString());

        //        targetItem.OP_ID = 2;
        //        notificationItem.state = 1;
        //        notificationItem.OP_ID = 2;
        //        await db.SaveChangesAsync();
        //    }

        //    _mainUI.count = _mainUI.list_request?.Count ?? 0;
        //    UpdateUIElements();
        //}

        //private void HandleDelete(ChangeRecord change)
        //{
        //    var itemToRemove = _mainUI.list_request.FirstOrDefault(x => x.ID == change.DOC_ID);
        //    if (itemToRemove != null)
        //    {
        //        _mainUI.list_request.Remove(itemToRemove);
        //    }

        //    _mainUI.count = _mainUI.list_request?.Count ?? 0;
        //    UpdateUIElements();
        //}

        //private void UpdateUIElements()
        //{
        //    if (_mainUI.count == 0)
        //    {
        //        _mainUI.nofig.Visibility = Visibility.Hidden;
        //        _mainUI.bg1.BadgeBackground = Brushes.White;
        //        _mainUI.bg1.BadgeForeground = Brushes.Black;
        //        _mainUI.bg1.Badge = null;
        //    }
        //    else
        //    {
        //        _mainUI.nofig.Visibility = Visibility.Visible;
        //        _mainUI.bg1.BadgeBackground = Brushes.White;
        //        _mainUI.bg1.BadgeForeground = Brushes.Black;
        //        _mainUI.bg1.Badge = _mainUI.list_request?.Count;
        //    }

        //    _mainUI.Nof_Grid.ItemsSource = null;
        //    _mainUI.Nof_Grid.ItemsSource = _mainUI.list_request;
        //    _mainUI.Nof_Grid.View?.Refresh();
        //}

        //#endregion
    }

   
}