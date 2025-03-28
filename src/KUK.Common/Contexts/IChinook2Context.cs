﻿using Microsoft.EntityFrameworkCore;
using KUK.Common.ModelsNewSchema;
using KUK.Common.MigrationLogic;

namespace KUK.Common.Contexts
{
    public interface IChinook2Context
    {
        DbSet<DataMigration> DataMigrations { get; set; }
        DbSet<Address> Addresses { get; set; }
        DbSet<Customer> Customers { get; set; }
        DbSet<Invoice> Invoices { get; set; }
        DbSet<InvoiceLine> InvoiceLines { get; set; }
        int SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
