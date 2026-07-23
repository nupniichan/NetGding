using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetGding.Contracts.Options;
using NetGding.Contracts.Messaging;
using StackExchange.Redis;

namespace NetGding.Configurations.Bootstrap;

public static class MessagingServiceExtensions
{
    public static IServiceCollection AddRedisMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            var connectionString = string.IsNullOrWhiteSpace(options.ConnectionString) ? "localhost:6379" : options.ConnectionString;
            var config = ConfigurationOptions.Parse(connectionString);
            config.AbortOnConnectFail = false;
            config.ConnectRetry = 5;
            config.ConnectTimeout = 5000;
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddSingleton<IEventBus, RedisEventBus>();

        return services;
    }
}
