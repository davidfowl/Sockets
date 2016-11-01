using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.Logging;

namespace SocketsSample.Hubs
{
    public class Chat : PubSubHub
    {
        // TODO: This needs to go away
        public Chat(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override async Task OnConnected(Connection connection)
        {
            await Clients.All.Invoke("Send", connection.ConnectionId + " joined");

            await base.OnConnected(connection);

            await Clients.All.Invoke("Send", connection.ConnectionId + " left");
        }

        public Task Send(string message)
        {
            return Clients.All.Invoke("Send", Context.ConnectionId + ": " + message);
        }
    }

    public class ChatNormal : Hub
    {
        public ChatNormal(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override async Task OnConnected(Connection connection)
        {
            await Clients.All.Invoke("Send", connection.ConnectionId + " joined");

            await base.OnConnected(connection);

            await Clients.All.Invoke("Send", connection.ConnectionId + " left");
        }

        public Task Send(string message)
        {
            return Clients.All.Invoke("Send", Context.ConnectionId + ": " + message);
        }

        public Task Send(string room, string message)
        {
            return Clients.Group(room).Invoke("Send", message);
        }

        public void JoinRoom(string room)
        {
            Clients.Group(room).Invoke("Send", Context.ConnectionId + " joined " + room);

            Groups.Add(room);
        }

        public void LeaveRoom(string room)
        {
            Groups.Remove(room);

            Clients.Group(room).Invoke("Send", Context.ConnectionId + " left " + room);
        }
    }

    public class ChatRedis : Hub
    {
        private readonly IPubSub _bus;

        public ChatRedis(IPubSub bus, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _bus = bus;
        }

        public override async Task OnConnected(Connection connection)
        {
            await _bus.Publish("Chat", connection.ConnectionId + " joined");

            using (_bus.Subscribe("Chat", OnChatMessage))
            {
                await base.OnConnected(connection);
            }

            await _bus.Publish("Chat", connection.ConnectionId + " left");
        }

        public Task Send(string message)
        {
            return _bus.Publish("Chat", message);
        }

        public Task Send(string room, string message)
        {
            return _bus.Publish(room, message);
        }

        public void JoinRoom(string room)
        {
            _bus.Publish(room, Context.ConnectionId + " joined " + room);

            var subscripton = _bus.Subscribe(room, message =>
            {
                return Clients.Client(Context.ConnectionId).Invoke("Send", message);
            });

            Context.Connection.Metadata[room] = subscripton;
        }

        public void LeaveRoom(string room)
        {
            (Context.Connection.Metadata[room] as IDisposable).Dispose();

            _bus.Publish(room, Context.ConnectionId + " left " + room);
        }

        private Task OnChatMessage(object message)
        {
            return Clients.All.Invoke("Send", message);
        }
    }
}
