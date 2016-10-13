using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using SocketsSample.ScaleOut;

namespace SocketsSample.Migrations
{
    [DbContext(typeof(ConnectionStoreContext))]
    [Migration("20161013054448_Initial")]
    partial class Initial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.0.1")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("SocketsSample.ScaleOut.RemoteConnection", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ConnectionId");

                    b.Property<string>("ServerName");

                    b.Property<string>("UserName");

                    b.HasKey("Id");

                    b.ToTable("Connections");
                });

            modelBuilder.Entity("SocketsSample.ScaleOut.RemoteConnectionSignal", b =>
                {
                    b.Property<int>("ConnectionId");

                    b.Property<int>("SignalId");

                    b.HasKey("ConnectionId", "SignalId");

                    b.HasIndex("ConnectionId");

                    b.HasIndex("SignalId");

                    b.ToTable("RemoteConnectionSignal");
                });

            modelBuilder.Entity("SocketsSample.ScaleOut.Signal", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Name");

                    b.HasKey("Id");

                    b.ToTable("Signals");
                });

            modelBuilder.Entity("SocketsSample.ScaleOut.RemoteConnectionSignal", b =>
                {
                    b.HasOne("SocketsSample.ScaleOut.RemoteConnection", "Connection")
                        .WithMany("Signals")
                        .HasForeignKey("ConnectionId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("SocketsSample.ScaleOut.Signal", "Signal")
                        .WithMany("Connections")
                        .HasForeignKey("SignalId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
        }
    }
}
