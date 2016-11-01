using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;
using SocketsSample.Hubs;

namespace SocketsSample.EndPoints.Hubs
{
    public class PubSubClientProxy : IClientProxy
    {
        private readonly IPubSub _bus;
        private readonly string _signal;

        public PubSubClientProxy(string signal, IPubSub bus)
        {
            _signal = signal;
            _bus = bus;
        }

        public Task Invoke(string method, params object[] args)
        {
            var message = new InvocationDescriptor
            {
                Method = method,
                Arguments = args
            };

            return _bus.Publish(_signal, message);
        }
    }

    public class PubSubGroupManager : IGroupManager
    {
        private readonly Func<string, Connection, IDisposable> _subscribe;
        private readonly Connection _connection;

        public PubSubGroupManager(Func<string, Connection, IDisposable> subscribe, Connection connection)
        {
            _subscribe = subscribe;
            _connection = connection;
        }

        public void Add(string groupName)
        {
            var groups = _connection.Metadata.GetOrAdd("groups", k => new ConcurrentDictionary<string, IDisposable>());
            groups[groupName] = _subscribe(groupName, _connection);
        }

        public void Remove(string groupName)
        {
            var groups = _connection.Metadata.Get<ConcurrentDictionary<string, IDisposable>>("groups");

            IDisposable subscription;
            if (groups.TryRemove(groupName, out subscription))
            {
                subscription.Dispose();
            }
        }
    }
}
