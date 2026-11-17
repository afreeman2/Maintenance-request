using Maintenance_request.data;
using System.IO;
using System.Text.Json;

namespace Maintenance_request.Notifications;

// إيصالات الحساب محلية مثل مركز المنظومة؛ لا نحذف رأس الطلب الذي يحتاجه إلغاء الإنجاز لاحقًا.
public sealed class ClientNotificationState
{
    #region حفظ العرض والإخفاء لكل حساب ونسخة عملية
    private long owner;
    private string? file;
    private HashSet<string> entries = [];
    public void Configure(long userId, string? directory = null)
    {
        if (owner == userId && file is not null) return;
        owner = userId;
        file = Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Maintenance_request", "Notifications"), "receipts-" + userId + ".json");
        try { entries = File.Exists(file) ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(file)) ?? [] : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { entries = []; }
    }
    public static int Operation(T_Notif_Action_User row)
    {
        int operation = row.NotificationOperation == 0 ? row.OP_ID : row.NotificationOperation;
        return operation == 6 ? 3 : operation;
    }
    public static string Key(T_Notif_Action_User row) => row.ID + ":" + Operation(row) + ":" + (row.NotificationRevision == default ? row.rendate : row.NotificationRevision).ToString("O");
    public bool WasShown(T_Notif_Action_User row) => entries.Contains("shown:" + Key(row));
    // قراءة البطاقة مستقلة عن إيصال عرض البالون، وتسجّل لكل نسخة من عملية الطلب.
    public bool IsRead(T_Notif_Action_User row) => entries.Contains("read:" + Key(row));
    public void MarkRead(IEnumerable<T_Notif_Action_User> rows)
    {
        bool changed = false;
        foreach (var row in rows) changed |= entries.Add("read:" + Key(row));
        if (changed) Save();
    }
    public bool IsDismissed(T_Notif_Action_User row) => entries.Contains("hidden:" + Key(row));
    public void MarkShown(T_Notif_Action_User row) { entries.Add("shown:" + Key(row)); Save(); }
    public void Dismiss(T_Notif_Action_User row) { entries.Add("hidden:" + Key(row)); Save(); }
    private void Save()
    {
        if (file is null) return;
        try { Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(entries)); File.Move(file + ".tmp", file, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    #endregion
}
