using System;
using System.Threading;
using System.Threading.Tasks;
using Channels;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocketsSample.EndPoints.Hubs;
using SocketsSample.Hubs;

namespace SocketsSample
{
    public class Hub : RpcEndpoint, IHubConnectionContext
    {
        protected readonly IServiceProvider _serviceProvider;
        private readonly AllClientProxy _all;

        // Currently executing connection
        private AsyncLocal<Connection> _connection = new AsyncLocal<Connection>();

        // Scoped services for this call
        private AsyncLocal<IServiceScope> _scope = new AsyncLocal<IServiceScope>();

        public Hub(ILogger<RpcEndpoint> jsonRpcLogger, IServiceProvider serviceProvider)
            : base(jsonRpcLogger, serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _all = new AllClientProxy(_serviceProvider, Connections);
        }

        public IHubConnectionContext Clients => this;

        protected Connection Connection => _connection.Value;

        public virtual HubCallerContext Context => new HubCallerContext(Connection);

        public virtual IGroupManager Groups => new GroupManager(Connection);

        public virtual IServiceProvider Services => _scope.Value.ServiceProvider;

        public virtual IClientProxy All => _all;

        public virtual IClientProxy Client(string connectionId)
        {
            return new SingleClientProxy(_serviceProvider, Connections, connectionId);
        }

        public virtual IClientProxy Group(string groupName)
        {
            return new GroupProxy(_serviceProvider, Connections, groupName);
        }

        public virtual IClientProxy User(string userId)
        {
            return new UserProxy(_serviceProvider, Connections, userId);
        }

        protected override void BeforeInvoke(Connection connection, object endpoint)
        {
            _connection.Value = connection;
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            _scope.Value = scopeFactory.CreateScope();
        }

        protected override void AfterInvoke(Connection connection, object endpoint)
        {
            _connection.Value = null;
            _scope.Value.Dispose();
        }

        protected override void DiscoverEndpoints()
        {
            RegisterRPCEndPoint(GetType());
        }
    }

    public class PubSubHub : Hub
    {
        private readonly IPubSub _bus;
        private readonly IClientProxy _all;

        public PubSubHub(IPubSub bus, ILogger<RpcEndpoint> jsonRpcLogger, IServiceProvider serviceProvider) : base(jsonRpcLogger, serviceProvider)
        {
            _bus = bus;
            _all = new PubSubClientProxy(GetType().Name, bus);
        }

        public override IClientProxy All => _all;

        public override IClientProxy Client(string connectionId)
        {
            return new PubSubClientProxy(GetType().Name + "." + connectionId, _bus);
        }

        public override IClientProxy Group(string groupName)
        {
            return new PubSubClientProxy(GetType().Name + "." + groupName, _bus);
        }

        public override IClientProxy User(string userId)
        {
            return new PubSubClientProxy(GetType().Name + "." + userId, _bus);
        }

        public override IGroupManager Groups => new PubSubGroupManager(Subscribe, GetType().Name, Connection);

        public override async Task OnConnected(Connection connection)
        {
            using (Subscribe(GetType().Name, connection))
            using (Subscribe(GetType().Name + "." + connection.ConnectionId, connection))
            using (Subscribe(GetType().Name + "." + connection.User.Identity.Name, connection))
            {
                await base.OnConnected(connection);
            }
        }

        private IDisposable Subscribe(string signal, Connection connection)
        {
            return _bus.Subscribe(signal, message =>
            {
                var invocationAdapter =
                    _serviceProvider
                        .GetRequiredService<InvocationAdapterRegistry>()
                        .GetInvocationAdapter((string)connection.Metadata["formatType"]);

                return invocationAdapter.WriteInvocationDescriptor((InvocationDescriptor)message, connection.Channel.GetStream());
            });
        }
    }
}
