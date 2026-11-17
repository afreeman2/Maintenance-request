using MahApps.Metro.Controls;
using Maintenance_request.Registration;
using Maintenance_request.Support;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Maintenance_request.Settings;

public partial class Password_Chanche : MetroWindow
{
    #region تهيئة النافذة والتحقق الفوري من الأخطاء عند التعديل
    private bool saving;
    public Password_Chanche() { InitializeComponent(); Loaded += (_, _) => txbPassword.Focus(); }
    private void btnCancel_Click(object sender, RoutedEventArgs e) => Close();
    private async void Confirmation_KeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) await SaveAsync(); }
    private async void Insert_Click(object sender, RoutedEventArgs e) => await SaveAsync();
    #endregion

    #region حفظ كلمة المرور دون تكرار العملية أو تغيير جلسة المستخدم قبل نجاح الحفظ
    private async Task SaveAsync()
    {
        if (saving) return;
        FieldFeedback.Clear(txbPassword); FieldFeedback.Clear(txb_new_Password); FieldFeedback.Clear(txb_sh_Password);
        if (txbPassword.Password.Length == 0) { FieldFeedback.Reject(txbPassword, "أدخل كلمة المرور الحالية."); return; }
        if (RegistrationStore.PasswordError(txb_new_Password.Password, txb_sh_Password.Password) is string error)
        { FieldFeedback.Reject(txb_new_Password.Password == txb_sh_Password.Password ? txb_new_Password : txb_sh_Password, error); return; }
        saving = true; SaveButton.IsEnabled = false;
        try
        {
            long owner = App.curr_user.User_ID;
            string encoded = await new AccountStore().ChangePasswordAsync(owner, txbPassword.Password, txb_new_Password.Password, txb_sh_Password.Password);
            if (App.curr_user.User_ID == owner) App.curr_user.PassWord = encoded;
            txbPassword.Clear(); txb_new_Password.Clear(); txb_sh_Password.Clear();
            UiMessages.Show(this, "حساب المستخدم", "تم تغيير كلمة المرور.", MessageBoxButton.OK, MessageBoxImage.Information); Close();
        }
        catch (ArgumentException ex) { FieldFeedback.Reject(txbPassword, ex.Message); }
        catch (Exception ex) { UiMessages.Show(this, "حساب المستخدم", ex.GetBaseException().Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { saving = false; SaveButton.IsEnabled = true; }
    }
    #endregion
}
