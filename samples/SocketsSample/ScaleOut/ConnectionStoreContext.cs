using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SocketsSample.ScaleOut
{
    public class ConnectionStoreContext : DbContext
    {
        public ConnectionStoreContext()
        {

        }

        public ConnectionStoreContext(DbContextOptions options)
            : base(options)
        {

        }

        public DbSet<RemoteConnection> Connections { get; set; }

        public DbSet<Signal> Signals { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RemoteConnectionSignal>()
                .HasKey(cs => new { cs.ConnectionId, cs.SignalId });

            modelBuilder.Entity<RemoteConnectionSignal>()
                .HasOne(cs => cs.Connection)
                .WithMany(c => c.Signals)
                .HasForeignKey(cs => cs.ConnectionId);

            modelBuilder.Entity<RemoteConnectionSignal>()
                .HasOne(cs => cs.Signal)
                .WithMany(s => s.Connections)
                .HasForeignKey(cs => cs.SignalId);
        }
    }
}
