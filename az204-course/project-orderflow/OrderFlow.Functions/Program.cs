using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Storage.Blobs;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        var env = context.HostingEnvironment;
        config.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        var credential = new DefaultAzureCredential();

        services.AddSingleton(new CosmosClient(
            accountEndpoint: configuration["CosmosDb:AccountEndpoint"]!,
            authKeyOrResourceToken: configuration["CosmosDb:AccountKey"],
            clientOptions: new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            }));

        var blobConnectionString = configuration["BlobStorage:ConnectionString"];
        if (!string.IsNullOrEmpty(blobConnectionString))
            services.AddSingleton(new BlobServiceClient(blobConnectionString));
        else
        {
            var accountName = configuration["BlobStorage:AccountName"];
            if (!string.IsNullOrEmpty(accountName))
                services.AddSingleton(new BlobServiceClient(
                    new Uri($"https://{accountName}.blob.core.windows.net"), credential));
        }
    })
    .Build();

host.Run();
