using System;
using Microsoft.AspNetCore.Sockets;

namespace SocketsSample
{
    public class EchoEndPoint : MessageEndPoint
    {
        public async override void OnMessage(ArraySegment<byte> message, Connection connection)
        {
            await Send(connection, message);
        }
    }
}
