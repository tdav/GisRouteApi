using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Exceptions;
using System;
using System.Reflection;

namespace GisRouteApi
{
    public class Program
    {
        public IConfiguration Conf { get; set; }

        public static void Main(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                              .Enrich.FromLogContext()
                              .Enrich.WithMachineName()
                              .Enrich.WithExceptionDetails()
                              .Enrich.WithProperty("Environment", environment)
                              .ReadFrom.Configuration(configuration)
                              .CreateLogger();
            try
            {
                Host.CreateDefaultBuilder(args)
                     .ConfigureWebHostDefaults(wb => { wb.UseStartup<Startup>(); })
                     .ConfigureAppConfiguration(conf =>
                     {
                         conf.AddEnvironmentVariables();
                         conf.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                         conf.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true);
                     })
                     .UseSerilog()
                     .Build()
                     .Run();
            }
            catch (Exception ex)
            {
                Log.Fatal($"Failed to start {Assembly.GetExecutingAssembly().GetName().Name}", ex);
                throw;
            }
        }
    }
}


