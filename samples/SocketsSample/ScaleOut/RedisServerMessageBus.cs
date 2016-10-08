using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace SocketsSample.ScaleOut
{
    public class RedisServerMessageBus : IServerMessageBus
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly ISubscriber _subscriber;

        // TODO: Flow connection string via an IOptions<>
        public RedisServerMessageBus()
        {
            _redis = ConnectionMultiplexer.Connect("localhost");
            _redis.ConnectionFailed += ConnectionFailed;
            _subscriber = _redis.GetSubscriber();
        }

        public Task PublishAsync(string key, byte[] message)
        {
            return _subscriber.PublishAsync(key, message);
        }

        public Task SubscribeAsync(string key, Action<byte[]> receiveCallback)
        {
            return _subscriber.SubscribeAsync(key, (_, message) => receiveCallback(message));
        }

        private void ConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            // TODO: Handle connection failed

        }
    }
}
