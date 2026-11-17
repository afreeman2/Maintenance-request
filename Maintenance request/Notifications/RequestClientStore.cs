using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Maintenance.Shared;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Maintenance_request.Notifications;

// عمليات الموظف تقيد دائمًا بمالِك الطلب، وتعيد فحص الحالة داخل معاملة تمنع السباق مع إصدار التكليف.
public sealed class RequestClientStore(Func<Cont_maintContext> createContext)
{
    #region قراءة الطلبات واستلام ردود المنظومة
    public async Task<List<T_Notif_Action_User>> LoadAsync(long ownerId, CancellationToken token = default) =>
        (await LoadCoreAsync(ownerId, false, token)).Rows;

    public Task<(List<T_Notif_Action_User> Rows, List<(long ActionId, T_Notif_Action_User Request)> PendingCancellations)>
        LoadRepliesAsync(long ownerId, CancellationToken token = default) => LoadCoreAsync(ownerId, true, token);

    private async Task<(List<T_Notif_Action_User> Rows, List<(long ActionId, T_Notif_Action_User Request)> PendingCancellations)>
        LoadCoreAsync(long ownerId, bool includePendingCancellations, CancellationToken token)
    {
        await using var db = createContext();
        var rows = await db.T_Notif_Action_Users.AsNoTracking().Where(x => x.User_ID == ownerId).OrderBy(x => x.ID).ToListAsync(token);
        var actions = await db.T_Notif_Actions.AsNoTracking().Where(x => x.User_ID == ownerId).OrderBy(x => x.rendate).ThenBy(x => x.ID).ToListAsync(token);
        // تجميع القنوات مرة واحدة يمنع إعادة مسح جميع الأحداث لكل طلب عند اتساع القائمة.
        var latestActions = actions.GroupBy(x => x.DOC_ID).ToDictionary(group => group.Key, group => group.Last());
        foreach (var row in rows)
        {
            latestActions.TryGetValue(row.ID, out var latest);
            row.NotificationOperation = latest?.OP_ID ?? row.OP_ID;
            row.NotificationRevision = latest?.rendate ?? row.rendate;
        }
        List<(long ActionId, T_Notif_Action_User Request)> pendingCancellations = [];
        if (includePendingCancellations)
        {
            var requestIds = rows.Select(row => row.ID).ToHashSet();
            // إذا سبق التكليف الجديد استلام الإلغاء، نعرض الحدث السابق منفصلًا عن صف الحالة الحالية.
            foreach (var action in actions.Where(x => x.OP_ID == RequestProtocol.ExecutionCancelled && x.state == 0
                && requestIds.Contains(x.DOC_ID) && latestActions[x.DOC_ID].ID != x.ID))
            {
                pendingCancellations.Add((action.ID, new T_Notif_Action_User
                {
                    ID = action.DOC_ID, OP_ID = action.OP_ID, NotificationOperation = action.OP_ID,
                    NotificationRevision = action.rendate
                }));
            }
        }
        return (rows, pendingCancellations);
    }

