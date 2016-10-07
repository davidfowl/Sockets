using System.Collections;
using System.Collections.Generic;

namespace SocketsSample.ScaleOut
{
    public class Signal
    {
        public int Id { get; set; }

        // TODO: How to enforce that this is unique?
        public string Name { get; set; }

        public virtual ICollection<RemoteConnection> Connections { get; set; }
    }
}