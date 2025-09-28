using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using StackExchange.Redis;
using SkfProductAI.Infrastructure;
using SkfProductAI.Services;
using SkfProductAI.Functions;

var host = new HostBuilder()
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: true)
              .AddJsonFile("local.settings.json", optional: true)
              .AddEnvironmentVariables();
    })
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var cfg = context.Configuration;
        var endpoint = cfg["AZURE_OPENAI_ENDPOINT"];
        var key = cfg["AZURE_OPENAI_KEY"];
        var deployment = cfg["AZURE_OPENAI_DEPLOYMENT"] ?? "gpt-4o-mini";
        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(key))
        {
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(deployment, endpoint, key);
            services.AddSingleton(sp => builder.Build());
        }

        services.AddSingleton<IConnectionMultiplexer?>(_ =>
        {
            var conn = cfg["REDIS_CONNECTION"];
            if (string.IsNullOrWhiteSpace(conn)) return null; 
            return ConnectionMultiplexer.Connect(conn);
        });

        // Application services
        services.AddSingleton<InstructionContext>();
        services.AddSingleton<ProductCatalog>();
        services.AddSingleton<QueryHandler>();
    })
    .Build();

await host.RunAsync();
