using Amqp;
using Amqp.Framing;
using Amqp.Serialization;
using Amqp.Types;
using ActiveMQ.Helper.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ActiveMQ.Helper.Internal;

internal sealed class AmqpPublisher(
    IAmqpConnectionManager connectionManager,
    ILogger<AmqpPublisher> logger)
    : IAmqpPublisher, IAsyncDisposable
{
    private readonly IAmqpConnectionManager _connectionManager = connectionManager;
    private readonly ILogger<AmqpPublisher> _logger = logger;

    private readonly ConcurrentDictionary<string, SenderLink> _senders = new();
    private readonly SemaphoreSlim _sync = new(1, 1);


    public Task SendAsync<T>(T payload, string address, string queue, AmqpAddressType addressType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        ValidateAmqpContract<T>();

        var message = new Message
        {
            BodySection = new AmqpValue<T>(payload),           
        };

       
        return SendAsync(message, address, queue, addressType, ct);
    }


    public async Task SendAsync(Message message, string address, string queue, AmqpAddressType addressType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address cannot be empty.", nameof(address));

        if (string.IsNullOrWhiteSpace(queue))
            throw new ArgumentException("Queue cannot be empty.", nameof(queue));

        var destination = $"{address}::{queue}";

        var sender = await GetOrCreateSenderAsync(destination, addressType, ct);

        try
        {
            await sender.SendAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Send failed. Resetting sender for {Destination}", destination);

            ResetSender(destination);
            throw;
        }
    }



    private async Task<SenderLink> GetOrCreateSenderAsync(string destination,AmqpAddressType addressType, CancellationToken ct)
    {
        if (_senders.TryGetValue(destination, out var existing))
        {
            if (!existing.IsClosed)
                return existing;

            ResetSender(destination);
        }

        await _sync.WaitAsync(ct);
        try
        {
            if (_senders.TryGetValue(destination, out existing))
            {
                if (!existing.IsClosed)
                    return existing;

                ResetSender(destination);
            }

            var session = await _connectionManager.GetSessionAsync(ct);

            var target = new Target
            {
                Address = destination,
                Capabilities = addressType == AmqpAddressType.Topic
                    ? [new Symbol("topic")]
                    : [new Symbol("queue")]
            };

            var sender = new SenderLink(
                session,
                $"sender-{destination.Replace("::", "-")}",
                target,
                onAttached: null);

            _senders[destination] = sender;

            _logger.LogInformation(
                "Created AMQP sender for {Destination}", destination);

            return sender;
        }
        finally
        {
            _sync.Release();
        }
    }

    private void ResetSender(string destination)
    {
        if (_senders.TryRemove(destination, out var sender))
        {
            try
            {
                sender.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error closing sender for {Destination}", destination);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            try
            {
                sender.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing sender");
            }
        }

        _senders.Clear();
        _sync.Dispose();
    }

    private static void ValidateAmqpContract<T>()
    {
        var type = typeof(T);

        // Allow primitives automatically
        if (type.IsPrimitive ||
            type == typeof(string) ||
            type == typeof(Guid) ||
            type == typeof(DateTime) ||
            type == typeof(decimal))
        {
            return;
        }

        var hasContract = type.GetCustomAttributes(typeof(AmqpContractAttribute), inherit: true).Any();

        if (!hasContract)
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' must be decorated with [AmqpContract] to be sent over AMQP.");
        }
    }
}
