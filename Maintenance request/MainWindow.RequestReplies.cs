using DevExpress.Mvvm;
using Maintenance_request.converts;
using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Maintenance_request.Notifications;
using Maintenance.Shared;
using Microsoft.EntityFrameworkCore;

namespace Maintenance_request;

public partial class MainWindow
{
    #region استقبال الردود الحالية وإزالة السجلات المحذوفة دون فقد تغييرات أثناء إغلاق التطبيق
    private readonly Func<Cont_maintContext>? requestReplyContextFactory;
    private readonly SemaphoreSlim replyRefreshGate = new(1, 1);
    private readonly NotificationSound requestNotificationSound;
    private readonly string? requestNotificationStorageDirectory;
    private readonly ClientNotificationState clientNotificationState = new();
    private readonly HashSet<string> pendingRequestToasts = [];
    private Cont_maintContext CreateReplyContext()
    {
        if (requestReplyContextFactory is not null) return requestReplyContextFactory();
        DbContextHelper.InitOptions(App.DatabaseConnectionString);
        return new Cont_maintContext(DbContextHelper.Options);
    }
    // لقطة خاصة بموظف الطلب؛ عمومية مركز المنظومة لا تعني كشف طلبات الموظفين لبعضهم في التطبيق.
    private async Task ProcessRequestRepliesAsync(CancellationToken token)
    {
        if (!await replyRefreshGate.WaitAsync(0, token)) return;
        try { await LoadAndReceiveRepliesAsync(token); }
        finally { replyRefreshGate.Release(); }
    }
    private async Task LoadAndReceiveRepliesAsync(CancellationToken token)
    {
        long owner = App.curr_user.User_ID;
        clientNotificationState.Configure(owner, requestNotificationStorageDirectory);
        var store = new RequestClientStore(CreateReplyContext);
        var (snapshot, pendingCancellations) = await store.LoadRepliesAsync(owner, token);
        if (token.IsCancellationRequested || App.curr_user.User_ID != owner) return;
        // نعرض الإلغاء السابق قبل الرد الأحدث، دون تبديل حالة البطاقة إلى الإلغاء.
        foreach (var (actionId, request) in pendingCancellations)
        {
            ShowClientToast(request, owner);
            if (clientNotificationState.WasShown(request))
                await store.ReceivePreviousCancellationAsync(actionId, owner, token);
        }
        foreach (var request in snapshot.Where(x => ClientNotificationState.Operation(x) is 1 or 2 or 3 or 4 or 7 or 8))
        {
            if (clientNotificationState.IsDismissed(request)) continue;
            ShowClientToast(request, owner);
            if (ClientNotificationState.Operation(request) == 4 && !pendingRequestToasts.Contains(ClientNotificationState.Key(request))
                && clientNotificationState.WasShown(request))
                clientNotificationState.Dismiss(request);
            // إن تعذر تأكيد سابق نعيد حفظ الاستلام فقط، دون إعادة عرض التنبيه أو الصوت.
            if (clientNotificationState.WasShown(request) && request.state == 0 && request.OP_ID is 2 or 3 or 7 or 8)
                await store.ReceiveAsync(request.ID, owner, request.OP_ID, token);
        }
        // إعادة القراءة تعرض الحالة المستقرة 1 أو2 أو6، ولا تعيد تنبيه الجاري بعد العملية8.
        var allCurrent = await store.LoadAsync(owner, token);
        var current = allCurrent.Where(x => !clientNotificationState.IsDismissed(x)).ToList();
        if (token.IsCancellationRequested || App.curr_user.User_ID != owner) return;
        SetDashboardRequests(allCurrent);
        bool changed = list_request.Count != current.Count || list_request.Zip(current).Any(pair =>
            pair.First.ID != pair.Second.ID || pair.First.OP_ID != pair.Second.OP_ID || pair.First.state != pair.Second.state
            || pair.First.Description != pair.Second.Description || pair.First.Serv_num != pair.Second.Serv_num
            || pair.First.Serv_type != pair.Second.Serv_type || pair.First.MaintenanceCosts_No != pair.Second.MaintenanceCosts_No
            || pair.First.NotificationRevision != pair.Second.NotificationRevision
            || pair.First.NotificationOperation != pair.Second.NotificationOperation
            || pair.First.Build != pair.Second.Build || pair.First.User_Name != pair.Second.User_Name
            || pair.First.ndate != pair.Second.ndate);
        if (!changed) return;
        list_request.Clear(); foreach (var row in current) list_request.Add(row);
        UpdateUIElements();
    }

