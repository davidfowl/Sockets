using System;
using System.Threading;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocketsSample.EndPoints.Hubs;
using SocketsSample.Hubs;

namespace SocketsSample
{
    public class Hub : RpcEndpoint, IHubConnectionContext
    {
        private readonly IServiceProvider _serviceProvider;
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

        private Connection Connection => _connection.Value;

        public virtual HubCallerContext Context => new HubCallerContext(Connection);

        public virtual IGroupManager Groups => new GroupManager(Connection);

        public virtual IServiceProvider Services => _scope.Value.ServiceProvider;

        IClientProxy IHubConnectionContext.All => _all;

        IClientProxy IHubConnectionContext.Client(string connectionId)
        {
            return new SingleClientProxy(_serviceProvider, Connections, connectionId);
        }

        IClientProxy IHubConnectionContext.Group(string groupName)
        {
            return new GroupProxy(_serviceProvider, Connections, groupName);
        }

        IClientProxy IHubConnectionContext.User(string userId)
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
}
