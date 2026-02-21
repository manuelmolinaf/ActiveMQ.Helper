using ActiveMQ.Helper.Abstractions;
using ActiveMQ.Helper.Hosting;
using ActiveMQ.Helper.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace ActiveMQ.Helper.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAmqpMessaging(
        this IServiceCollection services,
        string connectionString)
    {
        services.TryAddSingleton<AsyncRetryPolicy>(sp =>
    Policy
        .Handle<Exception>()
        .WaitAndRetryForeverAsync(
            retryAttempt => TimeSpan.FromSeconds(
                Math.Min(30, retryAttempt)),
            (ex, ts) =>
            {
                var logger = sp
                    .GetRequiredService<ILogger<AmqpConnectionManager>>();

                logger.LogWarning(
                    ex,
                    "Retrying AMQP connection in {Delay}",
                    ts);
            }));

        services.AddSingleton<IAmqpConnectionManager>(sp =>
            new AmqpConnectionManager(
                connectionString,
                sp.GetRequiredService<AsyncRetryPolicy>(),
                sp.GetRequiredService<ILogger<AmqpConnectionManager>>()));

        services.AddHostedService<AmqpHostedService>();

        services.AddSingleton<IAmqpPublisher, AmqpPublisher>();
        services.AddSingleton<IAmqpConsumer, AmqpConsumer>();

        return services;
    }
}
