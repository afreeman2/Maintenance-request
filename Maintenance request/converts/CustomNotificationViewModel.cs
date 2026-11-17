using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Maintenance_request.converts
{
    [POCOViewModel]
    public class CustomNotificationViewModel
    {
        public virtual string Caption { get; set; }
        public virtual string Content { get; set; }
    }
}
