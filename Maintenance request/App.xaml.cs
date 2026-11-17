using DevExpress.Data;
using DevExpress.Pdf.Xmp;
using DevExpress.XtraRichEdit;
using Hardcodet.Wpf.TaskbarNotification;
using Maintenance_app.DatabContext;
using Maintenance_request.converts;
using Maintenance_request.data;
using Microsoft.Win32;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Maintenance_request
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // متغير للتأكد مما إذا كان التطبيق قُد تم فتحه عن طريق الإشعار
        public static bool LaunchedFromNotification { get; set; } = false;
        public static string Branch_Name = "";
        public static string DB_US = "";
        public static string from_time = "";
        public static string saPassword = "";
        public static string CONSTRING = "";
        public static string usname = "";
        public static T_Maintenance_request_User curr_user;
        public static int ex_win;
        public static bool isdel=false; 
        private static Mutex _mutex = null;
        public const string ApplicationID = "منظومة طلبات الصيانة";

        public const string ApplicationName = "منظومة طلبات الصيانة";
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                DevExpress.Xpf.Core.ApplicationThemeHelper.UpdateApplicationThemeName();
                DevExpress.Data.ShellHelper.TryCreateShortcut(ApplicationName, ApplicationName);
                //string aumid = ApplicationID;
                //string displayName = ApplicationName;
                //WindowsToastBootstrapper.RemoveShortcutOnUnload = false;
                //WindowsToastBootstrapper.Initialize(aumid, displayName);
               
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Maintenance_request.Support.UiMessages.Show(ex.Message);
            }
        }





        #region دوال الترميز وإدارة بيانات التطبيق

        #region دالة فك التشفير
        public static string decrypt(string encryptpwd)
        {
            string decryptpwd = string.Empty;
            UTF8Encoding encodepwd = new UTF8Encoding();
            Decoder Decode = encodepwd.GetDecoder();
            byte[] todecode_byte = Convert.FromBase64String(encryptpwd);
            int charCount = Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
            char[] decoded_char = new char[charCount - 1 + 1];
            Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
            decryptpwd = new String(decoded_char);
            return decryptpwd;
        }

        #endregion

        #region دالة التشفير
        public static string encrypt(string password)
        {
            string strmsg = string.Empty;
            byte[] encode = new byte[password.Length - 1 + 1];
            encode = Encoding.UTF8.GetBytes(password);
            strmsg = Convert.ToBase64String(encode);
            return strmsg;
        }
        #endregion
        #endregion

        public void checkUpRegApp1()
        {
            try
            {
                // ---------------------------------------------------------------
                // التحقق من قانونية المنظومة
                // ----------------------------------------

                // Create the example key with registry security.




                RegistryKey rk3 = null;
                RegistryKey rk4 = null;
                if (Environment.Is64BitOperatingSystem)
                {
                    rk3 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
                    rk3 = rk3.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SSF\Maintenance_services\PassUserSQL", false);

                    rk4 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);

                    rk4 = rk4.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SSF\Maintenance_services\NameUserSQL", false);
                }
                else
                {
                    rk3 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SSF\Maintenance_services\PassUserSQL", false);
                    rk4 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SSF\Maintenance_services\NameUserSQL", false);
                }



                if ((rk3 == null))
                {
                    if (Environment.Is64BitOperatingSystem == true)
                    {
                        ProcessStartInfo procStartInfo = new ProcessStartInfo();
                        Process procExecuting = new Process();

                        {
                            var withBlock = procStartInfo;
                            withBlock.UseShellExecute = true;
                            withBlock.FileName = "تسجيل المنظومة-64.exe";

                            withBlock.WindowStyle = ProcessWindowStyle.Normal;
                            withBlock.Verb = "runas"; // add this to prompt for elevation
                        }

                        procExecuting = Process.Start(procStartInfo);
                        System.Environment.Exit(0);
                    }
                    else
                    {
                        ProcessStartInfo procStartInfo = new ProcessStartInfo();
                        Process procExecuting = new Process();

                        {
                            var withBlock = procStartInfo;
                            withBlock.UseShellExecute = true;
                            withBlock.FileName = "تسجيل المنظومة-32.exe";

                            withBlock.WindowStyle = ProcessWindowStyle.Normal;
                            withBlock.Verb = "runas"; // add this to prompt for elevation
                        }

                        procExecuting = Process.Start(procStartInfo);
                        System.Environment.Exit(0);
                    }
                }

                saPassword = decrypt((string)rk3.GetValue("PassUserSQL", 0.0));
                DB_US = decrypt((string)rk4.GetValue("NameUserSQL", 0.0));
            }
            catch (Exception ex)
            {
                Maintenance_request.Support.UiMessages.Show("خطأ في تسجيل المنظومة." + ex.Message);
                System.Environment.Exit(0);
            }
        }
        #region اتصال التطبيق عند بدء التشغيل

        private static string? installedConnectionString;

        // تستعمل جميع الشاشات اتصال النسخة المثبتة، وتبقى صيغة الاتصال القديمة متاحة للنسخ السابقة.
        internal static string DatabaseConnectionString => installedConnectionString
            ?? ConfigurationManager.AppSettings["connstring"] + DB_US + ";Password=" + saPassword
               + ";TrustServerCertificate=True;";

        private void Application_Startup(object sender, StartupEventArgs e)
        {

            try
            {

















                string installedSettings = Path.Combine(AppContext.BaseDirectory, InstalledConnectionSettings.FileName);
                if (File.Exists(installedSettings))
                {
                    installedConnectionString = InstalledConnectionSettings.Read(installedSettings);
                    new Login().ShowDialog();
                    return;
                }

                checkUpRegApp1();
                if (File.Exists(System.AppDomain.CurrentDomain.BaseDirectory + @"\ConectionString") == false)
                {
                    Maintenance_request.Support.UiMessages.Show("الملف الخاص بالأتصال غير موجود", "تطبيق طلبات الصيانة", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    ConfigurationManager.AppSettings["connstring"] = File.ReadAllText(System.AppDomain.CurrentDomain.BaseDirectory + @"\ConectionString");
                    CONSTRING = ConfigurationManager.AppSettings["connstring"];
                }






                Login f = new Login();
                f.ShowDialog();
            }
            catch (Exception ex)
            {

                Maintenance_request.Support.UiMessages.Show(ex.Message);
                Maintenance_request.Support.UiMessages.Show("خطأ غير متوقع سيتم فرض إغلاق التطبيق ");
                App.Current.Shutdown();

            }

        }

        #endregion

    }

}
