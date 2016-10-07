using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocketsSample.ScaleOut
{
    public interface IDistributedConnectionStore
    {
        // REVIEW: Not sure the best contract for storing user here, e.g. ClaimsPrincipal vs. string vs. something else
        Task AddConnectionAsync(string serverName, string connectionId, string user, IList<string> signals);

        Task RemoveConnectionAsync(string serverName, string connectionId);

        Task<IList<IGrouping<string, RemoteConnection>>> GetRemoteConnectionsAsync(string serverName, string signal);
    }
}
