using Microsoft.EntityFrameworkCore;

namespace KUK.Common.Contexts
{
    public class Chinook1DataChangesContext : Chinook1Context, IChinook1DataChangesContext
    {
        public Chinook1DataChangesContext(DbContextOptions<Chinook1DataChangesContext> options) : base(options) { }
    }
}
