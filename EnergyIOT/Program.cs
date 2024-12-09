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
        //services.AddKeyedSingleton<IDevices, TPLinkKasa>("Kasa");
        services.AddHttpClient();
        services.AddHttpClient("kasaAPI", x =>
        {
            x.DefaultRequestHeaders.Accept.Clear();
            x.DefaultRequestHeaders.Add("Accept", "application/json");
        });


    })

    .Build();

host.Run();
