using Microsoft.EntityFrameworkCore;

namespace KUK.Common.Contexts
{
    public class Chinook1RootContext : Chinook1Context, IChinook1RootContext
    {
        public Chinook1RootContext(DbContextOptions<Chinook1RootContext> options) : base(options) { }
    }
}
