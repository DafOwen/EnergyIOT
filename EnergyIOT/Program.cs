using EnergyIOT.DataAccess;
using EnergyIOT.Devices;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddTransient<IDataStore, DataStoreCosmoDB>();
        services.AddTransient<IDevices, TPLinkKasa>();
        services.AddTransient<IDevices, TPLinkTapo>();
        services.AddHttpClient();

        services.AddHttpClient("kasaAPI", x =>
        {
            x.DefaultRequestHeaders.Accept.Clear();
            x.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient("tapoAPI", x =>
        {
            x.DefaultRequestHeaders.Accept.Clear();
            x.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            // Allowing Untrusted SSL Certificates
            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, cetChain, policyErrors) => true;

            return handler;
        });


    })

    .Build();

host.Run();