using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocketsSample.ScaleOut
{
    public class RedisServerMessageBus : IServerMessageBus
    {
        public Task SendAsync<TMessage>(string key, TMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
