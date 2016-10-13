using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Channels;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocketsSample.Hubs;
using SocketsSample.ScaleOut;

namespace SocketsSample
{
    public class HubEndpoint : JsonRpcEndpoint
    {
        private readonly static string _serverName = "svr-" + Guid.NewGuid().ToString();

        private readonly ILogger<HubEndpoint> _logger;
        private readonly IServerMessageBus _messageBus;
        private readonly IDistributedConnectionStore _connectionStore;
        private readonly HashSet<string> _hubSignals = new HashSet<string>();
        private readonly ConcurrentDictionary<Type, IHubConnectionContext> _hubContexts = new ConcurrentDictionary<Type, IHubConnectionContext>();

        public HubEndpoint(IDistributedConnectionStore connectionStore, IServerMessageBus messageBus, ILogger<HubEndpoint> logger, ILogger<JsonRpcEndpoint> jsonRpcLogger, IServiceProvider serviceProvider)
            : base(jsonRpcLogger, serviceProvider)
        {
            _logger = logger;
            _connectionStore = connectionStore;
            _messageBus = messageBus;
            _messageBus.SubscribeAsync(_serverName, OnScaleoutMessage);
        }

        public IClientProxy All(string hubName)
        {
            return new AllClientProxy(this, hubName, _connectionStore, _messageBus);
        }

        public IClientProxy Client(string connectionId)
        {
            return new SingleClientProxy(this, connectionId, _connectionStore, _messageBus);
        }

        public override async Task OnConnected(Connection connection)
        {
            // Store the connection in the distributed store
            var signals = new List<string>(_hubSignals);
            signals.Add(connection.ConnectionId);
            await _connectionStore.AddConnectionAsync(_serverName, connection.ConnectionId, connection?.User?.Identity?.Name, signals);

            await base.OnConnected(connection);

            await _connectionStore.RemoveConnectionAsync(_serverName, connection.ConnectionId);
        }

        private Task OnScaleoutMessage(byte[] message)
        {
            // TODO: This would use formatters eventually to deserialize the HubScaleoutMessage
            var json = Encoding.UTF8.GetString(message);
            var hubMessage = JsonConvert.DeserializeObject<HubScaleoutMessage>(json);

            // Invoke the method
            var proxy = new AllClientProxy(this, hubMessage.HubName, _connectionStore, _messageBus);
            return Task.WhenAll(proxy.InvokeLocal(hubMessage.Method, hubMessage.Args));
        }

        private byte[] Pack(string method, object[] args)
        {
            var obj = new JObject();
            obj["method"] = method;
            obj["params"] = new JArray(args.Select(a => JToken.FromObject(a)).ToArray());

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Outgoing RPC invocation method '{methodName}'", method);
            }

            return Encoding.UTF8.GetBytes(obj.ToString());
        }

        protected override void Initialize(object endpoint)
        {
            var hub = endpoint as Hub;
            hub.Clients = _hubContexts.GetOrAdd(hub.GetType(), t =>
            {
                var allProxy = new AllClientProxy(this, t.FullName, _connectionStore, _messageBus);
                return new HubConnectionContext(allProxy, this);
            });

            base.Initialize(endpoint);
        }

        protected override void DiscoverEndpoints()
        {
            // Register the chat hub
            RegisterJsonRPCEndPoint(typeof(Chat));
            _hubSignals.Add(typeof(Chat).FullName);
        }

        private class HubConnectionContext : IHubConnectionContext
        {
            private readonly HubEndpoint _hubEndpoint;

            public HubConnectionContext(IClientProxy all, HubEndpoint hubEndpoint)
            {
                All = all;
                _hubEndpoint = hubEndpoint;
            }

            public IClientProxy All { get; }

            public IClientProxy Client(string connectionId) => _hubEndpoint.Client(connectionId);
        }

        private abstract class ClientProxy: IClientProxy
        {
            private readonly IDistributedConnectionStore _connectionStore;
            private readonly IServerMessageBus _messageBus;

            public ClientProxy(HubEndpoint endPoint, string signal, IDistributedConnectionStore connectionStore, IServerMessageBus messageBus)
            {
                EndPoint = endPoint;
                Signal = signal;
                _connectionStore = connectionStore;
                _messageBus = messageBus;
            }

            public HubEndpoint EndPoint { get; }

            public string Signal { get;}

            public abstract Task Invoke(string method, params object[] args);

            protected async Task InvokeRemote(string method, object[] args)
            {
                var remoteConnections = await _connectionStore.GetRemoteConnectionsAsync(_serverName, Signal);

                var remoteSends = new List<Task>(remoteConnections.Count);
                var message = new HubScaleoutMessage(Signal, method, args);

                foreach (var server in remoteConnections)
                {
                    // Send message to each server
                    // TODO: This would use formatters eventually to serialize the HubScaleoutMessage
                    var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                    remoteSends.Add(_messageBus.PublishAsync(server.Key, payload));
                }

                await Task.WhenAll(remoteSends);
            }
        }

        private class AllClientProxy : ClientProxy
        {
            public AllClientProxy(HubEndpoint endPoint, string hubSignal, IDistributedConnectionStore connectionStore, IServerMessageBus messageBus)
                : base(endPoint, hubSignal, connectionStore, messageBus)
            {

            }

            public override Task Invoke(string method, params object[] args)
            {
                // REVIEW: Thread safety
                var sendTasks = new List<Task>(EndPoint.Connections.Count + 1);

                // Send message to local connections
                sendTasks.AddRange(InvokeLocal(method, args));

                // Send message to connections on other servers
                sendTasks.Add(InvokeRemote(method, args));

                return Task.WhenAll(sendTasks);
            }

            public IEnumerable<Task> InvokeLocal(string method, object[] args)
            {
                byte[] message = null;

                foreach (var connection in EndPoint.Connections)
                {
                    if (message == null)
                    {
                        message = EndPoint.Pack(method, args);
                    }

                    yield return connection.Channel.Output.WriteAsync(message);
                }
            }
        }

        private class SingleClientProxy : ClientProxy
        {
            public SingleClientProxy(HubEndpoint endPoint, string connectionId, IDistributedConnectionStore connectionStore, IServerMessageBus messageBus)
                 : base(endPoint, connectionId, connectionStore, messageBus)
            {
                
            }

            public override Task Invoke(string method, params object[] args)
            {
                var connection = EndPoint.Connections[Signal];
                if (connection != null)
                {
                    // Local send
                    return connection?.Channel.Output.WriteAsync(EndPoint.Pack(method, args));
                }

                // Attempt a remote send
                // TODO: Handle unknown connection id properly
                return InvokeRemote(method, args);
            }
        }
    }
}
