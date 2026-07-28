using Azure;
using Azure.Identity;
using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.Middleware;
using OrderFlow.Api.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var credential = new DefaultAzureCredential();

var appConfigEndpoint = builder.Configuration["AppConfiguration:Endpoint"];
if (!string.IsNullOrEmpty(appConfigEndpoint))
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(new Uri(appConfigEndpoint), credential);
    });
}

var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), credential);
}

var appInsightsConn = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrEmpty(appInsightsConn))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConn;
    });
}

var blobConnectionString = builder.Configuration["BlobStorage:ConnectionString"];
var sbConnectionString = builder.Configuration["ServiceBus:ConnectionString"];
var egEndpoint = builder.Configuration["EventGrid:TopicEndpoint"];
var egKey = builder.Configuration["EventGrid:TopicKey"];
var redisConn = builder.Configuration["Redis:ConnectionString"];

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OrderFlow API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddHealthChecks();

var hasCosmos = !string.IsNullOrEmpty(builder.Configuration["CosmosDb:EndpointUri"]);
var hasBlob = !string.IsNullOrEmpty(blobConnectionString) || !string.IsNullOrEmpty(builder.Configuration["BlobStorage:AccountName"]);
var hasKeyVault = !string.IsNullOrEmpty(keyVaultUri);
var hasServiceBus = !string.IsNullOrEmpty(sbConnectionString);
var hasEventGrid = !string.IsNullOrEmpty(egEndpoint);
var hasRedis = !string.IsNullOrEmpty(redisConn);

if (hasCosmos)
{
    var cosmosEndpoint = builder.Configuration["CosmosDb:EndpointUri"]!;
    var cosmosKey = builder.Configuration["CosmosDb:PrimaryKey"];
    builder.Services.AddSingleton(new Microsoft.Azure.Cosmos.CosmosClient(
        cosmosEndpoint, cosmosKey,
        new Microsoft.Azure.Cosmos.CosmosClientOptions
        {
            SerializerOptions = new Microsoft.Azure.Cosmos.CosmosSerializationOptions
            {
                PropertyNamingPolicy = Microsoft.Azure.Cosmos.CosmosPropertyNamingPolicy.CamelCase
            }
        }));
    builder.Services.AddScoped<ICosmosService, CosmosService>();
    builder.Services.AddScoped<OrderProcessingService>();
}

if (hasBlob)
{
    if (!string.IsNullOrEmpty(blobConnectionString))
        builder.Services.AddSingleton(new BlobServiceClient(blobConnectionString));
    else
    {
        var blobAccountName = builder.Configuration["BlobStorage:AccountName"]!;
        builder.Services.AddSingleton(new BlobServiceClient(
            new Uri($"https://{blobAccountName}.blob.core.windows.net"), credential));
    }
    builder.Services.AddScoped<IBlobService, BlobService>();
    builder.Services.AddScoped<IImageService, ImageService>();
}

if (hasKeyVault)
{
    builder.Services.AddSingleton(new SecretClient(new Uri(keyVaultUri!), credential));
    builder.Services.AddScoped<IKeyVaultService, KeyVaultService>();
}

if (hasServiceBus)
{
    builder.Services.AddSingleton(new ServiceBusClient(sbConnectionString!));
    builder.Services.AddScoped<IServiceBusService, ServiceBusService>();
}

if (hasEventGrid)
{
    if (!string.IsNullOrEmpty(egKey))
        builder.Services.AddSingleton(new EventGridPublisherClient(new Uri(egEndpoint!), new AzureKeyCredential(egKey)));
    else
        builder.Services.AddSingleton(new EventGridPublisherClient(new Uri(egEndpoint!), credential));
    builder.Services.AddScoped<IEventGridService, EventGridService>();
}

if (hasRedis)
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn!));
    builder.Services.AddScoped<ICacheService, CacheService>();
}

builder.Services.AddScoped<IAppConfigurationService, AppConfigurationService>();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHealthChecks("/health");

if (app.Services.GetService<ICosmosService>() is CosmosService cosmosService)
{
    try { await cosmosService.InitializeAsync(); }
    catch (Exception ex) { app.Logger.LogError(ex, "Cosmos DB init failed (expected without credentials)"); }
}

app.Run();

public partial class Program { }
