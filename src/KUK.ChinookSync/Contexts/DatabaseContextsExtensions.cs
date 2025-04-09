using KUK.Common;
using KUK.Common.Contexts;
using Microsoft.EntityFrameworkCore;

namespace KUK.ChinookSync.Contexts
{
    public static class DatabaseContextsExtensions
    {
        public static IServiceCollection AddDatabaseContexts(
            this IServiceCollection services,
            AppSettingsConfig config,
            ServiceLifetime contextLifetime)
        {
            services.AddDbContext<Chinook1DataChangesContext>(options =>
                options.UseMySQL(config.OldDatabaseConnectionString),
                contextLifetime);

            services.Add(new ServiceDescriptor(typeof(IChinook1DataChangesContext),
                provider => provider.GetService<Chinook1DataChangesContext>(), contextLifetime));
                
            services.Add(new ServiceDescriptor(typeof(IOldDataChangesContext),
                provider => provider.GetService<Chinook1DataChangesContext>(), contextLifetime));

            services.AddDbContext<Chinook1RootContext>(options =>
                options.UseMySQL(config.RootOldDatabaseConnectionString),
                contextLifetime);

            services.Add(new ServiceDescriptor(typeof(IChinook1RootContext),
                provider => provider.GetService<Chinook1RootContext>(), contextLifetime));

            services.AddDbContext<Chinook2Context>(options =>
                options.UseNpgsql(config.NewDatabaseConnectionString,
                    x => x.MigrationsAssembly("KUK.ChinookSync")),
                contextLifetime);

            services.Add(new ServiceDescriptor(typeof(IChinook2Context),
                provider => provider.GetService<Chinook2Context>(), contextLifetime));

            //services.Add(new ServiceDescriptor(typeof(NewDbContext),
            //    provider => provider.GetService<Chinook2Context>(), contextLifetime));

            services.AddDbContext<NewDbContext>(options =>
                options.UseNpgsql(config.NewDatabaseConnectionString,
                    x => x.MigrationsAssembly("KUK.ChinookSync")),
                contextLifetime);

            return services;
        }
    }
}
