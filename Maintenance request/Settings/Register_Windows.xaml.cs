using MahApps.Metro.Controls;
using MahApps.Metro.SimpleChildWindow;
using Maintenance_request.converts;
using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Management;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using ComboBox = System.Windows.Controls.ComboBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace Maintenance_request.Settings
{
    /// <summary>
    /// Interaction logic for Register_Windows.xaml
    /// </summary>
    public partial class Register_Windows : MetroWindow
    {
        public Register_Windows()
        {
            InitializeComponent();
        }

        public string GetCpuId()
        {
            string cpuID = string.Empty;

            ManagementObjectSearcher mc = new ManagementObjectSearcher("Select * From Win32_BaseBoard");

            foreach (ManagementObject mo in mc.Get())
            {
                if (string.IsNullOrEmpty(cpuID))
                {
                    cpuID = mo["SerialNumber"].ToString();
                }
            }

            return cpuID;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.Windows.OfType<Login>().Any())
            {
                Application.Current.Shutdown();
            }
            else
            {
                Close();
            }
        }

        private void btnAddNewVehicleSupplier_Click(object sender, RoutedEventArgs e)
        {
        }

        private void But_Canc_Click(object sender, RoutedEventArgs e)
        {
        }

        private void txtepm_name_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtepm_name.Tag = 0;
        }

        private void txtepm_name_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                txb_new_Password.Focus();
        }

        private void txb_new_Password_PasswordChanged(object sender, RoutedEventArgs e)
        {
            txb_new_Password.Tag = 0;
        }

        private void txb_new_Password_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                txb_sh_Password.Focus();
        }

        private void txb_sh_Password_PasswordChanged(object sender, RoutedEventArgs e)
        {
            txb_sh_Password.Tag = 0;
        }

        private void txb_sh_Password_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                cbomangment.Focus();
        }

        private void cbomangment_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                _Combo_Bulding.Focus();
        }

        private void save_User_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DbContextHelper.InitOptions(App.DatabaseConnectionString);
                using (var My_LinQ = new Cont_maintContext(DbContextHelper.Options))
                {
                    if (txtepm_name.Text.Length == 0)
                    {
                        txtepm_name.Tag = 1;
                        ErrorRule.ErrorMessage = "أدخل اسم المستخدم";
                        txtepm_name.GetBindingExpression(TextBox.TagProperty)?.UpdateSource();
                        txtepm_name.Focus();
                        return;
                    }

                    if (My_LinQ.T_Maintenance_request_Users.Any(x =>
                            x.PC_ID == GetCpuId() &&
                            EF.Functions.Like(x.User_Name, txtepm_name.Text)))
                    {
                        txtepm_name.Tag = 1;
                        ErrorRule.ErrorMessage = "أسم المستخدم مسجل بالفعل";
                        txtepm_name.GetBindingExpression(TextBox.TagProperty)?.UpdateSource();
                        txtepm_name.Focus();
                        return;
                    }

                    if (txb_new_Password.Password.Length == 0)
                    {
                        txb_new_Password.Tag = 1;
                        ErrorRule.ErrorMessage = "أدخل كلمة المرور";
                        txb_new_Password.GetBindingExpression(PasswordBox.TagProperty)?.UpdateSource();
                        txb_new_Password.Focus();
                        return;
                    }

                    if (txb_new_Password.Password.Length < 3)
                    {
                        txb_new_Password.Tag = 1;
                        ErrorRule.ErrorMessage = "أقل طول لكلمة المرور ثلاثة أرقام أو أحرف";
                        txb_new_Password.GetBindingExpression(PasswordBox.TagProperty)?.UpdateSource();
                        txb_new_Password.Focus();
                        return;
                    }

                    if (txb_sh_Password.Password.Length == 0)
                    {
                        txb_sh_Password.Tag = 1;
                        ErrorRule.ErrorMessage = "أدخل تأكيد كلمة المرور";
                        txb_new_Password.GetBindingExpression(PasswordBox.TagProperty)?.UpdateSource();
                        txb_sh_Password.Focus();
                        return;
                    }

                    if (txb_sh_Password.Password != txb_new_Password.Password)
                    {
                        txb_sh_Password.Tag = 1;
                        ErrorRule.ErrorMessage = "كلمة المرور غير مطابقة لتأكيد كلمة المرور";
                        txb_sh_Password.GetBindingExpression(PasswordBox.TagProperty)?.UpdateSource();
                        txb_sh_Password.Focus();
                        return;
                    }

                    // if (cboAdjective.SelectedIndex == -1)
                    // {
                    //     cboAdjective.Tag = 1;
                    //     ErrorRule.ErrorMessage = "أدخل الصفة";
                    //     cboAdjective.GetBindingExpression(ComboBox.TagProperty)?.UpdateSource();
                    //     cboAdjective.Focus();
                    //     return;
                    // }

                    if (cbomangment.SelectedIndex == -1)
                    {
                        cbomangment.Tag = 1;
                        ErrorRule.ErrorMessage = "أدخل الإدارة";
                        cbomangment.GetBindingExpression(ComboBox.TagProperty)?.UpdateSource();
                        cbomangment.Focus();
                        return;
                    }

                    if (_Combo_Bulding.SelectedIndex == -1)
                    {
                        _Combo_Bulding.Tag = 1;
                        ErrorRule.ErrorMessage = "أدخل المبني";
                        _Combo_Bulding.GetBindingExpression(ComboBox.TagProperty)?.UpdateSource();
                        _Combo_Bulding.Focus();
                        return;
                    }

                    var new_user = new T_Maintenance_request_User
                    {
                        // Functionality_ID = cboAdjective.SelectedIndex,
                        Mangment_Request_ID = (int)cbomangment.SelectedValue,
                        PassWord = App.encrypt(txb_new_Password.Password),
                        User_Name = txtepm_name.Text,
                        PC_ID = GetCpuId(),
                        State = true,
                        ISsound = true,
                        Sound_CHoise = 2,
                        Build_ID =Convert.ToInt16(_Combo_Bulding.SelectedValue)
                    };

                    My_LinQ.T_Maintenance_request_Users.Add(new_user);
                    My_LinQ.SaveChanges();

                    t1.Foreground = Brushes.Black;
                    t1.Text = "تم تسجيل المستخدم";
                    ch2.SetCurrentValue(ChildWindow.IsOpenProperty, true);

                    txtepm_name.Focus();

                    App.curr_user = new_user;

                    MainWindow f = new MainWindow
                    {
                        Owner = this
                    };

                    Hide();
                    f.ShowDialog();
                    Close();

                }
                // Application.Current.Shutdown();
                // Process.Start(Application.ResourceAssembly.Location);
            }
            catch (Exception ex)
            {
                Maintenance_request.Support.UiMessages.Show(ex.Message);
            }
        }

        private void onok(object sender, RoutedEventArgs e)
        {
            ch2.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
