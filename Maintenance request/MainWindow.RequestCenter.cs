using Maintenance.Shared;
using Maintenance_request.data;
using Maintenance_request.Notifications;
using Maintenance_request.Support;
using Maintenance_request.user_requst_windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Visibility = System.Windows.Visibility;

namespace Maintenance_request;

public partial class MainWindow
{
    #region عرض بطاقات الإشعارات وتجميعها حسب مرحلة الطلب
    private sealed record NoticeFilterOption(string Title, string Icon, int? Stage);

    private sealed record RequestNoticeCard(T_Notif_Action_User Row, bool IsUnread)
    {
        // البطاقة تعرض الحالة الحالية بلونها؛ عمليتا الإلغاء 7 و8 تظهران في البالون فقط.
        private RequestNotificationAppearance Appearance => RequestNotificationAppearance.For(Stage);
        public string Title => Stage switch
        {
            RequestProtocol.New => "تم إرسال طلب الصيانة الخاص بك إلى القسم المختص",
            RequestProtocol.Executing => "جاري تنفيذ طلب الصيانة الخاص بك",
            _ => Appearance.Title
        };
        public string AccentColor => Appearance.AccentColor;
        public string TintColor => Appearance.TintColor;
        public int Stage => RequestProtocol.StableStage(Row.OP_ID);
        public string Group => Stage switch
        {
            RequestProtocol.New => "بانتظار التكليف",
            RequestProtocol.Executing => "جاري التنفيذ",
            RequestProtocol.Completed => "تم التنفيذ",
            RequestProtocol.Cancelled => "ملغاة",
            _ => "طلبات أخرى"
        };
        public string Context => string.Join(" • ", new[] { Row.Serv_type, Row.Build }.Where(value => !string.IsNullOrWhiteSpace(value)));
        public string DateLabel => Row.rendate == default ? Row.ndate.ToString("dd/MM/yyyy") : Row.rendate.ToString("dd/MM/yyyy HH:mm");
        public string WaitingLabel
        {
            get
            {
                if (Stage != RequestProtocol.New || Row.ndate == default) return string.Empty;
                int days = Math.Max(0, DateOnly.FromDateTime(DateTime.Today).DayNumber - Row.ndate.DayNumber);
                return days == 0 ? "بانتظار التكليف اليوم" : $"أيام الانتظار: {days}";
            }
        }
        public string Details => string.Join(" • ", new[] { Group, Context, Row.User_Name, DateLabel }.Where(value => !string.IsNullOrWhiteSpace(value)));
        public bool CanEdit => Row.OP_ID == RequestProtocol.New && Row.MaintenanceCosts_No is not > 0;
        public bool CanDismiss => Row.OP_ID is RequestProtocol.Completed or RequestProtocol.Closed;
    }

    private void InitializeRequestCenter()
    {
        NoticeFilter.ItemsSource = new NoticeFilterOption[]
        {
            new("جميع الحالات", "BellOutline", null),
            new("بانتظار التكليف", "ClipboardTextOutline", RequestProtocol.New),
            new("جاري التنفيذ", "ProgressWrench", RequestProtocol.Executing),
            new("تم التنفيذ", "CheckCircleOutline", RequestProtocol.Completed),
            new("ملغاة", "Cancel", RequestProtocol.Cancelled)
        };
        NoticeFilter.SelectedIndex = 0;
        UpdateRequestCenter();
    }

