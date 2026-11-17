using DevExpress.Xpf.Editors;
using Maintenance_request.Registration;
using Hardcodet.Wpf.TaskbarNotification;
using MahApps.Metro.Controls;
using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Maintenance_request.Notifications;
using Maintenance_request.Support;
using Maintenance_request.user_requst_windows;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Maintenance_request;

public partial class MainWindow : MetroWindow
{
    #region حالة النافذة ومصادر الفحص ومؤقت الساعة
    public int count;
    public ObservableCollection<T_Notif_Action_User> list_request = [];
    private readonly DispatcherTimer clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private CancellationTokenSource? _cts;
    private bool _allowClose, initialized, preferencesSaving;
    private List<T_Notif_Action_User> dashboardRequests = [];
    public static bool LaunchedFromNotification { get; set; }
    public static string ApplicationID => "منظومة طلبات الصيانة";
    public static string ApplicationName => "منظومة طلبات الصيانة";
    public MainWindow() : this(null) { }
    // تبقى دورة استقبال الردود قابلة للفحص بسياق معزول وصوت وإيصالات بديلة.
    public MainWindow(Func<Cont_maintContext>? replyContextFactory, Action? playNotificationSound = null, string? notificationStorageDirectory = null)
    {
        requestReplyContextFactory = replyContextFactory;
        requestNotificationSound = new Maintenance.Shared.NotificationSound(playNotificationSound ?? (() => ClientSoundPlayer.Play(App.curr_user?.Sound_CHoise ?? 2)));
        requestNotificationStorageDirectory = notificationStorageDirectory;
        InitializeComponent();
        InitializeRequestCenter();
        StatsYear.EditValue = DateTime.Today.Year;
        clockTimer.Tick += timer_Tick;
        AddShortcut(Key.F5, Request_mnue_Click); AddShortcut(Key.F6, nofig_Click);
        AddShortcut(Key.F8, opt_Click); AddShortcut(Key.F9, ch_password_mnue_Click);
        ((FrameworkElement)Content).SizeChanged += (_, _) => ResizeRequestsPanel();
        LocationChanged += (_, _) => { if (Notifi_Show.IsOpen) ResizeRequestsPanel(); };
    }
    private void AddShortcut(Key key, RoutedEventHandler execute)
    {
        var command = new RoutedCommand(); command.InputGestures.Add(new KeyGesture(key));
        CommandBindings.Add(new CommandBinding(command, (_, e) => execute(this, e)));
    }
    #endregion

