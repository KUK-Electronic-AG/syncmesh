using KUK.Common.MigrationLogic;
using KUK.Common.Models.Outbox;
using Microsoft.EntityFrameworkCore;

namespace KUK.Common.Contexts
{
    public class NewDbContext : DbContext
    {
        public NewDbContext(DbContextOptions<NewDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// This will store information about which data migrations have been applied, similar to built-in
        /// Entity Framework migrations table
        /// </summary>
        public DbSet<DataMigration> DataMigrations { get; set; }

        public DbSet<ProcessedMessagesNew> ProcessedMessages { get; set; }
    }
}
