using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SocketsSample.Hubs
{
    public class Chat : Hub
    {
        private static readonly ConcurrentDictionary<string, string> _users = new ConcurrentDictionary<string, string>();

        public override async Task OnConnectedAsync()
        {
            await Clients.All.Invoke("Send", Context.Connection.ConnectionId + " joined");
        }

        public override async Task OnDisconnectedAsync()
        {
            await Clients.All.Invoke("Send", Context.Connection.ConnectionId + " left");

            foreach (var val in _users)
            {
                if (val.Value.Equals(Context.ConnectionId))
                {
                    string ignore;
                    _users.TryRemove(val.Key, out ignore);
                    break;
                }
            }
        }

        public Task Send(string message)
        {
            // TODO: make this better
            string userName = string.Empty;
            foreach (var val in _users)
            {
                if (val.Value.Equals(Context.ConnectionId))
                {
                    userName = val.Key;
                    break;
                }
            }

            message = message.Trim();

            if (message.StartsWith("/"))
            {
                var tokens = message.Split(' ');
                if (tokens[0].Equals("/nick", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(userName))
                    {
                        Clients.Client(Context.ConnectionId).Invoke("Send", "You already have a username");
                        return Task.CompletedTask;
                    }

                    if (tokens.Length > 1)
                    {
                        userName = tokens[1];
                    }
                    else
                    {
                        Clients.Client(Context.ConnectionId).Invoke("Send", "No username specified");
                        return Task.CompletedTask;
                    }

                    var connectionId = _users.GetOrAdd(userName, Context.ConnectionId);
                    if (!connectionId.Equals(Context.ConnectionId))
                    {
                        Clients.Client(Context.ConnectionId).Invoke("Send", $"Someone else already has {userName} for a username");
                        return Task.CompletedTask;
                    }

                    Clients.Client(Context.ConnectionId).Invoke("Send", $"Username set to {userName}");
                    Clients.All.Invoke("Send", $"{userName} has entered the room");
                }
            }
            else // message.StartsWith("/")
            {
                if (!string.IsNullOrEmpty(userName))
                {
                    Clients.All.Invoke("Send", $"{userName}: {message}");
                }
                else
                {
                    Clients.Client(Context.ConnectionId).Invoke("Send", "Set username before writing messages, use /nick [name]");
                }
            }

            return Task.CompletedTask;
        }
    }
}
