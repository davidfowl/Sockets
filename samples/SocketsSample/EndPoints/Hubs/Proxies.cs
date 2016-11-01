using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Channels;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.DependencyInjection;
using SocketsSample.Hubs;

namespace SocketsSample.EndPoints.Hubs
{
    public class UserProxy : AllClientProxy
    {
        private readonly string _userId;

        public UserProxy(IServiceProvider serviceProvier, ConnectionList connections, string userId) : base(serviceProvier, connections)
        {
            _userId = userId;
        }

        protected override bool Include(Connection connection)
        {
            return connection.User.Identity.Name == _userId;
        }
    }

    public class GroupManager : IGroupManager
    {
        private readonly Connection _connection;
        public GroupManager(Connection connection)
        {
            _connection = connection;
        }

        public void Add(string groupName)
        {
            // TODO: Make metadata thread safe
            _connection.Metadata["group"] = groupName;
        }

        public void Remove(string groupName)
        {
            _connection.Metadata["group"] = null;
        }
    }

    public class GroupProxy : AllClientProxy
    {
        private readonly string _groupName;
        public GroupProxy(IServiceProvider serviceProvier, ConnectionList connections, string groupName) : base(serviceProvier, connections)
        {
            _groupName = groupName;
        }

        protected override bool Include(Connection connection)
        {
            return (string)connection.Metadata["group"] == _groupName;
        }
    }

    public class AllClientProxy : IClientProxy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConnectionList _connections;

        public AllClientProxy(IServiceProvider serviceProvier, ConnectionList connections)
        {
            _serviceProvider = serviceProvier;
            _connections = connections;
        }

        public Task Invoke(string method, params object[] args)
        {
            // REVIEW: Thread safety
            var tasks = new List<Task>(_connections.Count);
            var message = new InvocationDescriptor
            {
                Method = method,
                Arguments = args
            };

            // TODO: serialize once per format by providing a different stream?
            foreach (var connection in _connections)
            {
                if (!Include(connection))
                {
                    continue;
                }
                var invocationAdapter =
                    _serviceProvider
                        .GetRequiredService<InvocationAdapterRegistry>()
                        .GetInvocationAdapter((string)connection.Metadata["formatType"]);

                tasks.Add(invocationAdapter.WriteInvocationDescriptor(message, connection.Channel.GetStream()));
            }

            return Task.WhenAll(tasks);
        }

        protected virtual bool Include(Connection connection)
        {
            return true;
        }
    }

    public class SingleClientProxy : IClientProxy
    {
        private readonly string _connectionId;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConnectionList _connections;

        public SingleClientProxy(IServiceProvider serviceProvier, ConnectionList connections, string connectionId)
        {
            _serviceProvider = serviceProvier;
            _connections = connections;
            _connectionId = connectionId;
        }

        public Task Invoke(string method, params object[] args)
        {
            var connection = _connections[_connectionId];

            var invocationAdapter =
                _serviceProvider
                    .GetRequiredService<InvocationAdapterRegistry>()
                    .GetInvocationAdapter((string)connection.Metadata["formatType"]);

            var message = new InvocationDescriptor
            {
                Method = method,
                Arguments = args
            };

            return invocationAdapter.WriteInvocationDescriptor(message, connection.Channel.GetStream());
        }
    }

    public class LocalCallbacks : HubCallbacks
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConnectionList _connections;

        public LocalCallbacks(IServiceProvider serviceProvier, ConnectionList connections)
        {
        }

        public override Task InvokeAllConnections(string hubName, string method, params object[] args)
        {
            // REVIEW: Thread safety
            var tasks = new List<Task>(_connections.Count);
            var message = new InvocationDescriptor
            {
                Method = method,
                Arguments = args
            };

            // TODO: serialize once per format by providing a different stream?
            foreach (var connection in _connections)
            {
                var invocationAdapter =
                    _serviceProvider
                        .GetRequiredService<InvocationAdapterRegistry>()
                        .GetInvocationAdapter((string)connection.Metadata["formatType"]);

                tasks.Add(invocationAdapter.WriteInvocationDescriptor(message, connection.Channel.GetStream()));
            }

            return Task.WhenAll(tasks);
        }
    }

    public class PubSubcallbacks : HubCallbacks
    {
        private IPubSub _bus;

        public override Task InvokeAllConnections(string hubName, string method, params object[] args)
        {
            var message = new InvocationDescriptor
            {
                Method = method,
                Arguments = args
            };

            return _bus.Publish(hubName, message);
        }
    }

    public abstract class HubCallbacks
    {
        public virtual Task InvokeConnection(string hubName, string connectionId, string method, params object[] args)
        {
            return Task.CompletedTask;
        }

        public virtual Task InvokeAllConnections(string hubName, string method, params object[] args)
        {
            return Task.CompletedTask;
        }

        public virtual Task InvokeGroupConnections(string hubName, string groupName, string method, params object[] args)
        {
            return Task.CompletedTask;
        }

        public virtual Task InvokeUserConnections(string hubName, string userId, string method, params object[] args)
        {
            return Task.CompletedTask;
        }
    }
}
