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

public partial class Request_Window : MetroWindow
{
    #region مصادر البيانات وحالة التحميل والحفظ والإغلاق
    private readonly Func<Cont_maintContext> createContext;
    private readonly RequestComposerStore store;
    private readonly Action<string, MessageBoxImage> showMessage;
    private readonly CancellationTokenSource lifetime = new();
    private bool loaded, loading, saving, closed;
    public long req_id = -1;
    public T_Notif_Action_User? CURR_REQ { get; set; }
    public Request_Window() : this(null, null) { }
    public Request_Window(Func<Cont_maintContext>? factory, Action<string, MessageBoxImage>? message)
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
            loaded = true;TxtName.Focus();
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
        
        saving = true; FormPanel.IsEnabled = false; save_Request.IsEnabled = false; LoadingIndicator.DeferedVisibility = true;
        try
        {
            await store.SendAsync(App.curr_user.User_ID, TxtName.Text, service.Serv_num, lifetime.Token);
            if (closed) return true;
            TxtName.Clear(); _Combo_Types.SelectedIndex = -1; TxtName.Focus();
             showMessage("تم إرسال طلبك إلى القسم المختص.", MessageBoxImage.Information); return true;
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

}
