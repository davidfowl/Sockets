using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocketsSample.ScaleOut
{
    public class RemoteConnection
    {
        public string ConnectionId { get; set; }

        public string ServerName { get; set; }

        public string UserName { get; set; }

        public virtual ICollection<Signal> Signals { get; set; }
    }
}