    // تأكيد حدث سابق لا يعدّل رأس الطلب أو رقم التكليف الجديد.
    public async Task ReceivePreviousCancellationAsync(long actionId, long ownerId, CancellationToken token = default)
    {
        await using var db = createContext();
        await db.T_Notif_Actions.Where(x => x.ID == actionId && x.User_ID == ownerId
            && x.OP_ID == RequestProtocol.ExecutionCancelled && x.state == 0)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.state, 1), token);
    }
    public async Task<bool> ReceiveAsync(long requestId, long ownerId, int expectedOperation, CancellationToken token = default)
    {
        if (expectedOperation is not (2 or 3 or 7 or 8)) return false;
        await using var db = createContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var request = await db.T_Notif_Action_Users.SingleOrDefaultAsync(x => x.ID == requestId && x.User_ID == ownerId, token);
        // قد يكون المدير غيّر المرحلة أثناء عرض التنبيه؛ لا يستبدل الاستلام القديم الرد الجديد.
        if (request is null || request.OP_ID != expectedOperation || request.state != 0) return false;
        request.OP_ID = RequestProtocol.AfterReceipt(expectedOperation); request.state = 1;
        request.MaintenanceCosts_State = request.OP_ID == RequestProtocol.Closed;
        if (expectedOperation == RequestProtocol.ExecutionCancelled) request.MaintenanceCosts_No = -1;
        var action = await LatestActionAsync(db, request.ID, ownerId, token);
        if (action is not null)
        {
            // نحفظ العملية الأصلية في قناة النقل حتى يتلقاها كل مستخدمي المنظومة ولو استقر الرأس قبلاً.
            action.state = 1;
            action.MaintenanceCosts_No = request.MaintenanceCosts_No;
            action.MaintenanceCosts_State = request.MaintenanceCosts_State;
            // قناة 1 للطلب الجديد و2 للجاري و6 للمنتهي؛ OP_ID هو المرجع الفعلي للمرحلة.
            action.Branch_ID = (short)request.OP_ID;
        }
        await db.SaveChangesAsync(token); await tx.CommitAsync(token); return true;
    }

    private static Task<T_Notif_Action?> LatestActionAsync(Cont_maintContext db, long requestId, long ownerId, CancellationToken token) =>
        db.T_Notif_Actions.Where(x => x.DOC_ID == requestId && x.User_ID == ownerId)
            .OrderByDescending(x => x.rendate).ThenByDescending(x => x.ID).FirstOrDefaultAsync(token);
    #endregion

    #region تعديل الطلب الجديد وإلغاؤه دون التأثير في الطلبات المنفذة
    public async Task UpdateAsync(long requestId, long ownerId, string description, short serviceNumber, string serviceName, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new InvalidOperationException("أدخل بيان طلب الصيانة.");
        await using var db = createContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var request = await EditableAsync(db, requestId, ownerId, token);
        request.Description = description.Trim(); request.Serv_num = serviceNumber; request.Serv_type = serviceName;
        foreach (var action in await db.T_Notif_Actions.Where(x => x.DOC_ID == request.ID && x.User_ID == ownerId).ToListAsync(token))
        { action.Description = request.Description; action.Serv_num = serviceNumber; action.Serv_type = serviceName; }
        // state وOP_ID وDOC_Type لا تتغير عند تعديل النص أو الخدمة، فلا يتولد إشعار إدراج مكرر.
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }
    public async Task CancelAsync(long requestId, long ownerId, CancellationToken token = default)
    {
        await using var db = createContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var request = await EditableAsync(db, requestId, ownerId, token);
        request.OP_ID = RequestProtocol.Cancelled; request.state = 0; request.rendate = DateTime.Now;
        var action = await LatestActionAsync(db, request.ID, ownerId, token);
        if (action is not null)
        { action.OP_ID = RequestProtocol.Cancelled; action.state = 0; action.Branch_ID = 4; action.rendate = request.rendate; }
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }
    private static async Task<T_Notif_Action_User> EditableAsync(Cont_maintContext db, long requestId, long ownerId, CancellationToken token = default)
    {
        var request = await db.T_Notif_Action_Users.SingleOrDefaultAsync(x => x.ID == requestId && x.User_ID == ownerId, token)
            ?? throw new InvalidOperationException("الطلب غير موجود أو لا يتبع المستخدم الحالي.");
        if (request.OP_ID != RequestProtocol.New || request.MaintenanceCosts_No is > 0)
            throw new InvalidOperationException("يسمح بتعديل الطلب أو إلغائه قبل إصدار تكليف الصيانة فقط.");
        return request;
    }
    // حذف إشعار الإنجاز محلي بعد إعادة فحص الملكية والحالة، وليس حذف طلب أو مستند.
    public async Task<T_Notif_Action_User> CompletedNoticeAsync(long requestId, long ownerId)
    {
        var row = (await LoadAsync(ownerId)).SingleOrDefault(x => x.ID == requestId)
            ?? throw new InvalidOperationException("الطلب لا يتبع المستخدم الحالي أو لم يعد موجودًا.");
        if (row.OP_ID is not (3 or 6)) throw new InvalidOperationException("يمكن حذف إشعار الطلب المنفذ فقط؛ حدّث القائمة.");
        return row;
    }
    #endregion
}
