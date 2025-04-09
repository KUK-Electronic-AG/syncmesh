using KUK.Common.Models.OldSchema;
using Microsoft.EntityFrameworkCore;

namespace KUK.Common.Contexts
{
    public class OldDbContext : DbContext, IOldDataChangesContext, IOldRootContext
    {
        public OldDbContext(DbContextOptions options) : base(options)
        {
            
        }

        public DbSet<ProcessedMessagesOld> ProcessedMessages { get; set; }
    }
}
