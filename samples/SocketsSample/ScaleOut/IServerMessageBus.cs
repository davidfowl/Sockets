using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocketsSample.ScaleOut
{
    public interface IServerMessageBus
    {
        Task PublishAsync(string key, byte[] message);

        Task SubscribeAsync(string key, Func<byte[], Task> receiveCallback);
    }
}
