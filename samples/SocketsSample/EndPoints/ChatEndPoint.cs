using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Channels;
using Microsoft.AspNetCore.Sockets;
using System.Collections.Concurrent;
using Channels.Text.Primitives;

namespace SocketsSample
{
    public class ChatEndPoint : EndPoint
    {
        private static readonly byte[] Join = Encoding.UTF8.GetBytes("/join");
        private static readonly byte[] Msg = Encoding.UTF8.GetBytes("/msg");
        private static readonly byte[] Nick = Encoding.UTF8.GetBytes("/nick");
        private static readonly byte[] Room = Encoding.UTF8.GetBytes("/room");
        private static readonly byte[] NameSeparator = Encoding.UTF8.GetBytes(": ");
        private static readonly byte[] PMFromSeparator = Encoding.UTF8.GetBytes("PM from ");
        private static readonly byte[] PMToSeparator = Encoding.UTF8.GetBytes("PM to ");
        private static readonly byte Space = Encoding.UTF8.GetBytes(" ")[0];
        private static readonly byte ForwardSlash = Encoding.UTF8.GetBytes("/")[0];

        private ConcurrentDictionary<string, Connection> _users = new ConcurrentDictionary<string, Connection>();

        private int FindSpace(ReadableBufferReader input)
        {
            while (input.Peek() != Space && !input.End)
            {
                input.Take();
            }

            if (input.End)
            {
                return -1;
            }

            return input.Index;
        }

        private int FindNonSpace(ref ReadableBufferReader input)
        {
            while (input.Peek() == Space)
            {
                input.Take();
            }

            if (input.End)
            {
                return -1;
            }

            return input.Index;
        }

        public override async Task OnConnected(Connection connection)
        {
            await Broadcast($"{connection.ConnectionId} connected ({connection.Metadata["transport"]})");
            byte[] name = null;

            while (true)
            {
                var result = await connection.Channel.Input.ReadAsync();
                var input = result.Buffer;
                try
                {
                    if (input.IsEmpty && result.IsCompleted)
                    {
                        break;
                    }

                    // We can avoid the copy here but we'll deal with that later
                    //var message = input.ToArray();

                    if (input.Peek() == ForwardSlash)
                    {
                        ReadableBuffer buffer;
                        ReadCursor cursor;
                        if (input.TrySliceTo(Space, out buffer, out cursor))
                        {
                            //only allow /nick until user has a name
                            if (name == null)
                            {
                                if (buffer.Equals(Nick))
                                {
                                    buffer = input.Slice(cursor);
                                    buffer = buffer.TrimStart();

                                    if (buffer.Length > 0)
                                    {
                                        var i = FindSpace(new ReadableBufferReader(buffer));
                                        if (i == -1)
                                        {
                                            name = buffer.ToArray();
                                        }
                                        else
                                        {
                                            name = buffer.Slice(0, i).ToArray();
                                        }

                                        var stringName = Encoding.UTF8.GetString(name);
                                        _users.AddOrUpdate(stringName, connection, (k, c) => { throw new Exception("name already added"); });
                                        await Broadcast($"Set username to {stringName}", connection);
                                    }
                                }
                            }
                            else
                            {
                                if (buffer.Equals(Msg))
                                {
                                    buffer = input.Slice(cursor);
                                    buffer = buffer.TrimStart();

                                    var i = FindSpace(new ReadableBufferReader(buffer));
                                    if (i == -1)
                                    {
                                        // no user specified
                                    }
                                    else
                                    {
                                        var pmName = buffer.Slice(0, i).ToArray();

                                        Connection c;
                                        if (!_users.TryGetValue(Encoding.UTF8.GetString(pmName), out c))
                                        {
                                            //failed to get user
                                        }

                                        buffer = buffer.Slice(i).TrimStart();

                                        if (buffer.Length > 0)
                                        {
                                            var message = buffer.ToArray();

                                            await Broadcast(PMFromSeparator, name, NameSeparator, message, c);
                                            await Broadcast(PMToSeparator, pmName, NameSeparator, message, connection);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    // no '/'
                    else
                    {
                        if (name != null)
                        {
                            // WriteableBuffer can take a ReadableBuffer in Append look into fixing this
                            await Broadcast(name, NameSeparator, input.ToArray());
                        }
                    }
                }
                finally
                {
                    connection.Channel.Input.Advance(input.End);
                }
            }

            if (name != null)
            {
                Connection temp;
                var nameStr = Encoding.UTF8.GetString(name);
                if (!_users.TryRemove(nameStr, out temp))
                {
                    // (TODO) failed to remove from dict
                }
            }

            await Broadcast($"{connection.ConnectionId} disconnected ({connection.Metadata["transport"]})");
        }

        private Task Broadcast(string text)
        {
            return Broadcast(Encoding.UTF8.GetBytes(text));
        }

        private Task Broadcast(byte[] payload)
        {
            var tasks = new List<Task>(Connections.Count);

            foreach (var c in Connections)
            {
                tasks.Add(c.Channel.Output.WriteAsync(payload));
            }

            return Task.WhenAll(tasks);
        }

        private Task Broadcast(Span<byte> payload1, Span<byte> payload2)
        {
            var tasks = new List<Task>(Connections.Count);

            foreach (var c in Connections)
            {
                tasks.Add(Broadcast(payload1, payload2, c));
            }

            return Task.WhenAll(tasks);
        }

        private Task Broadcast(Span<byte> payload1, Span<byte> payload2, Span<byte> payload3)
        {
            var tasks = new List<Task>(Connections.Count);

            foreach (var c in Connections)
            {
                tasks.Add(Broadcast(payload1, payload2, payload3, c));
            }

            return Task.WhenAll(tasks);
        }

        private Task Broadcast(string payload, Connection c)
        {
            return Broadcast(Encoding.UTF8.GetBytes(payload), c);
        }

        private Task Broadcast(Span<byte> payload, Connection c)
        {
            return c.Channel.Output.WriteAsync(payload);
        }

        private Task Broadcast(Span<byte> payload1, Span<byte> payload2, Connection c)
        {
            var writeBuffer = c.Channel.Output.Alloc();
            writeBuffer.Write(payload1);
            writeBuffer.Write(payload2);
            return writeBuffer.FlushAsync();
        }

        private Task Broadcast(Span<byte> payload1, Span<byte> payload2, Span<byte> payload3, Connection c)
        {
            var writeBuffer = c.Channel.Output.Alloc();
            writeBuffer.Write(payload1);
            writeBuffer.Write(payload2);
            writeBuffer.Write(payload3);
            return writeBuffer.FlushAsync();
        }

        private Task Broadcast(Span<byte> payload1, Span<byte> payload2, Span<byte> payload3, Span<byte> payload4, Connection c)
        {
            var writeBuffer = c.Channel.Output.Alloc();
            writeBuffer.Write(payload1);
            writeBuffer.Write(payload2);
            writeBuffer.Write(payload3);
            writeBuffer.Write(payload4);
            return writeBuffer.FlushAsync();
        }
    }
}
