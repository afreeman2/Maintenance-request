using DevExpress.Xpf.Core;
using MahApps.Metro.Controls;
using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Maintenance_request.Registration;
using System.Collections.ObjectModel;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Maintenance_request.user_requst_windows;

public partial class Register_Windows : MetroWindow
{
    #region القوائم وحالة التحميل والحفظ ومصادر البيانات
    // النوع الصحيح للإدارات T_Mangment_Request، والقائمتان مرتبطتان بتصميم XAML مباشرة.
    public ObservableCollection<T_Mangment_Request> mang_list { get; } = [];
    public ObservableCollection<T_Build> building_list { get; } = [];
    private readonly RegistrationStore store;
    private readonly Func<string> computerIdProvider;
    private readonly Action<T_Maintenance_request_User> registrationCompleted;
    private readonly HashSet<Control> invalidFields = [];
    private bool loading, loaded, saving, closed;
    private T_Maintenance_request_User? registeredUser;
    public Register_Windows() : this(null, null, null) { }
    // المصادر البديلة تسمح بفحص الواجهة والحفظ دون الجهاز أو قاعدة العمل الحقيقية.
    public Register_Windows(Func<Cont_maintContext>? contextFactory, Func<string>? computerId, Action<T_Maintenance_request_User>? onRegistered)
    {
        store = new(contextFactory ?? CreateContext); computerIdProvider = computerId ?? ReadComputerId;
        registrationCompleted = onRegistered ?? OpenMainWindow;
        InitializeComponent(); DataContext = this; Closed += (_, _) => closed = true;
    }
    private static Cont_maintContext CreateContext()
    {
        DbContextHelper.InitOptions(App.DatabaseConnectionString);
        return new(DbContextHelper.Options!);
    }
    #endregion

    #region تحميل الإدارات والمباني وقراءة معرّف الجهاز
    private async void Register_Windows_Loaded(object sender, RoutedEventArgs e) => await LoadDataAsync();
    public async Task LoadDataAsync()
    {
        if (loaded || loading) return;
        loading = true; save_User.IsEnabled = false; LoadingIndicator.DeferedVisibility = true;
        try
        {
            var options = await store.LoadAsync(); if (closed) return;
            mang_list.Clear(); foreach (var row in options.Managements) mang_list.Add(row);
            building_list.Clear(); foreach (var row in options.Buildings) building_list.Add(row);
            loaded = true; ClearErrors(); txtepm_name.Focus();
        }
        catch (Exception ex) { if (!closed) ShowMessage("تعذر تحميل الإدارات والمباني. " + ex.GetBaseException().Message, MessageBoxImage.Error); }
        finally { loading = false; if (!closed) { LoadingIndicator.DeferedVisibility = false; save_User.IsEnabled = loaded && !saving; } }
    }
    private static string ReadComputerId()
    {
        using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
        using var results = searcher.Get();
        foreach (ManagementObject board in results)
            using (board)
                if (board["SerialNumber"]?.ToString()?.Trim() is { Length: > 0 } id) return id;
        return string.Empty;
    }
    public string getCPU_ID() => computerIdProvider();
    #endregion

