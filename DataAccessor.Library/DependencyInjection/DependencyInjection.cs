using System;
using System.Collections.Generic;
using System.Linq;
using DataAccessor.Library.Core;
using DataAccessor.Library.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccessor.Library.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, Action<DataAccessOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DataAccessOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton(options.Http);
        services.AddSingleton<IReadOnlyList<DatabaseConnectionOptions>>(options.Databases);

        services.AddHttpClient<HttpDataAccess>(client =>
        {
            if (!string.IsNullOrWhiteSpace(options.Http.BaseUrl))
            {
                client.BaseAddress = new Uri(options.Http.BaseUrl, UriKind.Absolute);
            }

            if (options.Http.Authorization != null)
            {
                client.DefaultRequestHeaders.Authorization = options.Http.Authorization;
            }

            foreach (var header in options.Http.DefaultHeaders)
            {
                client.DefaultRequestHeaders.Remove(header.Key);
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        });

        services.AddSingleton<IDbConnectionFactory>(_ => new ConfigurableDbConnectionFactory(GetDefaultDatabase(options)));
        services.AddSingleton<IMultiServerConnectionFactory>(_ => new ConfigurableMultiServerConnectionFactory(options.Databases));
        services.AddSingleton<GenericDataAccess>();
        services.AddSingleton<CrossServerDataAccess>(provider => new CrossServerDataAccess(provider.GetRequiredService<DataAccessOptions>()));
        services.AddSingleton<CompositeDataAccess>(provider =>
            new CompositeDataAccess(
                provider.GetRequiredService<HttpDataAccess>(),
                provider.GetRequiredService<GenericDataAccess>(),
                provider.GetRequiredService<CrossServerDataAccess>()));
        services.AddSingleton<IMultiServerDataAccess, MultiDatabaseDataAccess>(provider =>
            new MultiDatabaseDataAccess(provider.GetRequiredService<IMultiServerConnectionFactory>(), provider.GetRequiredService<DataAccessOptions>()));
        services.AddSingleton<MultiDatabaseDataAccess>(provider =>
            new MultiDatabaseDataAccess(provider.GetRequiredService<IMultiServerConnectionFactory>(), provider.GetRequiredService<DataAccessOptions>()));
        services.AddSingleton<DistributedTransactionManager>(provider =>
            new DistributedTransactionManager(provider.GetRequiredService<IMultiServerConnectionFactory>(), provider.GetRequiredService<DataAccessOptions>()));

        services.AddSingleton<IGenericDataAccess>(provider => options.DefaultSource switch
        {
            DataSourceType.Database => provider.GetRequiredService<GenericDataAccess>(),
            DataSourceType.Http => provider.GetRequiredService<HttpDataAccess>(),
            _ => provider.GetRequiredService<CompositeDataAccess>()
        });

        return services;
    }

    private static DatabaseConnectionOptions GetDefaultDatabase(DataAccessOptions options)
    {
        var defaultDatabase = options.Databases.FirstOrDefault(database => string.Equals(database.Name, "Default", StringComparison.OrdinalIgnoreCase))
            ?? options.Databases.FirstOrDefault();

        if (defaultDatabase == null)
        {
            throw new InvalidOperationException("At least one database connection must be configured.");
        }

        return defaultDatabase;
    }
}
