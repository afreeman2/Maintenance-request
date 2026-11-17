using Maintenance_request.database_cotext;

namespace Maintenance_request.Support;

public static class ClientContext
{
    #region إنشاء سياق مستقل لكل عملية دون تكرار بناء الاتصال داخل الواجهات
    public static Cont_maintContext Create()
    {
        // يأخذ السياق الاتصال من الإعداد النشط عند بدء التطبيق.
        DbContextHelper.InitOptions(App.DatabaseConnectionString);
        return new Cont_maintContext(DbContextHelper.Options!);
    }
    #endregion
}
