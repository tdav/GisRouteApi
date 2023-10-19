using GisRouteApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;
using Serilog;

namespace GisRouteApi
{
    public class Startup
    {
        public IConfiguration conf { get; }

        public Startup(IConfiguration conf) => this.conf = conf;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
                 .AddNewtonsoftJson(opt =>
                 {
                     opt.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                     opt.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                 });

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddSingleton<IRouterDbService, RouterDbService>();
            services.AddSerilog();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(end => end.MapControllers());
            app.UseSerilogRequestLogging();
        }
    }
}
