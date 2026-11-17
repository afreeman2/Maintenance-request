using MahApps.Metro.Controls;
using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Maintenance_request.Notifications;
using Maintenance_request.Requests;
using Maintenance_request.Support;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Maintenance_request.user_requst_windows;

public partial class Config_Request_Window : MetroWindow
{
    #region مصادر البيانات وحالة التحميل والحفظ والإغلاق
    private readonly Func<Cont_maintContext> createContext;
    private readonly RequestComposerStore store;
    private readonly Action<string, MessageBoxImage> showMessage;
    private readonly CancellationTokenSource lifetime = new();
    private bool loaded, loading, saving, closed;
    public long req_id = -1;
    public T_Notif_Action_User? CURR_REQ { get; set; }
    public Config_Request_Window() : this(null, null) { }
    public Config_Request_Window(Func<Cont_maintContext>? factory, Action<string, MessageBoxImage>? message)
    {
        createContext = factory ?? ClientContext.Create; store = new(createContext);
        showMessage = message ?? ((text, icon) => UiMessages.Show(this, "طلبات الصيانة", text, MessageBoxButton.OK, icon));
        InitializeComponent(); save_Request.IsEnabled = false;
        Closed += (_, _) => { closed = true; lifetime.Cancel(); };
    }
    private async void Request_Window_Loaded(object sender, RoutedEventArgs e) => await LoadDataAsync();
    public async Task LoadDataAsync()
    {
        if (loaded || loading) return;
        loading = true; LoadingIndicator.DeferedVisibility = true;
        try
        {
            var services = await store.ServicesAsync(lifetime.Token); if (closed) return;
            _Combo_Types.ItemsSource = services;
            if (CURR_REQ is not null) { TxtName.Text = CURR_REQ.Description; _Combo_Types.SelectedValue = CURR_REQ.Serv_num; }
            loaded = true;  TxtName.Focus();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closed) {  showMessage(ex.GetBaseException().Message, MessageBoxImage.Error); } }
        finally { loading = false; if (!closed) { LoadingIndicator.DeferedVisibility = false; save_Request.IsEnabled = loaded; } }
    }
    #endregion

    #region التحقق والحفظ غير المتزامن ومنع النقر المزدوج
    private async void save_Request_Click(object sender, RoutedEventArgs e) => await SaveRequestAsync();
    public async Task<bool> SaveRequestAsync()
    {
        if (!loaded || loading || saving || closed) return false;
        FieldFeedback.Clear(TxtName); FieldFeedback.Clear(_Combo_Types);
        if (string.IsNullOrWhiteSpace(TxtName.Text)) return FieldFeedback.Reject(TxtName, "أدخل تفاصيل طلب الصيانة.");
        if (_Combo_Types.SelectedItem is not T_Service service) return FieldFeedback.Reject(_Combo_Types, "اختر نوع الخدمة.");
        if (req_id <= 0) return FieldFeedback.Reject(TxtName, "اختر طلبًا من قائمة البحث أولًا.");
        saving = true; FormPanel.IsEnabled = false; save_Request.IsEnabled = false; LoadingIndicator.DeferedVisibility = true;
        try
        {
            await new RequestClientStore(createContext).UpdateAsync(req_id, App.curr_user.User_ID, TxtName.Text, service.Serv_num, service.Serv_type, lifetime.Token);
            if (closed) return true;

             showMessage("تم حفظ تعديل الطلب.", MessageBoxImage.Information); return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { if (!closed) showMessage(ex.GetBaseException().Message, MessageBoxImage.Error); return false; }
        finally { saving = false; if (!closed) { FormPanel.IsEnabled = true; save_Request.IsEnabled = loaded; LoadingIndicator.DeferedVisibility = false; } }
    }
    private void _Combo_Types_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { TxtName.Focus(); e.Handled = true; }
    }
    private void btnCancel_Click(object sender, RoutedEventArgs e) => Close();
    #endregion

    #region البحث الخاص بالمستخدم وتنظيفه ومنع وصول نتائج متأخرة بعد الإغلاق
    private int searchGeneration;
    private void Search_Request_Click(object sender, RoutedEventArgs e) { ResetSearch(); adv_searsh.IsOpen = true; Date_Begin.Focus(); }
    private async void Search_data_Click(object sender, RoutedEventArgs e) => await SearchAsync();
    public async Task SearchAsync()
    {
        FieldFeedback.Clear(Date_Begin); FieldFeedback.Clear(Date_End);
        if (Date_Begin.SelectedDate is not DateTime from) { FieldFeedback.Reject(Date_Begin, "اختر بداية الفترة."); return; }
        if (Date_End.SelectedDate is not DateTime to) { FieldFeedback.Reject(Date_End, "اختر نهاية الفترة."); return; }
        if (from > to) { FieldFeedback.Reject(Date_End, "نهاية الفترة يجب ألا تسبق بدايتها."); return; }
        int generation = ++searchGeneration; Search_data.IsEnabled = false;
        try
        {
            var rows = await store.SearchAsync(App.curr_user.User_ID, DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), lifetime.Token);
            if (!closed && adv_searsh.IsOpen && generation == searchGeneration) Search_Grid.ItemsSource = rows;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closed && generation == searchGeneration) showMessage(ex.GetBaseException().Message, MessageBoxImage.Error); }
        finally { if (!closed && generation == searchGeneration) Search_data.IsEnabled = true; }
    }
    private void ResetSearch()
    {
        searchGeneration++; Date_Begin.SelectedDate = null; Date_End.SelectedDate = null;
        Date_Begin.Text = ""; Date_End.Text = ""; Search_Grid.ItemsSource = null; Search_Grid.SelectedItem = null;
        FieldFeedback.Clear(Date_Begin); FieldFeedback.Clear(Date_End); Search_data.IsEnabled = true;
    }
    private void Search_Closed(object? sender, RoutedEventArgs e) => ResetSearch();
    private void cancel_search_Click(object sender, RoutedEventArgs e) { adv_searsh.IsOpen = false; ResetSearch(); }
    private void Search_Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Search_Grid.SelectedItem is not T_Notif_Action_User selected) return;
        CURR_REQ = selected; req_id = selected.ID; TxtName.Text = selected.Description; _Combo_Types.SelectedValue = selected.Serv_num;
        adv_searsh.IsOpen = false; ResetSearch(); TxtName.Focus();
    }
    #endregion

    #region إلغاء الطلب مع إعادة فحص المرحلة والملكية داخل المعاملة
    private async void Delete_Request_Click(object sender, RoutedEventArgs e)
    {
        if (saving || req_id <= 0) return;
        if (!UiMessages.Confirm(this, "هل تريد إلغاء طلب الصيانة المحدد؟")) return;
        saving = true; Delete_Request.IsEnabled = false; save_Request.IsEnabled = false;
        try
        {
            await new RequestClientStore(createContext).CancelAsync(req_id, App.curr_user.User_ID, lifetime.Token);
            App.isdel = true; if (!closed) { showMessage("تم إلغاء طلب الصيانة.", MessageBoxImage.Information); Close(); }
        }
        catch (Exception ex) { if (!closed) showMessage(ex.GetBaseException().Message, MessageBoxImage.Error); }
        finally { saving = false; if (!closed) { Delete_Request.IsEnabled = true; save_Request.IsEnabled = loaded; } }
    }
    #endregion

}
