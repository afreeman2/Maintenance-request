using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Maintenance_request.Support;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Maintenance_request.Registration;

public sealed class AccountStore(Func<Cont_maintContext>? factory = null)
{
    #region تحميل حسابات الجهاز والتحقق من كلمة المرور الحالية من القاعدة
    private Cont_maintContext Create() => factory?.Invoke() ?? ClientContext.Create();
    public async Task<List<T_Maintenance_request_User>> UsersAsync(string computer, CancellationToken token = default)
    {
        await using var db = Create();
        return await db.T_Maintenance_request_Users.AsNoTracking().Where(x => x.PC_ID == computer).OrderBy(x => x.User_Name).ToListAsync(token);
    }
    public async Task<T_Maintenance_request_User?> AuthenticateAsync(long id, string computer, string password, CancellationToken token = default)
    {
        await using var db = Create();
        var user = await db.T_Maintenance_request_Users.AsNoTracking().Include(x => x.Build).Include(x => x.Mangment_Request)
            .SingleOrDefaultAsync(x => x.User_ID == id && x.PC_ID == computer, token);
        // يبقى ترميز الحسابات القديمة كما هو؛ لا نعتمد كلمة مرور مخزنة في نسخة قائمة الدخول.
        return user is not null && App.decrypt(user.PassWord) == password ? user : null;
    }
    #endregion

    #region تغيير كلمة المرور بعد التحقق من القديمة وحد العمود دون تعديل المستخدم قبل الحفظ
    public async Task<string> ChangePasswordAsync(long owner, string current, string password, string confirmation)
    {
        if (RegistrationStore.PasswordError(password, confirmation) is string error) throw new ArgumentException(error);
        await using var db = Create();
        var user = await db.T_Maintenance_request_Users.SingleOrDefaultAsync(x => x.User_ID == owner)
            ?? throw new InvalidOperationException("تعذر العثور على حساب المستخدم الحالي.");
        if (App.decrypt(user.PassWord) != current) throw new ArgumentException("كلمة المرور الحالية غير صحيحة.");
        string encoded = App.encrypt(password); user.PassWord = encoded;
        await db.SaveChangesAsync(); return encoded;
    }
    #endregion

    #region حفظ بيانات الحساب وخيارات الصوت المطابقة للشاشة الأصلية
    public async Task<T_Maintenance_request_User> SaveProfileAsync(long owner, string name, int managementId, short buildingId, bool soundEnabled, short soundChoice)
    {
        name = name.Trim();
        if (name.Length is 0 or > 100) throw new ArgumentException("أدخل اسم المستخدم بما لا يتجاوز100 حرف.");
        if (soundChoice is < 1 or > 3) throw new ArgumentException("اختر إحدى نغمات التنبيه الثلاث.");
        await using var db = Create();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await db.T_Maintenance_request_Users.SingleOrDefaultAsync(x => x.User_ID == owner)
            ?? throw new InvalidOperationException("تعذر العثور على الحساب الحالي.");
        if (await db.T_Maintenance_request_Users.AnyAsync(x => x.User_ID != owner && x.PC_ID == user.PC_ID && x.User_Name == name))
            throw new ArgumentException("اسم المستخدم مسجل بالفعل على هذا الجهاز.");
        var management = await db.T_Mangment_Requests.SingleOrDefaultAsync(x => x.Mangment_Request_ID == managementId)
            ?? throw new ArgumentException("الإدارة المختارة غير موجودة؛ حدّث الخيارات.");
        var build = await db.T_Builds.SingleOrDefaultAsync(x => x.Build_ID == buildingId)
            ?? throw new ArgumentException("المبنى المختار غير موجود؛ حدّث الخيارات.");
        user.User_Name = name; user.Mangment_Request_ID = managementId; user.Build_ID = buildingId;
        user.ISsound = soundEnabled; user.Sound_CHoise = soundChoice;
        user.Mangment_Request = management; user.Build = build;
        await db.SaveChangesAsync(); await tx.CommitAsync(); return user;
    }
    #endregion
}
