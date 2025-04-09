using KUK.Common.Models.OldSchema;
using Microsoft.EntityFrameworkCore;

namespace KUK.Common.Contexts
{
    public interface IOldDataChangesContext
    {
        DbSet<ProcessedMessagesOld> ProcessedMessages { get; set; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
