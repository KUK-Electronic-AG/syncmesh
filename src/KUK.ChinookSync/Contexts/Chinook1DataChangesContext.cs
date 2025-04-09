using KUK.Common.Contexts;
using Microsoft.EntityFrameworkCore;

namespace KUK.ChinookSync.Contexts
{
    public class Chinook1DataChangesContext : Chinook1Context, IChinook1DataChangesContext, IOldDataChangesContext
    {
        public Chinook1DataChangesContext(DbContextOptions<Chinook1DataChangesContext> options) : base(options) { }
    }
}
