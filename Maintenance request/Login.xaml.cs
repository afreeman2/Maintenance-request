using MahApps.Metro.Controls;
using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Maintenance_request.Registration;
using Maintenance_request.Support;
using System.Management;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Maintenance_request;

public partial class Login : MetroWindow
{
    #region مصادر الحسابات وحالة التحميل والدخول
    private readonly AccountStore store;
    private readonly Func<string> deviceProvider;
    private readonly Action<T_Maintenance_request_User> signedIn;
    private bool loaded, busy, closed;
    private string computer = "";
    public Login() : this(null, null, null) { }
    public Login(Func<Cont_maintContext>? factory, Func<string>? device, Action<T_Maintenance_request_User>? completed)
    {
        store = new(factory); deviceProvider = device ?? GetCPU_ID;
        signedIn = completed ?? (user => { var main = new MainWindow(); System.Windows.Application.Current.MainWindow = main; main.Show(); Close(); });
        InitializeComponent(); Closed += (_, _) => closed = true;
    }
    public string GetCPU_ID()
    {
        using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
        using var results = searcher.Get();
        foreach (ManagementObject board in results) using (board) if (board["SerialNumber"]?.ToString() is { Length: > 0 } id) return id;
        return "";
    }
    private async void LogIn_LoadedAsync(object sender, RoutedEventArgs e) => await LoadDataAsync();
    public async Task LoadDataAsync()
    {
        if (loaded || busy) return; busy = true; BNEXT.IsEnabled = false; LoginStatus.Text = "جاري تحميل حسابات الجهاز...";
        try
        {
            computer = await Task.Run(deviceProvider);
            if (string.IsNullOrWhiteSpace(computer)) throw new InvalidOperationException("تعذرت قراءة معرّف الجهاز.");
            var users = await store.UsersAsync(computer); if (closed) return;
            cboUserFullName.ItemsSource = users; if (users.Count == 1) cboUserFullName.SelectedIndex = 0;
            loaded = true; LoginStatus.Text = users.Count == 0 ? "لا توجد حسابات على هذا الجهاز؛ يمكنك إنشاء حساب." : "";
            if (users.Count == 1) txbPassword.Focus(); else cboUserFullName.Focus();
        }
        catch (Exception ex) { if (!closed) LoginStatus.Text = ex.GetBaseException().Message; }
        finally { busy = false; if (!closed) BNEXT.IsEnabled = loaded; }
    }
    #endregion

    #region التحقق من بيانات الدخول ومنع الطلبات المتداخلة
    private async void BNEXT_Click(object sender, RoutedEventArgs e) => await SignInAsync();
    public async Task<bool> SignInAsync()
    {
        if (!loaded || busy || closed) return false;
        FieldFeedback.Clear(cboUserFullName); FieldFeedback.Clear(txbPassword);
        if (cboUserFullName.SelectedItem is not T_Maintenance_request_User selected) return FieldFeedback.Reject(cboUserFullName, "اختر حساب المستخدم.");
        if (txbPassword.Password.Length == 0) return FieldFeedback.Reject(txbPassword, "أدخل كلمة المرور.");
        busy = true; BNEXT.IsEnabled = false;
        try
        {
            var user = await store.AuthenticateAsync(selected.User_ID, computer, txbPassword.Password);
            if (closed) return false;
            if (user is null) return FieldFeedback.Reject(txbPassword, "كلمة المرور غير صحيحة أو تغيّرت بيانات الحساب؛ أعد المحاولة.");
            App.curr_user = user; txbPassword.Clear(); signedIn(user); return true;
        }
        catch (Exception ex) { if (!closed) UiMessages.Show(this, "تسجيل الدخول", ex.GetBaseException().Message, MessageBoxButton.OK, MessageBoxImage.Error); return false; }
        finally { busy = false; if (!closed) BNEXT.IsEnabled = true; }
    }
    private async void Password_KeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; await SignInAsync(); } }

    #endregion

    #region تنبيه لوحة المفاتيح وإظهار كلمة المرور

    // يعيد إخفاء الكلمة عند مغادرة الخانة وزر العين معًا.
    private void PasswordArea_FocusChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false && PasswordVisibilityButton is not null)
            PasswordVisibilityButton.IsChecked = false;
    }

    // يستخدم محرر DevExpress نفسه كي تبقى القيمة والربط والتحقق في الخانة الأصلية.
    private void PasswordVisibility_Changed(object sender, RoutedEventArgs e)
    {
        if (txbPassword is null) return;

        if (((ToggleButton)sender).IsChecked == true)
            txbPassword.ShowPassword();
        else
            txbPassword.HidePassword();
    }

    // يقرأ حالة المفتاح عند دخول الخانة ويخفي التنبيه عند مغادرتها.
    private void Password_FocusChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdatePasswordCapsLockWarning();

    // حدّث التنبيه بعد تبديل المفتاح، واترك حدث KeyUp يعالج اختصار الدخول.
    private void Password_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.CapsLock)
            UpdatePasswordCapsLockWarning();
    }

    private void UpdatePasswordCapsLockWarning()
    {
        if (PasswordCapsLockWarning is null) return;

        PasswordCapsLockWarning.Visibility = txbPassword.IsKeyboardFocusWithin && Keyboard.IsKeyToggled(Key.CapsLock)
            ? Visibility.Visible
            : Visibility.Hidden;
    }

    #endregion

    #region إنشاء الحساب وإغلاق نافذة الدخول

    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        new user_requst_windows.Register_Windows { Owner = this }.ShowDialog();
        if (!closed) { loaded = false; await LoadDataAsync(); }
    }
    private void out_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();
    #endregion
}
