using Amqp;

namespace ActiveMQ.Helper.Abstractions;


public interface IAmqpConnectionManager
{
    Task<Session> GetSessionAsync(CancellationToken ct);
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
}
