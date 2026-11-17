using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Maintenance_app.DatabContext;
using MaintenancePackaging;
using Microsoft.Data.SqlClient;

namespace MaintenanceRequestSetup;

internal static class Installation
{
    #region إعداد قاعدة البيانات

    public const string ProductName = "تطبيق طلبات الصيانة";
    public const string DestinationDatabase = "Maintenance_Dabase";
    private const string ShortcutName = "منظومة طلبات الصيانة";
    private static readonly Assembly Resources = typeof(Installation).Assembly;
    private static string InstallRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SSF", "MaintenanceRequest");
    private static string ApplicationDirectory => Path.Combine(InstallRoot, "Application");

    // يتحقق من صلاحية قراءة جداول التطبيق المشتركة دون كتابة بيانات.
    public static async Task VerifyConnectionAsync(SqlConnection connection)
    {
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (0) 1 FROM dbo.T_Maintenance_request_Users;
            SELECT TOP (0) 1 FROM dbo.T_Maintenance_Request;
            SELECT TOP (0) 1 FROM dbo.T_Build;
            SELECT TOP (0) 1 FROM dbo.T_Service;
            SELECT TOP (0) 1 FROM dbo.T_Mangment_Request;
            SELECT TOP (0) 1 FROM dbo.T_Notif_Action;
            SELECT TOP (0) 1 FROM dbo.T_Notif_Action_Users;
            """;
        await command.ExecuteNonQueryAsync();
    }

    #endregion

    #region تثبيت الملفات وإنشاء الاختصار

    // يُفك التطبيق داخل مجلد مؤقت أولًا، ثم تُستبدل النسخة السابقة بعد نجاح الاتصال.
    public static async Task<string> InstallAsync(
        string connectionString, IProgress<string> progress)
    {
        string root = InstallRoot;
        Directory.CreateDirectory(root);
        using var installationLock = AcquireInstallationLock(root);
        string stage = Path.Combine(root, "stage-" + Guid.NewGuid().ToString("N"));
        string previous = Path.Combine(root, "previous-" + Guid.NewGuid().ToString("N"));
        string application = ApplicationDirectory;
        string executable = Path.Combine(application, ProductName + ".exe");

        CheckInstalledApplicationClosed(executable);

        try
        {
            progress.Report("جارٍ اختبار الاتصال بقاعدة الوجهة…");
            await using var connection = new SqlConnection(connectionString);
            await VerifyConnectionAsync(connection);

            progress.Report("جارٍ تجهيز ملفات التطبيق ومكونات التشغيل…");
            await Task.Run(() =>
            {
                using var archive = new ZipArchive(OpenResource("Application.zip"), ZipArchiveMode.Read);
                archive.ExtractToDirectory(stage);
                if (!File.Exists(Path.Combine(stage, ProductName + ".exe")))
                    throw new InvalidOperationException("ملف تشغيل التطبيق مفقود من الحزمة.");
                InstalledConnectionSettings.Write(stage, connectionString);
            });

            progress.Report("جارٍ اعتماد التثبيت وإنشاء اختصاري ابدأ وسطح المكتب…");
            CheckInstalledApplicationClosed(executable);
            try
            {
                if (Directory.Exists(application))
                    Directory.Move(application, previous);
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                throw new InvalidOperationException("ملفات التطبيق قيد الاستخدام. أغلق نافذة التطبيق ثم أعد محاولة التثبيت.", ex);
            }

            try
            {
                Directory.Move(stage, application);
                InstallerShortcuts.Create(executable, ShortcutName);
            }
            catch
            {
                if (Directory.Exists(application))
                    Directory.Move(application, stage);
                if (Directory.Exists(previous))
                    Directory.Move(previous, application);
                throw;
            }

            CleanStaging(previous);
            return executable;
        }
        finally
        {
            CleanStaging(stage);
        }
    }

    private static FileStream AcquireInstallationLock(string root)
    {
        // يُميّز القفل بين وجود نافذة تثبيت أخرى ووجود نسخة مفتوحة من التطبيق.
        try
        {
            return new FileStream(Path.Combine(root, "setup.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex) when (IsSharingViolation(ex))
        {
            throw new InvalidOperationException("توجد عملية تثبيت أخرى قيد التنفيذ. انتظر اكتمالها ثم أعد المحاولة.", ex);
        }
    }

    private static void CheckInstalledApplicationClosed(string executable)
    {
        // نقارن المسار حتى لا نمنع التثبيت بسبب نسخة تعمل من مجلد مختلف.
        var processes = Process.GetProcessesByName(ProductName);
        try
        {
            foreach (var process in processes)
            {
                string? runningExecutable;
                try
                {
                    runningExecutable = process.MainModule?.FileName;
                }
                catch (Win32Exception) { continue; }
                catch (InvalidOperationException) { continue; }

                if (string.Equals(runningExecutable, executable, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("التطبيق مفتوح الآن. أغلق نافذة «تطبيق طلبات الصيانة» ثم أعد محاولة التثبيت.");
            }
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    private static bool IsSharingViolation(IOException error) =>
        (error.HResult & 0xFFFF) is 32 or 33;

    private static Stream OpenResource(string name) =>
        Resources.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException("مورد تثبيت مطلوب مفقود من الحزمة.");

    private static void CleanStaging(string path)
    {
        // قد يحتفظ Windows مؤقتًا بمقبض ملف من النسخة السابقة بعد تشغيلها.
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    #endregion
}
