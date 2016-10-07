using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SocketsSample.ScaleOut
{
    public class EntityFrameworkConnectionStore : IDistributedConnectionStore
    {
        private readonly ConnectionStoreContext _db;

        public EntityFrameworkConnectionStore(ConnectionStoreContext db)
        {
            _db = db;
        }

        public async Task AddConnectionAsync(string serverName, string connectionId, string user, IList<string> signals)
        {
            var connection = new RemoteConnection { ConnectionId = connectionId, ServerName = serverName, UserName = user };

            foreach (var signal in signals)
            {
                // HACK: This is totally not the way to do this, but hacking it up in EF so meh
                var remoteSignal = await _db.Signals.SingleOrDefaultAsync(s => s.Name == signal);
                if (remoteSignal == null)
                {
                    remoteSignal = new Signal { Name = signal };
                    _db.Signals.Add(remoteSignal);
                }
                connection.Signals.Add(remoteSignal);
            }

            _db.Connections.Add(connection);

            await _db.SaveChangesAsync();
        }

        public async Task<IList<IGrouping<string, RemoteConnection>>> GetRemoteConnectionsAsync(string serverName, string signal)
        {
            IList<IGrouping<string, RemoteConnection>> connections = await _db.Signals
                .Include(s => s.Connections)
                .Where(s => s.Name == signal)
                .SelectMany(s => s.Connections)
                .GroupBy(c => c.ServerName)
                .ToListAsync();

            return connections;
        }

        public async Task RemoveConnectionAsync(string serverName, string connectionId)
        {
            var connection = await _db.Connections.SingleOrDefaultAsync(c => c.ConnectionId == connectionId);
            _db.Remove(connection);
            await _db.SaveChangesAsync();
        }
    }
}
