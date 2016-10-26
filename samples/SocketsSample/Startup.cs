using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocketsSample.ScaleOut;
using System;
using System.Text;

namespace SocketsSample
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                //builder.AddUserSecrets();
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting();
            services.AddDbContext<ConnectionStoreContext>(options => options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
            services.AddSingleton<IDistributedConnectionStore, EntityFrameworkConnectionStore>();
            services.AddSingleton<IServerMessageBus, RedisServerMessageBus>();
            services.AddSingleton<HubEndpoint>();
            services.AddSingleton<JsonRpcEndpoint>();
            services.AddSingleton<ChatEndPoint>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.Map("/asasas", c =>
            {
                c.Run(async h =>
                {
                    var g = Encoding.UTF8.GetBytes(Environment.MachineName);
                    //await h.Response.Body.WriteAsync(Environment.MachineName);
                    await h.Response.Body.WriteAsync(g, 0, g.Length);
                });
            });
            loggerFactory.AddConsole(LogLevel.Debug);
            loggerFactory.AddDebug(LogLevel.Debug);

            app.UseFileServer();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSockets(routes =>
            {
                routes.MapSocketEndpoint<HubEndpoint>("/hubs");
                routes.MapSocketEndpoint<ChatEndPoint>("/chat");
                routes.MapSocketEndpoint<JsonRpcEndpoint>("/jsonrpc");
            });
        }
    }
}
