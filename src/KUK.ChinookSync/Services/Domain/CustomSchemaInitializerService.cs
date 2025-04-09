using KUK.ChinookSync.Services.Domain.Interfaces;
using KUK.Common.Contexts;
using KUK.Common.Services;
using Microsoft.EntityFrameworkCore;

namespace KUK.ChinookSync.Services.Domain
{
    public class CustomSchemaInitializerService: ICustomSchemaInitializerService
    {
        public void AddTestValueColumn(OldDbContext context, int precision)
        {
            // Always add precision as a suffix to the column name
            string columnName = $"TestValue{precision}";

            // Add TestValue column with specified precision
            var sql = $"ALTER TABLE db_chinook1.Invoice ADD COLUMN {columnName} DECIMAL(12,{precision});";
            ((DbContext)context).Database.ExecuteSqlRaw(sql);

            // Update all the records to add random TestValue with specified precision
            var updateSql = $@"
                UPDATE db_chinook1.Invoice
                SET {columnName} = ROUND(RAND() * POWER(10, {precision}), {precision});";
            ((DbContext)context).Database.ExecuteSqlRaw(updateSql);
        }
    }
}
