using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SocketsSample.ScaleOut
{
    public class ConnectionStoreContext : DbContext
    {
        public ConnectionStoreContext(DbContextOptions options)
            : base(options)
        {

        }

        public DbSet<RemoteConnection> Connections { get; set; }

        public DbSet<Signal> Signals { get; set; }
    }
}
