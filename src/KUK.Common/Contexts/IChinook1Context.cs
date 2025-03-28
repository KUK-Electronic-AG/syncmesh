using Microsoft.EntityFrameworkCore;
using KUK.Common.ModelsOldSchema;

namespace KUK.Common.Contexts
{
    public interface IChinook1Context
    {
        DbSet<Customer> Customers { get; set; }
        DbSet<Invoice> Invoices { get; set; }
        DbSet<InvoiceLine> InvoiceLines { get; set; }
        int SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
