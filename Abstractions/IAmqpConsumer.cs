using Amqp;

namespace ActiveMQ.Helper.Abstractions
{
    /// <summary>
    /// Defines a consumer capable of receiving AMQP messages
    /// from a queue or topic using AMQP 1.0 semantics.
    /// </summary>
    public interface IAmqpConsumer
    {
        /// <summary>
        /// Starts consuming messages from the specified address and queue.
        /// </summary>
        /// <param name="address">
        /// The broker address/namespace (e.g., "br.orders").
        /// </param>
        /// <param name="queue">
        /// The queue or topic name within the address.
        /// </param>
        /// <param name="addressType">
        /// Specifies how the source should be treated by the broker.
        /// Use <see cref="AmqpAddressType.Queue"/> for point-to-point (ANYCAST)
        /// or <see cref="AmqpAddressType.Topic"/> for publish/subscribe (MULTICAST).
        /// </param>
        /// <param name="handler">
        /// A delegate invoked for each received <see cref="Message"/>.
        /// The message is accepted automatically if the handler completes successfully.
        /// </param>
        /// <param name="ct">
        /// A cancellation token used to stop message consumption.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task StartAsync(
            string address,
            string queue,
            AmqpAddressType addressType,
            Func<Message, CancellationToken, Task> handler,
            CancellationToken ct = default);

        /// <summary>
        /// Starts consuming messages and automatically deserializes
        /// the message body to the specified type.
        /// </summary>
        /// <typeparam name="T">
        /// The expected type of the message body.
        /// </typeparam>
        /// <param name="address">
        /// The broker address/namespace (e.g., "br.orders").
        /// </param>
        /// <param name="queue">
        /// The queue or topic name within the address.
        /// </param>
        /// <param name="addressType">
        /// Specifies how the source should be treated by the broker.
        /// </param>
        /// <param name="handler">
        /// A delegate invoked for each received deserialized message.
        /// The message is accepted automatically if the handler completes successfully.
        /// </param>
        /// <param name="ct">
        /// A cancellation token used to stop message consumption.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task StartAsync<T>(
            string address,
            string queue,
            AmqpAddressType addressType,
            Func<T, CancellationToken, Task> handler,
            CancellationToken ct = default);

        /// <summary>
        /// Starts consuming messages in parallel with a specified maximum concurrency.
        /// </summary>
        /// <param name="address">
        /// The broker address/namespace.
        /// </param>
        /// <param name="queue">
        /// The queue or topic name within the address.
        /// </param>
        /// <param name="addressType">
        /// Specifies how the source should be treated by the broker.
        /// </param>
        /// <param name="maxConcurrency">
        /// The maximum number of messages to process concurrently.
        /// Must be greater than zero.
        /// </param>
        /// <param name="handler">
        /// A delegate invoked for each received message.
        /// </param>
        /// <param name="ct">
        /// A cancellation token used to stop message consumption.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task StartParallelAsync(
            string address,
            string queue,
            AmqpAddressType addressType,
            int maxConcurrency,
            Func<Message, CancellationToken, Task> handler,
            CancellationToken ct = default);

        /// <summary>
        /// Starts consuming messages in parallel with a specified maximum concurrency
        /// and automatically deserializes the message body to the specified type.
        /// </summary>
        /// <typeparam name="T">
        /// The expected type of the message body.
        /// </typeparam>
        /// <param name="address">
        /// The broker address/namespace.
        /// </param>
        /// <param name="queue">
        /// The queue or topic name within the address.
        /// </param>
        /// <param name="addressType">
        /// Specifies how the source should be treated by the broker.
        /// </param>
        /// <param name="maxConcurrency">
        /// The maximum number of messages to process concurrently. Must be greater than zero.
        /// </param>
        /// <param name="handler">
        /// A delegate invoked for each received deserialized message.
        /// </param>
        /// <param name="ct">
        /// A cancellation token used to stop message consumption.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task StartParallelAsync<T>(
            string address,
            string queue,
            AmqpAddressType addressType,
            int maxConcurrency,
            Func<T, CancellationToken, Task> handler,
            CancellationToken ct = default);

        /// <summary>
        /// Stops consuming messages and closes the underlying AMQP receiver link.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token used to cancel the stop operation.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous stop operation.</returns>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}