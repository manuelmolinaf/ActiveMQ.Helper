using ActiveMQ.Helper.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ActiveMQ.Helper.Hosting;

internal sealed class AmqpHostedService( IAmqpConnectionManager connectionManager, ILogger<AmqpHostedService> logger) : IHostedService
{
    private readonly IAmqpConnectionManager _connectionManager = connectionManager;
    private readonly ILogger<AmqpHostedService> _logger = logger;

    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AMQP hosted service starting");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _backgroundTask = Task.Run(async () =>
        {
            await _connectionManager.StartAsync(_cts.Token);

            try
            {
                await Task.Delay(Timeout.Infinite, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AMQP hosted service stopping");

        if (_cts != null)
        {
            _cts.Cancel();
        }

        if (_backgroundTask != null)
        {
            await Task.WhenAny(_backgroundTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        await _connectionManager.StopAsync();
    }
}
