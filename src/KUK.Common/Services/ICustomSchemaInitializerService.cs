using KUK.Common.Contexts;

namespace KUK.Common.Services
{
    public interface ICustomSchemaInitializerService
    {
        void AddTestValueColumn(OldDbContext context, int precision);
    }
}
