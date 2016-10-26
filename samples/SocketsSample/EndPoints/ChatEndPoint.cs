using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Channels;
using Microsoft.AspNetCore.Sockets;
using System.Collections.Concurrent;

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

        private bool ByteCompare(Span<byte> buffer, int start, int end, Span<byte> buffer2, int start2, int end2)
        {
            if (start > end || start2 > end2 || end - start < end2 - start2)
            {
                return false;
            }

            while (start < end)
            {
                if (!buffer[start].Equals(buffer2[start2]))
                {
                    return false;
                }

                start2++;
                start++;
            }

            return true;
        }

        private List<Span<byte>> GetTokens(byte[] message)
        {
            var list = new List<Span<byte>>();
            var start = 0;
            var length = message.Length;
            for (var index = start; index < length; index++)
            {
                if (message[index].Equals(Space))
                {
                    if (index != start)
                    {
                        list.Add(message.Slice(start, index - start));
                    }
                    start = index + 1;
                    continue;
                }
            }

            if (length != start)
            {
                list.Add(message.Slice(start, length - start));
            }

            return list;
        }

        private int FindSpace(Span<byte> input)
        {
            for (var i = 0; i < input.Length; ++i)
            {
                if (input[i].Equals(Space))
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindNonSpace(Span<byte> input)
        {
            for (var i = 0; i < input.Length; ++i)
            {
                if (!input[i].Equals(Space))
                {
                    return i;
                }
            }

            return -1;
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
                    var message = input.ToArray();

                    if (message[0].Equals(ForwardSlash))
                    {
                        var index = FindSpace(message.Slice(1));
                        if (index != -1)
                        {
                            index += 1;
                            if (name == null)
                            {
                                // check if /nick
                                if (message.Slice(0, index).BlockEquals(Nick.Slice()))
                                {
                                    var nameIndex = FindNonSpace(message.Slice(index));
                                    if (nameIndex != -1)
                                    {
                                        index += nameIndex;
                                        var index3 = FindSpace(message.Slice(index));
                                        if (index3 == -1)
                                        {
                                            name = message.Slice(index).ToArray();
                                        }
                                        else
                                        {
                                            name = message.Slice(index, index3).ToArray();
                                        }
                                        _users.AddOrUpdate(Encoding.UTF8.GetString(name), connection, (k, c) => { throw new Exception("name already added"); });
                                        await Broadcast($"Set username to {Encoding.UTF8.GetString(name)}", connection);
                                    }
                                }
                            }
                            else //already have a name
                            {
                                // check if /msg
                                if (message.Slice(0, index).BlockEquals(Msg.Slice()))
                                {
                                    var nameIndex = FindNonSpace(message.Slice(index));
                                    if (nameIndex != -1)
                                    {
                                        index += nameIndex;
                                        byte[] pmName = null;
                                        var index3 = FindSpace(message.Slice(index));
                                        if (index3 == -1)
                                        {
                                            pmName = message.Slice(index).ToArray();
                                        }
                                        else
                                        {
                                            pmName = message.Slice(index, index3).ToArray();
                                        }
                                        index += index3;
                                        var index4 = FindNonSpace(message.Slice(index));
                                        if (index4 == -1)
                                        {
                                            //no message
                                        }
                                        else
                                        {
                                            index += index4;
                                            Connection c;
                                            if (!_users.TryGetValue(Encoding.UTF8.GetString(pmName), out c))
                                            {
                                                //failed to get user
                                            }
                                            var msg = message.Slice(index);
                                            await Broadcast(new List<Span<byte>> { PMFromSeparator, name, NameSeparator, msg }, c);
                                            await Broadcast(new List<Span<byte>> { PMToSeparator, pmName, NameSeparator, msg }, connection);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else // no '/'
                    {
                        if (name != null)
                        {
                            await Broadcast(new List<Span<byte>> { name, NameSeparator, message });
                        }
                    }
                }
                finally
                {
                    connection.Channel.Input.Advance(input.End);
                }
            }

            Connection temp;
            var nameStr = Encoding.UTF8.GetString(name);
            if (!_users.TryRemove(nameStr, out temp))
            {
                // (TODO) failed to remove from dict
            }

            await Broadcast($"{nameStr}: {connection.ConnectionId} disconnected ({connection.Metadata["transport"]})");
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

        private Task Broadcast(IEnumerable<Span<byte>> payload)
        {
            var tasks = new List<Task>(Connections.Count);

            foreach (var c in Connections)
            {
                tasks.Add(Broadcast(payload, c));
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

        private Task Broadcast(IEnumerable<Span<byte>> payload, Connection c)
        {
            var writeBuffer = c.Channel.Output.Alloc();
            foreach (var val in payload)
            {
                writeBuffer.Write(val);
            }
            return writeBuffer.FlushAsync();
        }
    }
}
