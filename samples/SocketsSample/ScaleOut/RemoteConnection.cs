using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SocketsSample.ScaleOut
{
    public class RemoteConnection
    {
        [Key]
        public int Id { get; set; }

        public string ConnectionId { get; set; }

        public string ServerName { get; set; }

        public string UserName { get; set; }

        public virtual ICollection<RemoteConnectionSignal> Signals { get; set; } = new List<RemoteConnectionSignal>();
    }

    public class RemoteConnectionSignal
    {
        public int ConnectionId { get; set; }
        public RemoteConnection Connection { get; set; }

        public int SignalId { get; set; }
        public Signal Signal { get; set; }
    }
}