    #region تحميل الرئيسية والتحديث اللحظي دون إيقاف الواجهة عند تعذر الاتصال
    private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (initialized) return; initialized = true;
        // نفس ثقافة التطبيق الأصلي، لتبقى التواريخ والأدوات عربية على جميع الأجهزة.
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new("ar-LY");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new("ar-LY");
        lus.Text = App.curr_user?.User_Name ?? ""; WelcomeText.Text = "مرحبًا، " + lus.Text;
        vesion.Text = "الإصدار " + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");
        timer_Tick(this, EventArgs.Empty); clockTimer.Start();
        try
        {
            if (App.curr_user is not null)
            {
                var rows = await GetUserNotificationsAsync(App.curr_user.User_ID);
                list_request.Clear(); foreach (var row in rows) list_request.Add(row);
                UpdateUIElements();
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); RefreshStatus.Text = "تعذر التحديث؛ ستتم إعادة المحاولة تلقائيًا."; }
        Start();
        if (App.LaunchedFromNotification) { App.LaunchedFromNotification = false; ShowNotificationFlyout(); }
    }
    private async Task<ObservableCollection<T_Notif_Action_User>> GetUserNotificationsAsync(long userId)
    {
        clientNotificationState.Configure(userId, requestNotificationStorageDirectory);
        var rows = await new RequestClientStore(CreateReplyContext).LoadAsync(userId);
        SetDashboardRequests(rows);
        return new(rows.Where(x => !clientNotificationState.IsDismissed(x)));
    }
    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource(); var token = _cts.Token;
        _ = Task.Run(() => ExecuteAsync(token), token);
    }
    public void Stop()
    {
        var source = _cts; _cts = null; source?.Cancel(); source?.Dispose();
    }
    private async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await Dispatcher.InvokeAsync(() => ProcessRequestRepliesAsync(token)).Task.Unwrap(); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                if (!Dispatcher.HasShutdownStarted)
                    await Dispatcher.InvokeAsync(() => RefreshStatus.Text = "تعذر التحديث؛ ستتم إعادة المحاولة تلقائيًا.");
            }
            try { await Task.Delay(1000, token); } catch (OperationCanceledException) { break; }
        }
    }
    private void timer_Tick(object? sender, EventArgs e)
    {
        ltim.Text = DateTime.Now.ToString("HH:mm:ss"); LDATE.Text = DateTime.Today.ToString("dd/MM/yyyy");
    }
    // الإحصاءات من جميع طلبات الحساب، بما فيها إشعار إنجاز أخفاه العميل، ولا تُحسب الطلبات الملغاة.
    private void SetDashboardRequests(IEnumerable<T_Notif_Action_User> rows)
    {
        dashboardRequests = rows.ToList(); UpdateDashboard();
        RefreshStatus.Text = "آخر تحديث " + DateTime.Now.ToString("HH:mm:ss");
    }
    private void StatsYear_Changed(object sender, EditValueChangedEventArgs e) { if (TotalCountText is not null) UpdateDashboard(); }
    private void UpdateDashboard()
    {
        int? year = int.TryParse(StatsYear.EditValue?.ToString(), out int selectedYear) ? selectedYear : null;
        var rows = dashboardRequests.Where(x => x.OP_ID != 4 && (!year.HasValue || x.ndate.Year == year)).ToArray();
        TotalCountText.Text = rows.Length.ToString(); NewCountText.Text = rows.Count(x => x.OP_ID is 1 or 7).ToString();
        InProgressCountText.Text = rows.Count(x => x.OP_ID is 2 or 8).ToString(); CompletedCountText.Text = rows.Count(x => x.OP_ID is 3 or 6).ToString();
    }
    public void UpdateUIElements()
    {
        count = list_request.Count;
        UpdateRequestCenter();
    }
    #endregion

    #region فتح الطلبات ومركز المتابعة واختصاراتها
    private async void Request_mnue_Click(object sender, RoutedEventArgs e)
    {
        new Request_Window { Owner = this }.ShowDialog();
        try { await ProcessRequestRepliesAsync(CancellationToken.None); }
        catch (Exception ex) { UiMessages.Show(this, "طلبات الصيانة", ex.GetBaseException().Message, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void nofig_Click(object sender, RoutedEventArgs e) => ShowNotificationFlyout();
    public void ShowNotificationFlyout()
    {
        RestoreWindow();
        UpdateLayout();
        ResizeRequestsPanel();
        Notifi_Show.IsOpen = true;
    }

    private void ResizeRequestsPanel()
    {
        // نترك هامشًا حول اللوحة، ونحسب المساحة المتاحة أسفل الترويسة بوحدات WPF.
        if (Content is not FrameworkElement frame || frame.ActualWidth <= 0 || frame.ActualHeight <= 0)
            return;

        double availableHeight = frame.ActualHeight - 140;
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero && PresentationSource.FromVisual(RequestsAnchor) is not null)
        {
            // قد تتجاوز النافذة حافة الشاشة عند التحجيم، لذا نحسب المساحة المرئية أيضًا بمقياس الشاشة الحالية.
            var screen = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            var anchorBottom = RequestsAnchor.PointToScreen(new System.Windows.Point(0, RequestsAnchor.ActualHeight));
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(RequestsAnchor);
            availableHeight = Math.Min(availableHeight, (screen.Bottom - anchorBottom.Y) / dpi.DpiScaleY - 32);
        }

        RequestsPanel.Width = Math.Clamp(frame.ActualWidth - 72, 0, 760);
        RequestsPanel.Height = Math.Clamp(availableHeight, 0, 590);
    }

    private void Notifi_Hid_Click(object sender, RoutedEventArgs e) => Notifi_Show.IsOpen = false;
    private async void Poke_click(object sender, RoutedEventArgs e)
    {
        try { await ProcessRequestRepliesAsync(CancellationToken.None); }
        catch (Exception ex) { UiMessages.Show(this, "طلبات الصيانة", ex.GetBaseException().Message, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    public void HandleNotificationClick() => Dispatcher.Invoke(ShowNotificationFlyout);

    private void OpenNotification(long id)
    {
        ShowNotificationFlyout();
        SelectRequestNotice(id);
    }
    public static void SendActivatorMessage(string arguments)
    {
        if (Application.Current?.MainWindow is MainWindow window) window.HandleNotificationClick();
    }
    #endregion

    #region الحساب وحفظ خيار الصوت دون إنشاء حساب جديد أو تغيير بيانات المؤسسة
    private async void opt_Click(object sender, RoutedEventArgs e) => await LoadAccountAsync();
    public async Task LoadAccountAsync()
    {
        AccountFlyout.IsOpen = true; SavePreferencesButton.IsEnabled = false; AccountDetails.Text = "جاري تحميل بيانات الحساب...";
        try
        {
            var options = await new RegistrationStore(CreateReplyContext).LoadAsync();
            AccountManagement.ItemsSource = options.Managements; AccountBuilding.ItemsSource = options.Buildings;
            var user = App.curr_user; AccountName.Text = user.User_Name;
            AccountManagement.SelectedValue = user.Mangment_Request_ID; AccountBuilding.SelectedValue = user.Build_ID;
            SoundEnabled.IsChecked = user.ISsound != false; SoundChoice.SelectedValue = user.Sound_CHoise is >= 1 and <= 3 ? user.Sound_CHoise.Value : (short)2;
            AccountDetails.Text = "قم بتعديل بيانات الحساب الخاص بك.";
        }
        catch (Exception ex) { AccountDetails.Text = "تعذر تحميل البيانات."; UiMessages.Show(this, "حسابي", ex.GetBaseException().Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { SavePreferencesButton.IsEnabled = true; }
    }
    private void CloseAccount_Click(object sender, RoutedEventArgs e) { if (!preferencesSaving) AccountFlyout.IsOpen = false; }
    private void PreviewSound_Click(object sender, RoutedEventArgs e)
    {
        try { if (short.TryParse(SoundChoice.SelectedValue?.ToString(), out var choice)) ClientSoundPlayer.Play(choice); }
        catch (Exception ex) { UiMessages.Show(this, "معاينة التنبيه", ex.GetBaseException().Message, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private async void SavePreferences_Click(object sender, RoutedEventArgs e)
    {
        if (preferencesSaving) return;
        if (string.IsNullOrWhiteSpace(AccountName.Text)) { FieldFeedback.Reject(AccountName, "أدخل اسم المستخدم."); return; }
        if (AccountManagement.SelectedItem is not T_Mangment_Request management) { FieldFeedback.Reject(AccountManagement, "اختر الإدارة."); return; }
        if (AccountBuilding.SelectedItem is not T_Build building) { FieldFeedback.Reject(AccountBuilding, "اختر المبنى."); return; }
        if (!short.TryParse(SoundChoice.SelectedValue?.ToString(), out short choice)) { FieldFeedback.Reject(SoundChoice, "اختر نغمة التنبيه."); return; }
        preferencesSaving = true; SavePreferencesButton.IsEnabled = false;
        try
        {
            long owner = App.curr_user.User_ID;
            var user = await new AccountStore(CreateReplyContext).SaveProfileAsync(owner, AccountName.Text, management.Mangment_Request_ID, building.Build_ID, SoundEnabled.IsChecked == true, choice);
            if (App.curr_user.User_ID == owner) App.curr_user = user;
            lus.Text = user.User_Name; WelcomeText.Text = "مرحبًا، " + user.User_Name;
            AccountFlyout.IsOpen = false; UiMessages.Show(this, "حسابي", "تم حفظ بيانات الحساب.", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (ArgumentException ex) { FieldFeedback.Reject(AccountName, ex.Message); }
        catch (Exception ex) { UiMessages.Show(this, "حسابي", ex.GetBaseException().Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { preferencesSaving = false; SavePreferencesButton.IsEnabled = true; }
    }
    private void ch_password_mnue_Click(object sender, RoutedEventArgs e) => new Settings.Password_Chanche { Owner = this }.ShowDialog();
    #endregion

    #region التصغير والاستعادة والإغلاق وإيقاف الموارد
    private void Title_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    private void btnman_Click(object sender, RoutedEventArgs e)
    {
        MyNotifyIcon.Visibility = Visibility.Visible; Hide(); ShowInTaskbar = false;
        MyNotifyIcon.ShowBalloonTip("طلبات الصيانة", "يبقى التطبيق قيد التشغيل لاستقبال ردود طلباتك.", BalloonIcon.Info);
    }
    private void MetroWindow_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized) { Hide(); ShowInTaskbar = false; }
    }
    // استعادة سلوك العرض السابق بعد تراجع المستخدم عن التكبير التلقائي لهذا التطبيق.
    private void RestoreWindow() { ShowInTaskbar = true; Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
    private void MyNotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => RestoreWindow();
    private void Open_Click(object sender, RoutedEventArgs e) => RestoreWindow();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void btnCancel_Click(object sender, RoutedEventArgs e) => Close();
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose && IsVisible && requestReplyContextFactory is null
            && UiMessages.Show(this, "طلبات الصيانة", "هل تريد إغلاق التطبيق وإيقاف استقبال التنبيهات؟", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            e.Cancel = true;
        base.OnClosing(e);
    }
    private void MetroWindow_Closed(object sender, EventArgs e)
    {
        Stop(); StopNoticeBadge(); clockTimer.Stop(); clockTimer.Tick -= timer_Tick; MyNotifyIcon.Dispose();
        // التطبيق يعمل أصلًا بوضع الإغلاق الصريح؛ إغلاق الرئيسية يجب أن ينهي العملية أيضًا.
        if (initialized && requestReplyContextFactory is null) Application.Current.Shutdown();
    }
    public void ExitApplication() { _allowClose = true; Close(); Application.Current.Shutdown(); }
    #endregion
}
