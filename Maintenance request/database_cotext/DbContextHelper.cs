using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Maintenance_request.database_cotext
{
    internal class DbContextHelper
    {
        public static DbContextOptions<Cont_maintContext>? Options { get; private set; }

        public static void InitOptions(string connectionString)
        {
            var builder = new DbContextOptionsBuilder<Cont_maintContext>();
            builder.UseSqlServer(connectionString);

            Options = builder.Options;
        }
    }
}