    private void UpdateRequestCenter()
    {
        // يحتفظ التحديث الدوري بالتحديد ما دام الطلب ظاهرًا ضمن المرشح الحالي.
        long? selectedId = (NoticeList.SelectedItem as RequestNoticeCard)?.Row.ID;
        string search = NoticeSearch.Text.Trim();
        int? stage = (NoticeFilter.SelectedItem as NoticeFilterOption)?.Stage;
        var cards = list_request.Select(row => new RequestNoticeCard(row, !clientNotificationState.IsRead(row))).ToArray();
        var filtered = cards.Where(card => (!stage.HasValue || card.Stage == stage) &&
            (search.Length == 0 || new[] { card.Row.Description, card.Context, card.Row.User_Name, card.Title }
                .Any(value => value?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true)))
            .OrderBy(card => card.Stage).ThenByDescending(card => card.Row.rendate).ThenByDescending(card => card.Row.ID).ToList();
        var view = new ListCollectionView(filtered);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RequestNoticeCard.Group)));
        NoticeList.ItemsSource = view;
        NoticeList.SelectedItem = filtered.FirstOrDefault(card => card.Row.ID == selectedId);

        NoticeTotal.Text = cards.Length.ToString();
        NoticeWaiting.Text = cards.Count(card => card.Stage == RequestProtocol.New).ToString();
        NoticeExecuting.Text = cards.Count(card => card.Stage == RequestProtocol.Executing).ToString();
        NoticeCompleted.Text = cards.Count(card => card.Stage == RequestProtocol.Completed).ToString();
        NoticeResults.Text = $"النتائج: {filtered.Count}";
        EmptyRequestsText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateNoticeBadge(cards.Count(card => card.IsUnread));
    }

    private void NoticeSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (NoticeList is not null) UpdateRequestCenter();
    }

    private void NoticeFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (NoticeList is not null) UpdateRequestCenter();
    }

    private void SelectRequestNotice(long id)
    {
        // النقر على التنبيه يعرض الطلب حتى لو ترك المستخدم مرشحًا سابقًا.
        NoticeSearch.Clear();
        NoticeFilter.SelectedIndex = 0;
        NoticeList.SelectedItem = NoticeList.Items.OfType<RequestNoticeCard>().FirstOrDefault(card => card.Row.ID == id);
        if (NoticeList.SelectedItem is { } selected) NoticeList.ScrollIntoView(selected);
    }

    private void NoticeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = NoticeList.SelectedItem as RequestNoticeCard;
        NoticeDetails.DataContext = selected;
        NoticeDetails.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
        EditNoticeRequest.Visibility = selected?.CanEdit == true ? Visibility.Visible : Visibility.Collapsed;
        DismissNotice.Visibility = selected?.CanDismiss == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void EditNoticeRequest_Click(object sender, RoutedEventArgs e) => await EditSelectedNoticeAsync();

    private async void NoticeList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(NoticeList, source) is ListBoxItem item
            && ReferenceEquals(item.Content, NoticeList.SelectedItem))
            await EditSelectedNoticeAsync();
    }

    private async void NoticeList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || NoticeList.SelectedItem is not RequestNoticeCard { CanEdit: true }) return;
        e.Handled = true;
        await EditSelectedNoticeAsync();
    }

    private async Task EditSelectedNoticeAsync()
    {
        if (NoticeList.SelectedItem is not RequestNoticeCard { CanEdit: true } selected) return;
        Notifi_Show.IsOpen = false;
        // تغطي النافذة الشفافة إطار الطلبات فقط كي يظهر المحرر فوقه حتى عندما تكون الرئيسية بحجم عادي.
        new Config_Request_Window
        {
            Owner = this,
            Width = ActualWidth,
            Height = ActualHeight,
            CURR_REQ = selected.Row,
            req_id = selected.Row.ID
        }.ShowDialog();
        try { await ProcessRequestRepliesAsync(CancellationToken.None); }
        catch (Exception ex) { UiMessages.Show(this, "طلبات الصيانة", ex.GetBaseException().Message, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    #endregion

    #region قراءة الإشعارات محليًا ووميض العداد دون تغيير حالة الطلب
    private readonly DispatcherTimer noticeBadgeTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };

    private void ReadAllNotices_Click(object sender, RoutedEventArgs e)
    {
        clientNotificationState.MarkRead(list_request);
        UpdateRequestCenter();
    }

    private void UpdateNoticeBadge(int unread)
    {
        bg1.Content = unread;
        bg1.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (unread == 0)
        {
            noticeBadgeTimer.Stop();
            bg1.Opacity = 1;
        }
        else if (!noticeBadgeTimer.IsEnabled)
        {
            noticeBadgeTimer.Tick -= NoticeBadge_Tick;
            noticeBadgeTimer.Tick += NoticeBadge_Tick;
            noticeBadgeTimer.Start();
        }
    }

    // تبقى الشارة نفسها متصلة بالجرس ويظل العدد ثابتًا أثناء الوميض.
    private void NoticeBadge_Tick(object? sender, EventArgs e) => bg1.Opacity = bg1.Opacity == 1 ? 0.35 : 1;

    private void StopNoticeBadge()
    {
        noticeBadgeTimer.Stop();
        noticeBadgeTimer.Tick -= NoticeBadge_Tick;
        bg1.Opacity = 1;
    }
    #endregion
}
