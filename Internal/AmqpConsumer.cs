using Amqp;
using Amqp.Framing;
using Amqp.Types;
using ActiveMQ.Helper.Abstractions;
using Microsoft.Extensions.Logging;

namespace ActiveMQ.Helper.Internal;

internal sealed class AmqpConsumer(IAmqpConnectionManager connectionManager, ILogger<AmqpConsumer> logger) : IAmqpConsumer, IAsyncDisposable
{
    private readonly IAmqpConnectionManager _connectionManager = connectionManager;
    private readonly ILogger<AmqpConsumer> _logger = logger;

    private readonly SemaphoreSlim _sync = new(1, 1);

    private Task? _consumerTask;
    private CancellationTokenSource? _cts;

    private ReceiverLink? _receiver;

    private int _maxConcurrency = 1;
    private SemaphoreSlim? _workers;
    private readonly List<Task> _inFlight = [];
    private readonly object _inFlightLock = new();

    public Task StartAsync<T>(string address, string queue, AmqpAddressType addressType, Func<T, CancellationToken, Task> handler, CancellationToken ct = default)
    {
        return StartAsync(
            address,
            queue,
            addressType,
            async (message, token) =>
            {
                var obj = message.GetBody<T>();
                await handler(obj, token);
            },
            ct);
    }

    public Task StartParallelAsync<T>(string address, string queue, AmqpAddressType addressType, int maxConcurrency, Func<T, CancellationToken, Task> handler, CancellationToken ct = default)
    {
        return StartParallelAsync(
            address,
            queue,
            addressType,
            maxConcurrency,
            async (message, token) =>
            {
                var obj = message.GetBody<T>();
                await handler(obj, token);
            },
            ct);
    }



    public async Task StartAsync(string address, string queue, AmqpAddressType addressType, Func<Message, CancellationToken, Task> handler, CancellationToken ct = default)
    {
        await _sync.WaitAsync(ct);
        try
        {
            if (_consumerTask != null)
                throw new InvalidOperationException("Consumer already started.");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _consumerTask = Task.Run(
                () => SupervisorLoopAsync(address, queue, addressType, handler, _cts.Token),
                CancellationToken.None);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task StartParallelAsync(string address, string queue, AmqpAddressType addressType, int maxConcurrency, Func<Message, CancellationToken, Task> handler, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);

        await _sync.WaitAsync(ct);
        try
        {
            if (_consumerTask != null)
                throw new InvalidOperationException("Consumer already started.");

            _maxConcurrency = maxConcurrency;
            _workers = new SemaphoreSlim(maxConcurrency);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _consumerTask = Task.Run(
                () => SupervisorLoopAsync(address, queue, addressType, handler, _cts.Token),
                CancellationToken.None);
        }
        finally
        {
            _sync.Release();
        }
    }



    // OUTER SUPERVISOR LOOP (self-healing)
    private async Task SupervisorLoopAsync( string address, string queue, AmqpAddressType addressType, Func<Message, CancellationToken, Task> handler, CancellationToken ct)
    {
        var destination = $"{address}::{queue}";

        _logger.LogInformation("AMQP supervisor started for {Destination}", destination);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await EnsureReceiverAsync(destination, addressType, ct);

                if (_maxConcurrency <= 1)
                    await ConsumeLoopAsync(destination, handler, ct);
                else
                    await ConsumeLoopParallelAsync(destination, handler, _maxConcurrency, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                    break;

                _logger.LogWarning(
                    ex,
                    "AMQP consumer crashed for {Destination}. Reconnecting in 5s...",
                    destination);

                await SafeDelay(TimeSpan.FromSeconds(5), ct);
            }
        }

        _logger.LogInformation("AMQP supervisor stopped for {Destination}", destination);
    }

    // Ensures connection + session + receiver exist
    private async Task EnsureReceiverAsync( string destination, AmqpAddressType addressType, CancellationToken ct)
    {
        if (_receiver is { IsClosed: false })
            return;

        _logger.LogInformation("Creating receiver for {Destination}", destination);

        await _connectionManager.StartAsync(ct);
        var session = await _connectionManager.GetSessionAsync(ct);

        var source = new Source
        {
            Address = destination,
            Capabilities = addressType == AmqpAddressType.Topic
                ? [new Symbol("topic")]
                : [new Symbol("queue")]
        };

        _receiver = new ReceiverLink(
                session,
                $"receiver-{destination.Replace("::", "-")}-{Guid.NewGuid()}",
                source,
                onAttached: null);
    }

    // 🔥 INNER LOOP
    private async Task ConsumeLoopAsync(string destination, Func<Message, CancellationToken, Task> handler, CancellationToken ct)
    {
        _logger.LogInformation("Consume loop running for {Destination}", destination);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var message = await _receiver!.ReceiveAsync(TimeSpan.FromSeconds(5));

                if (message == null)
                    continue;

                try
                {
                    await handler(message, ct);
                    _receiver.Accept(message);
                }
                catch (Exception handlerEx)
                {
                    _logger.LogError(
                        handlerEx,
                        "Handler failed for message from {Destination}",
                        destination);

                    _receiver.Reject(message);
                }
            }
            catch (AmqpException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Transport error in consumer for {Destination}. Rebuilding receiver...",
                    destination);

                await RebuildReceiverAsync();
                return; // exit inner loop → supervisor reconnects
            }
        }
    }

    private async Task ConsumeLoopParallelAsync(string destination, Func<Message, CancellationToken, Task> handler, int maxConcurrency, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);

        _logger.LogInformation(
            "Parallel consume loop running for {Destination} with concurrency {Concurrency}",
            destination,
            maxConcurrency);

        // Align link credit with concurrency
        _receiver!.SetCredit(maxConcurrency);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var message = await _receiver.ReceiveAsync(TimeSpan.FromSeconds(5));
                if (message == null)
                    continue;

                await _workers!.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await handler(message, ct);
                        _receiver.Accept(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Handler failed for message from {Destination}",
                            destination);

                        _receiver.Reject(message);
                    }
                    finally
                    {
                        _workers.Release();
                    }
                }, CancellationToken.None);

                TrackInFlight(task);
            }
            catch (AmqpException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Transport error in consumer for {Destination}. Rebuilding receiver...",
                    destination);

                await RebuildReceiverAsync();
                return;
            }
        }

        await WaitForInFlightAsync();
    }

    private void TrackInFlight(Task task)
    {
        lock (_inFlightLock)
        {
            _inFlight.Add(task);
        }

        task.ContinueWith(t =>
        {
            lock (_inFlightLock)
            {
                _inFlight.Remove(t);
            }
        }, TaskScheduler.Default);
    }

    private async Task WaitForInFlightAsync()
    {
        Task[] tasks;
        lock (_inFlightLock)
        {
            tasks = _inFlight.ToArray();
        }

        if (tasks.Length > 0)
            await Task.WhenAll(tasks);
    }

    private async Task RebuildReceiverAsync()
    {
        try
        {
            _receiver?.Close();
        }
        catch { }

        _receiver = null;

        // Let supervisor recreate everything
        await Task.CompletedTask;
    }

    private static async Task SafeDelay(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException) { }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await _sync.WaitAsync(ct);
        try
        {
            _cts?.Cancel();

            if (_consumerTask != null)
                await _consumerTask.WaitAsync(ct);

            try
            {
                await WaitForInFlightAsync();
                _receiver?.Close();
            }
            catch { }

            _workers?.Dispose();
            _workers = null;
            _maxConcurrency = 1;

            _receiver = null;
            _consumerTask = null;

            _cts?.Dispose();
            _cts = null;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _sync.Dispose();
    }
}