using Maintenance_request.data;
using Maintenance_request.database_cotext;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Maintenance_request.Registration;

// مدخلات التسجيل؛ يبقى ترميز كلمة المرور متوافقًا مع تسجيل الدخول الحالي.
public sealed record RegistrationDraft(string Name, string Password, string Confirmation, string ComputerId, int ManagementId, short BuildingId);

public sealed class RegistrationStore(Func<Cont_maintContext> createContext)
{
    #region تحميل الخيارات والتحقق والحفظ
    public async Task<(List<T_Mangment_Request> Managements, List<T_Build> Buildings)> LoadAsync()
    {
        await using var db = createContext();
        return (await db.T_Mangment_Requests.AsNoTracking().OrderBy(x => x.Mangment_Request).ToListAsync(),
            await db.T_Builds.AsNoTracking().OrderBy(x => x.Build).ToListAsync());
    }
    public static string? PasswordError(string password, string confirmation)
    {
        if (string.IsNullOrEmpty(password)) return "أدخل كلمة المرور.";
        if (password.Length < 3) return "أقل طول لكلمة المرور ثلاثة أرقام أو أحرف.";
        if (string.IsNullOrEmpty(confirmation)) return "أدخل تأكيد كلمة المرور.";
        if (password != confirmation) return "كلمة المرور غير مطابقة لتأكيد كلمة المرور.";
        // حد عمود PassWord هو12 بعد الترميز، وليس12 حرفًا من كلمة المرور الأصلية.
        if (App.encrypt(password).Length > 12) return "كلمة المرور أطول مما تسمح به قاعدة البيانات بعد الترميز؛ استخدم كلمة أقصر.";
        return null;
    }
    public async Task<T_Maintenance_request_User> RegisterAsync(RegistrationDraft draft)
    {
        string name = draft.Name.Trim();
        if (name.Length is 0 or > 100) throw new ArgumentException("أدخل اسم المستخدم بما لا يتجاوز100 حرف.");
        if (PasswordError(draft.Password, draft.Confirmation) is string error) throw new ArgumentException(error);
        if (string.IsNullOrWhiteSpace(draft.ComputerId) || draft.ComputerId.Length > 50) throw new ArgumentException("تعذرت قراءة معرّف الجهاز بصورة صحيحة.");
        await using var db = createContext();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // المساواة حرفية؛ علامات % و_ في الاسم ليست رموز بحث Like.
        if (await db.T_Maintenance_request_Users.AnyAsync(x => x.PC_ID == draft.ComputerId && x.User_Name == name)) throw new DuplicateRegistrationException();
        if (!await db.T_Mangment_Requests.AnyAsync(x => x.Mangment_Request_ID == draft.ManagementId)) throw new ArgumentException("الإدارة المختارة غير موجودة؛ أعد تحميل القائمة.");
        if (!await db.T_Builds.AnyAsync(x => x.Build_ID == draft.BuildingId)) throw new ArgumentException("المبنى المختار غير موجود؛ أعد تحميل القائمة.");
        var user = new T_Maintenance_request_User
        {
            User_Name = name, PassWord = App.encrypt(draft.Password), PC_ID = draft.ComputerId,
            Mangment_Request_ID = draft.ManagementId, Build_ID = draft.BuildingId,
            State = true, ISsound = true, Sound_CHoise = 2
        };
        db.T_Maintenance_request_Users.Add(user);
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return user;
    }
    #endregion
}

// التكرار خطأ إدخال يعرض على الاسم، وليس رسالة تشغيل عامة.
public sealed class DuplicateRegistrationException : InvalidOperationException
{
    public DuplicateRegistrationException() : base("اسم المستخدم مسجل بالفعل على هذا الجهاز.") { }
}
