using Amqp;
using ActiveMQ.Helper.Abstractions;
using Microsoft.Extensions.Logging;
using Polly.Retry;


namespace ActiveMQ.Helper.Internal;

internal sealed class AmqpConnectionManager( string connectionString, AsyncRetryPolicy retryPolicy, ILogger<AmqpConnectionManager> logger) : IAmqpConnectionManager, IAsyncDisposable
{
    private readonly string _connectionString = connectionString;
    private readonly AsyncRetryPolicy _retryPolicy = retryPolicy;
    private readonly ILogger<AmqpConnectionManager> _logger = logger;

    private readonly SemaphoreSlim _sync = new(1, 1);

    private Connection? _connection;
    private Session? _session;

    public async Task StartAsync(CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
    }

    public async Task<Session> GetSessionAsync(CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
        return _session!;
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_connection is { IsClosed: false } && _session is { IsClosed: false })
            return;

        await _sync.WaitAsync(ct);
        try
        {
            if (_connection is { IsClosed: false } && _session is { IsClosed: false })
                return;

            await _retryPolicy.ExecuteAsync(async token =>
            {
                token.ThrowIfCancellationRequested();

                _logger.LogInformation("Establishing AMQP connection");

                await CloseInternalAsync();

                _connection = new Connection(new Address(_connectionString));
                _session = new Session(_connection);

            }, ct);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task StopAsync()
    {
        await _sync.WaitAsync();
        try
        {
            await CloseInternalAsync();
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task CloseInternalAsync()
    {
        try
        {
            if (_session != null)
                await _session.CloseAsync();

            if (_connection != null)
                await _connection.CloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while closing AMQP resources");
        }
        finally
        {
            _session = null;
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _sync.Dispose();
    }
}
