using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Maintenance_request.Support;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Maintenance_request.Requests;

public sealed class RequestComposerStore(Func<Cont_maintContext>? factory = null)
{
    #region قوائم الخدمات وإرسال الرأس وقناة الإشعار في معاملة واحدة
    private Cont_maintContext Create() => factory?.Invoke() ?? ClientContext.Create();
    public async Task<List<T_Service>> ServicesAsync(CancellationToken token = default)
    {
        await using var db = Create();
        return await db.T_Services.AsNoTracking().OrderBy(x => x.Serv_type).ToListAsync(token);
    }
    public async Task<long> SendAsync(long ownerId, string description, short serviceId, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new InvalidOperationException("أدخل تفاصيل طلب الصيانة.");
        await using var db = Create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var user = await db.T_Maintenance_request_Users.AsNoTracking().SingleOrDefaultAsync(x => x.User_ID == ownerId, token)
            ?? throw new InvalidOperationException("تعذر تحديد حساب المستخدم الحالي.");
        var service = await db.T_Services.AsNoTracking().SingleOrDefaultAsync(x => x.Serv_num == serviceId, token)
            ?? throw new InvalidOperationException("الخدمة المختارة لم تعد موجودة؛ حدّث القائمة.");
        var build = await db.T_Builds.AsNoTracking().SingleOrDefaultAsync(x => x.Build_ID == user.Build_ID, token);
        var management = await db.T_Mangment_Requests.AsNoTracking().SingleOrDefaultAsync(x => x.Mangment_Request_ID == user.Mangment_Request_ID, token);
        // وقت موحد يربط نسخة الحدث بين التطبيق والمنظومة ويمنع إعادة تنبيه الإرسال.
        var sentAt = DateTime.Now;
        var request = new T_Notif_Action_User { User_ID = ownerId, User_Name = user.User_Name,
            Description = description.Trim(), Serv_num = service.Serv_num, Serv_type = service.Serv_type,
            Build_ID = user.Build_ID, Build = build?.Build, Mangment_Request = management?.Mangment_Request,
            DOC_Type = 1, OP_ID = 1, state = 0, ndate = DateOnly.FromDateTime(sentAt), rendate = sentAt, MaintenanceCosts_State = false };
        db.T_Notif_Action_Users.Add(request);
        await db.SaveChangesAsync(token);
        db.T_Notif_Actions.Add(new T_Notif_Action { DOC_ID = request.ID, DOC_Number = request.ID.ToString(),
            User_ID = ownerId, User_Name = user.User_Name, Description = request.Description,
            Serv_num = service.Serv_num, Serv_type = service.Serv_type, Build_ID = user.Build_ID,
            Build = build?.Build, Mangment_Request = management?.Mangment_Request,
            DOC_Type = 1, OP_ID = 1, Branch_ID = 1, state = 0, ndate = sentAt, rendate = sentAt, MaintenanceCosts_State = false });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        return request.ID;
    }
    #endregion

    #region بحث خاص بالموظف في الطلبات التي لا تزال قابلة للتعديل
    public async Task<List<T_Notif_Action_User>> SearchAsync(long owner, DateOnly from, DateOnly to, CancellationToken token = default)
    {
        if (from > to) throw new InvalidOperationException("بداية الفترة يجب ألا تتجاوز نهايتها.");
        await using var db = Create();
        return await db.T_Notif_Action_Users.AsNoTracking().Where(x => x.User_ID == owner && x.OP_ID == 1
            && (x.MaintenanceCosts_No == null || x.MaintenanceCosts_No <= 0) && x.ndate >= from && x.ndate <= to)
            .OrderByDescending(x => x.ID).ToListAsync(token);
    }
    #endregion
}