    // يظل لون البالون ونصه مرتبطين بالعملية التي حدثت فعليًا، مع منع تكرار العرض والصوت.
    private void ShowClientToast(T_Notif_Action_User request, long owner)
    {
        string key = ClientNotificationState.Key(request);
        if (clientNotificationState.WasShown(request) || pendingRequestToasts.Contains(key)
            || DataContext is not NotificationMainViewModel vm || vm.DefaultNotificationService is null) return;
        var toast = vm.DefaultNotificationService.CreateCustomNotification(new RequestToast
        {
            Operation = ClientNotificationState.Operation(request), RequestId = request.ID
        });
        var showing = toast.ShowAsync();
        if (showing.IsFaulted || showing.IsCanceled) return;
        pendingRequestToasts.Add(key); clientNotificationState.MarkShown(request);
        requestNotificationSound.TryPlay(App.curr_user.ISsound != false);
        vm.Observe(showing);
        _ = FinishClientToastAsync(request, owner, showing);
    }
    private async Task FinishClientToastAsync(T_Notif_Action_User request, long owner, Task<NotificationResult> showing)
    {
        try
        {
            await showing;
            if (App.curr_user.User_ID != owner || ClientNotificationState.Operation(request) != 4) return;
            clientNotificationState.Dismiss(request);
            foreach (var item in list_request.Where(x => ClientNotificationState.Key(x) == ClientNotificationState.Key(request)).ToArray()) list_request.Remove(item);
            UpdateUIElements();
        }
        catch (Exception) { /* فشل عرض تنبيه لا يحذف الطلب أو مستنداته. */ }
        finally { pendingRequestToasts.Remove(ClientNotificationState.Key(request)); }
    }
    // يختفي إشعار الإنجاز لهذا العميل فقط؛ تبقى البيانات لعودة الطلب إذا ألغي تقرير الإنهاء.
    public async Task DismissCompletedNotificationAsync(long requestId)
    {
        long owner = App.curr_user.User_ID;
        var store = new RequestClientStore(CreateReplyContext);
        var row = await store.CompletedNoticeAsync(requestId, owner);
        if (row.OP_ID == 3 && row.state == 0 && !await store.ReceiveAsync(requestId, owner, 3))
            row = await store.CompletedNoticeAsync(requestId, owner);
        clientNotificationState.Configure(owner, requestNotificationStorageDirectory);
        clientNotificationState.MarkShown(row); clientNotificationState.Dismiss(row);
        foreach (var item in list_request.Where(x => x.ID == requestId).ToArray()) list_request.Remove(item);
        UpdateUIElements();
    }
    private async void DismissCompletedNotification_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement { Tag: T_Notif_Action_User row }) return;
        if (Maintenance_request.Support.UiMessages.Show(this, "طلبات الصيانة", "هل تريد حذف إشعار الطلب المنفذ من قائمتك؟", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxResult.No, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes) return;
        try { await DismissCompletedNotificationAsync(row.ID); }
        catch (Exception ex) { Maintenance_request.Support.UiMessages.Show(this, "طلبات الصيانة", ex.GetBaseException().Message, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxResult.OK, System.Windows.MessageBoxImage.Error); }
    }
    #endregion
}
