using KUK.Common.ModelsOldSchema;
using Microsoft.EntityFrameworkCore;

namespace KUK.Common.Contexts
{
    public class Chinook1Context : DbContext, IChinook1Context
    {
        public Chinook1Context(DbContextOptions options) : base(options) { }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceLine> InvoiceLines { get; set; }
        public DbSet<ProcessedMessagesOld> ProcessedMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // REMARK: If you switch from MySQL to Postgres, you would need to provide:
            // modelBuilder.HasDefaultSchema("db_chinook1");

            modelBuilder.Entity<Customer>().ToTable("Customer");
            modelBuilder.Entity<Invoice>().ToTable("Invoice");
            modelBuilder.Entity<InvoiceLine>().ToTable("InvoiceLine");
        }
    }
}
