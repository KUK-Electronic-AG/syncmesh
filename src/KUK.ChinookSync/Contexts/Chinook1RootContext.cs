using KUK.Common.Contexts;
using Microsoft.EntityFrameworkCore;

namespace KUK.ChinookSync.Contexts
{
    public class Chinook1RootContext : Chinook1Context, IChinook1RootContext, IOldRootContext
    {
        public Chinook1RootContext(DbContextOptions<Chinook1RootContext> options) : base(options) { }
    }
}
