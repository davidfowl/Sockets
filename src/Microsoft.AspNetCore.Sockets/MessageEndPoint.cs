using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Channels;

namespace Microsoft.AspNetCore.Sockets
{
    public abstract class MessageEndPoint : EndPoint
    {
        public override async Task OnConnected(Connection connection)
        {
            byte prefixSize = 8;
            var messagePrefixBuffer = new Span<byte>(new byte[prefixSize]);
            var messageSize = -1;

            while (true)
            {
                var input = await connection.Channel.Input.ReadAsync();

                if (input.IsEmpty && connection.Channel.Input.Reading.IsCompleted)
                {
                    break;
                }

                if (messageSize == -1)
                {
                    messageSize = GetMessageSize(connection, input, messagePrefixBuffer);
                }
                else
                {
                    if (input.Length >= messageSize)
                    {
                        var slice = input.Slice(0, messageSize);
                        var message = slice.ToArray();
                        connection.Channel.Input.Advance(slice.End);
                        messageSize = -1;
                        // TODO: handle exceptions?
                        OnMessage(new ArraySegment<byte>(message), connection);
                    }
                }
            }
        }

        private int GetMessageSize(Connection connection, ReadableBuffer input, Span<byte> messagePrefixBuffer)
        {
            var prefixSize = messagePrefixBuffer.Length;

            if (input.Length < prefixSize)
            {
                return -1;
            }

            var slice = input.Slice(0, prefixSize);
            slice.CopyTo(messagePrefixBuffer);

            var messageSize = 0;
            for (var i = 0; i < prefixSize; i++)
            {
                if (!char.IsDigit((char)messagePrefixBuffer[i]))
                {
                    throw new InvalidOperationException("Invalid characters in length prefix");
                }
                messageSize = (messageSize * 10) + (messagePrefixBuffer[i] - '0');
            }

            connection.Channel.Input.Advance(slice.End);

            if (messageSize == 0)
            {
                return -1;
            }

            return messageSize;
        }

        public abstract void OnMessage(ArraySegment<byte> message, Connection connection);

        public async Task Send(Connection connection, params ArraySegment<byte>[] messages)
        {
            foreach (var message in messages)
            {
                // TODO: remove linq
                await connection.Channel.Output.WriteAsync(
                    message.Array.Length.ToString("D8", CultureInfo.InvariantCulture).Select(c => (byte)c).ToArray());

                await connection.Channel.Output.WriteAsync(message);
            }
        }
    }
}
