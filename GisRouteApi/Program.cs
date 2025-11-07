using GisRouteApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;
using Serilog;
using Serilog.Exceptions;
using Serilog.Settings.Configuration;
using System;
using System.Net.Http;

namespace GisRouteApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddEnvironmentVariables();

            #region Init Logger
            var configurationAssemblies = new[]
            {
                typeof(ConsoleLoggerConfigurationExtensions).Assembly,
                typeof(FileLoggerConfigurationExtensions).Assembly,
            };

            var options = new ConfigurationReaderOptions(configurationAssemblies);

            Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithExceptionDetails()
                    .Enrich.WithProperty("Environment", builder.Environment)
                    .ReadFrom.Configuration(builder.Configuration, options)
                    .CreateLogger();
            #endregion

            builder.Services.AddControllers(o => { o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true; })
                            .AddNewtonsoftJson(o => { o.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver(); });

            builder.Services.AddSerilog();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllHeaders", builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyHeader()
                           .AllowAnyMethod();
                });
            });

            builder.Services.AddMemoryCache();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.EnableAnnotations();
            });

            builder.Services.AddSingleton<IRouterDbService, RouterDbService>();
            builder.Services.AddHttpClient("RouterDbService").ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            });

            var app = builder.Build();

            app.UseResponseCaching();

            app.UseRouting();
            app.UseCors("AllowAllHeaders");
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });
            app.MapControllers();

            app.UseSerilogRequestLogging();

            app.Run();
        }
    }
}


