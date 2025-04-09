using KUK.ChinookSync.Models.NewSchema;
using KUK.ChinookSync.Models.NewSchema.Mapping;
using KUK.ChinookSync.Models.NewSchema.Outbox;
using KUK.Common.Contexts;
using KUK.Common.MigrationLogic;
using KUK.Common.Models.Outbox;
using Microsoft.EntityFrameworkCore;

namespace KUK.ChinookSync.Contexts
{
    public class Chinook2Context : NewDbContext, IChinook2Context
    {
        public Chinook2Context(
            DbContextOptions<NewDbContext> options)
            : base(options)
        {
        }

        public DbSet<Address> Addresses { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceLine> InvoiceLines { get; set; }

        public DbSet<CustomerOutbox> CustomerOutbox { get; set; }
        public DbSet<InvoiceOutbox> InvoiceOutbox { get; set; }
        public DbSet<InvoiceLineOutbox> InvoiceLineOutbox { get; set; }
        public DbSet<AddressOutbox> AddressOutbox { get; set; }

        public DbSet<AddressMapping> AddressMappings { get; set; }
        public DbSet<CustomerMapping> CustomerMappings { get; set; }
        public DbSet<InvoiceMapping> InvoiceMappings { get; set; }
        public DbSet<InvoiceLineMapping> InvoiceLineMappings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("public"); // db_chinook2

            modelBuilder.Entity<BaseOutbox>().ToTable((string)null);

            modelBuilder.Entity<DataMigration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MigrationName).IsRequired();
                entity.Property(e => e.AppliedOn).IsRequired();
            });

            modelBuilder.Entity<DataMigration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MigrationName).IsRequired();
                entity.Property(e => e.AppliedOn).IsRequired();
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.CustomerId);
                entity.HasOne(e => e.Address)
                      .WithMany(a => a.Customers)
                      .HasForeignKey(e => e.AddressId)
                      .OnDelete(DeleteBehavior.Restrict); // Prevents deletion if used in Customers
            });

            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.InvoiceId);
                entity.HasOne(e => e.BillingAddress)
                      .WithMany(a => a.Invoices)
                      .HasForeignKey(e => e.BillingAddressId)
                      .OnDelete(DeleteBehavior.Restrict); // Prevents deletion if used in Invoices
            });

            modelBuilder.Entity<InvoiceLine>(entity =>
            {
                entity.HasKey(e => e.InvoiceLineId);
                entity.Property(e => e.UnitPrice).IsRequired();
                entity.Property(e => e.Quantity).IsRequired();

                entity.HasOne(e => e.Invoice)
                      .WithMany(i => i.InvoiceLines)
                      .HasForeignKey(e => e.InvoiceId)
                      .OnDelete(DeleteBehavior.Cascade); // Deleting Invoice deletes related InvoiceLines
            });

            modelBuilder.Entity<Address>(entity =>
            {
                entity.HasKey(e => e.AddressId);
                entity.Property(e => e.Street).IsRequired();
                entity.Property(e => e.City).IsRequired();
                entity.Property(e => e.Country).IsRequired();
                entity.Property(e => e.PostalCode).IsRequired(false);

                entity.HasMany(e => e.Customers)
                      .WithOne(c => c.Address)
                      .HasForeignKey(c => c.AddressId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Invoices)
                      .WithOne(i => i.BillingAddress)
                      .HasForeignKey(i => i.BillingAddressId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BaseOutbox>().HasKey(e => e.EventId); // Key for BaseOutbox
            modelBuilder.Entity<BaseOutbox>()
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<CustomerOutbox>().HasBaseType<BaseOutbox>();
            modelBuilder.Entity<InvoiceOutbox>().HasBaseType<BaseOutbox>();
            modelBuilder.Entity<InvoiceLineOutbox>().HasBaseType<BaseOutbox>();
            modelBuilder.Entity<AddressOutbox>().HasBaseType<BaseOutbox>();

            modelBuilder.Entity<AddressMapping>()
                .HasIndex(am => new
                {
                    am.AddressCompositeKeyStreet,
                    am.AddressCompositeKeyCity,
                    am.AddressCompositeKeyState,
                    am.AddressCompositeKeyCountry,
                    am.AddressCompositeKeyPostalCode
                })
                .IsUnique()
                .HasDatabaseName("IX_Unique_CompositeAddressKey");
        }
    }
}
