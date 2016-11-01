using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;

namespace SocketsSample.Hubs
{
    public interface IHubConnectionContext
    {
        IClientProxy All { get; }

        IClientProxy Client(string connectionId);

        IClientProxy Group(string groupName);

        IClientProxy User(string userId);
    }

    public interface IGroupManager
    {
        void Add(string groupName);
        void Remove(string groupName);
    }

    public interface IClientProxy
    {
        /// <summary>
        /// Invokes a method on the connection(s) represented by the <see cref="IClientProxy"/> instance.
        /// </summary>
        /// <param name="method">name of the method to invoke</param>
        /// <param name="args">argumetns to pass to the client</param>
        /// <returns>A task that represents when the data has been sent to the client.</returns>
        Task Invoke(string method, params object[] args);
    }

    public class HubCallerContext
    {
        public HubCallerContext(Connection connection)
        {
            ConnectionId = connection.ConnectionId;
            User = connection.User;
            Connection = connection;
        }

        public Connection Connection { get; }

        public ClaimsPrincipal User { get; }

        public string ConnectionId { get; }
    }
}
