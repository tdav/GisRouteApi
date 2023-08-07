using GisRouteApi.Services;
using Serilog;
using Serilog.Exceptions;

var builder = WebApplication.CreateBuilder(args);


Log.Logger = new LoggerConfiguration()
                  .Enrich.FromLogContext()
                  .Enrich.WithMachineName()
                  .Enrich.WithExceptionDetails()
                  .Enrich.WithProperty("Environment", builder.Environment)
                  .ReadFrom.Configuration(builder.Configuration)
                  .CreateLogger();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IRouterDbService, RouterDbService>();
builder.Services.AddSerilog();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseSerilogRequestLogging();
app.Run();
