# ActiveMQ.Helper

Lightweight, production-ready AMQP infrastructure library built on
**AMQP.Net Lite**, designed for high‑throughput, resilient messaging
with ActiveMQ (or any AMQP 1.0 broker).

------------------------------------------------------------------------

## 🚀 Features

-   ✅ Connection management with automatic retry (Polly)
-   ✅ Cached sender links for high-throughput publishing
-   ✅ Graceful consumer loop with cancellation support
-   ✅ Automatic sender reset on failure
-   ✅ Thread-safe start/stop lifecycle
-   ✅ Clean dependency injection integration
-   ✅ Designed for Kubernetes & container environments

------------------------------------------------------------------------

## 📦 Installation

``` bash
dotnet add package ActiveMQ.Helper
```

------------------------------------------------------------------------

## 🔧 Registration

Register messaging services in `Program.cs`:

``` csharp
builder.Services.AddAmqpMessaging(
    connectionString: "amqp://user:password@broker:5672");
```

This registers:

-   `IAmqpConnectionManager`
-   `IAmqpPublisher`
-   `IAmqpConsumer`
-   Background hosted service
-   Default retry policy (if not already registered)

------------------------------------------------------------------------

## 📤 Publishing Messages

``` csharp
public class OrderService
{
    private readonly IAmqpPublisher _publisher;

    public OrderService(IAmqpPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task PublishAsync()
    {
        await _publisher.SendAsync(
            new AmqpValue("Hello World"),
            destination: "orders.queue",
            AmqpAddressType.Queue);
    }
}
```

### Publisher Behavior

-   Reuses sender links per destination
-   Automatically recreates sender on failure
-   Thread-safe
-   Optimized for high throughput

------------------------------------------------------------------------

## 📥 Consuming Messages

``` csharp
public class OrderConsumer
{
    private readonly IAmqpConsumer _consumer;

    public OrderConsumer(IAmqpConsumer consumer)
    {
        _consumer = consumer;
    }

    public async Task StartAsync()
    {
        await _consumer.StartAsync(
            source: "orders.queue",
            AmqpAddressType.Queue,
            async (message, ct) =>
            {
                var body = message.GetBody<string>();
                Console.WriteLine(body);
            });
    }
}
```

### Consumer Behavior

-   Timeout-based receive loop (no infinite blocking)
-   Observes cancellation properly
-   Accepts messages on success
-   Rejects messages on failure
-   Safe shutdown via `StopAsync()`

------------------------------------------------------------------------

## 🛡 Retry Policy

By default, the package registers an infinite retry policy with
exponential delay (capped).

If you want to override it:

``` csharp
services.AddSingleton<AsyncRetryPolicy>(
    Policy.Handle<Exception>()
          .WaitAndRetryForeverAsync(...));
```

------------------------------------------------------------------------

## 🧠 Design Principles

-   One sender per destination
-   Unique receiver links per consumer instance
-   No sender creation per message
-   Defensive error handling
-   Clean separation of infrastructure from application logic

------------------------------------------------------------------------

## 🏗 Production Recommendations

-   Monitor connection health
-   Tune broker prefetch for high-load scenarios
-   Use dead-letter queues for poison messages
-   Run consumers as hosted services in background workers

------------------------------------------------------------------------

## 📄 License

MIT License

------------------------------------------------------------------------

## 👤 Author

Banreservas Messaging Platform Team
