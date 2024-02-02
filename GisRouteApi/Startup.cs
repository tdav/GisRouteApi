using GisRouteApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;
using Serilog;
using System.Net.Http;

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

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllHeaders",
                        builder =>
                        {
                            builder
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowAnyOrigin();
                        });
            });

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.EnableAnnotations();
            });
            services.AddSingleton<IRouterDbService, RouterDbService>();
            //services.AddSerilog();
            services.AddHttpClient("RouterDbService").ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            });
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