    #region التحقق المرئي ومسحه فور الكتابة أو الاختيار
    private bool Reject(Control field, string message)
    {
        if (field.GetBindingExpression(TagProperty) is { } binding)
        {
            Validation.MarkInvalid(binding, new ValidationError(new ExceptionValidationRule(), binding, message, null)); invalidFields.Add(field);
        }
        FormPanel.IsEnabled = true; field.Focus(); return false;
    }
    private void ClearError(Control field)
    {
        if (field.GetBindingExpression(TagProperty) is { } binding) Validation.ClearInvalid(binding);
        invalidFields.Remove(field);
    }
    private void ClearErrors() { foreach (var field in invalidFields.ToArray()) ClearError(field); }
    private void Input_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not Control field) return; ClearError(field);
        // للتمييز اللوني نمرر وجود قيمة فقط؛ لا نربط أو نسجل نص كلمة المرور.
        bool filled = field switch { TextBox text => !string.IsNullOrWhiteSpace(text.Text), PasswordBox password => password.Password.Length > 0, ComboBox combo => combo.SelectedItem is not null, _ => false };
        if (field.Parent is FrameworkElement grid && grid.Parent is Border surface) surface.Tag = filled;
    }
    private void Input_PreviewTextInput(object sender, TextCompositionEventArgs e) { if (sender is Control field) ClearError(field); }
    private bool ValidateFields()
    {
        ClearErrors();
        if (string.IsNullOrWhiteSpace(txtepm_name.Text) || txtepm_name.Text.Trim().Length > 100) return Reject(txtepm_name, "أدخل اسم المستخدم بما لا يتجاوز100 حرف.");
        if (RegistrationStore.PasswordError(txb_new_Password.Password, txb_sh_Password.Password) is string error)
        {
            bool confirmation = txb_new_Password.Password.Length >= 3 && (string.IsNullOrEmpty(txb_sh_Password.Password) || txb_sh_Password.Password != txb_new_Password.Password);
            return Reject(confirmation ? txb_sh_Password : txb_new_Password, error);
        }
        if (cbomangment.SelectedItem is not T_Mangment_Request) return Reject(cbomangment, "اختر الإدارة التابع لها.");
        if (_Combo_Bulding.SelectedItem is not T_Build) return Reject(_Combo_Bulding, "اختر المبنى.");
        return true;
    }
    #endregion

    #region الحفظ غير المتزامن وفتح المنظومة بعد الاعتماد
    // حدث Click يجب أن يكون void؛ الدالة المستقلة قابلة للانتظار من الزر وEnter والفحص.
    private async void save_User_Click(object sender, RoutedEventArgs e) => await SaveUserAsync();
    public async Task<bool> SaveUserAsync()
    {
        if (saving || loading || !loaded) return false;
        if (registeredUser is null && !ValidateFields()) return false;
        saving = true; FormPanel.IsEnabled = false; save_User.IsEnabled = false; LoadingIndicator.DeferedVisibility = true;
        try
        {
            if (registeredUser is null)
            {
                string computer = getCPU_ID();
                if (string.IsNullOrWhiteSpace(computer) || computer.Length > 50) { ShowMessage("تعذرت قراءة معرّف الجهاز بصورة صحيحة؛ أعد المحاولة.", MessageBoxImage.Error); return false; }
                registeredUser = await store.RegisterAsync(new(txtepm_name.Text, txb_new_Password.Password, txb_sh_Password.Password,
                    computer, ((T_Mangment_Request)cbomangment.SelectedItem).Mangment_Request_ID, ((T_Build)_Combo_Bulding.SelectedItem).Build_ID));
                if (closed) return true; ShowMessage("تم تسجيل المستخدم.", MessageBoxImage.Information);
            }
            // لا يتغير المستخدم الحالي قبل الاعتماد، ولا يتكرر الإدراج إذا تعذر فتح الرئيسية.
            App.curr_user = registeredUser; txb_new_Password.Clear(); txb_sh_Password.Clear();
            registrationCompleted(registeredUser); return true;
        }
        catch (DuplicateRegistrationException ex) { if (!closed) Reject(txtepm_name, ex.Message); return false; }
        catch (Exception ex) { if (!closed) ShowMessage(ex.GetBaseException().Message, MessageBoxImage.Error); return false; }
        finally { saving = false; if (!closed) { FormPanel.IsEnabled = true; save_User.IsEnabled = loaded; LoadingIndicator.DeferedVisibility = false; } }
    }
    private void OpenMainWindow(T_Maintenance_request_User user)
    {
        var main = new MainWindow { Owner = this }; Hide();
        try { main.ShowDialog(); Close(); } catch { Show(); throw; }
    }

    private void save_User_Copy_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();

    }

    private void ShowMessage(string text, MessageBoxImage icon) => Maintenance_request.Support.UiMessages.Show(this, "تطبيق طلبات الصيانة", text, MessageBoxButton.OK, MessageBoxResult.OK, icon);
    #endregion

    #region التنقل والإغلاق
    private async void Input_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not UIElement field) return; e.Handled = true;
        if (ReferenceEquals(field, _Combo_Bulding)) await SaveUserAsync();
        else field.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }
    private void btnClose_Click(object sender, RoutedEventArgs e)
    {
        if (saving) return;
        if (Application.Current.Windows.OfType<Login>().Any()) Application.Current.Shutdown(); else Close();
    }
    #endregion
}
