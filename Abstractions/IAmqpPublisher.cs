using Amqp;
using Amqp.Framing;

namespace ActiveMQ.Helper.Abstractions
{
    /// <summary>
    /// Defines a publisher capable of sending AMQP messages
    /// to a specific queue or topic under a given address.
    /// </summary>
    public interface IAmqpPublisher
    {
        /// <summary>
        /// Sends a strongly-typed payload. The object will be serialized
        /// using AMQPNetLite and placed in the message body.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the payload to serialize.
        /// </typeparam>
        /// <param name="payload">
        /// The object to send.
        /// </param>
        /// <param name="address">
        /// The broker address (e.g. "br.notifications").
        /// </param>
        /// <param name="queue">
        /// The queue or subscription name under the address
        /// (e.g. "pago-nomina").
        /// </param>
        /// <param name="addressType">
        /// Specifies how the destination should be treated by the broker.
        /// Use <see cref="AmqpAddressType.Queue"/> for point-to-point (ANYCAST)
        /// or <see cref="AmqpAddressType.Topic"/> for publish/subscribe (MULTICAST).
        /// </param>
        /// <param name="ct">
        /// A cancellation token used to cancel the send operation.
        /// </param>
        Task SendAsync<T>(
            T payload,
            string address,
            string queue,
            AmqpAddressType addressType,
            CancellationToken ct = default);

        /// <summary>
        /// Sends a fully constructed AMQP <see cref="Message"/>
        /// to the specified address and queue.
        /// </summary>
        /// <param name="message">
        /// The AMQP message to send.
        /// </param>
        /// <param name="address">
        /// The broker address (e.g. "br.notifications").
        /// </param>
        /// <param name="queue">
        /// The queue or subscription name under the address
        /// (e.g. "pago-nomina").
        /// </param>
        /// <param name="addressType">
        /// Specifies how the destination should be treated by the broker.
        /// Use <see cref="AmqpAddressType.Queue"/> for point-to-point (ANYCAST)
        /// or <see cref="AmqpAddressType.Topic"/> for publish/subscribe (MULTICAST).
        /// </param>
        /// <param name="ct">
        /// A cancellation token used to cancel the send operation.
        /// </param>
        Task SendAsync(
            Message message,
            string address,
            string queue,
            AmqpAddressType addressType,
            CancellationToken ct = default);

      
    }
}
