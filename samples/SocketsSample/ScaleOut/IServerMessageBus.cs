using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocketsSample.ScaleOut
{
    public interface IServerMessageBus
    {
        Task SendAsync<TMessage>(string key, TMessage message);
    }
}
