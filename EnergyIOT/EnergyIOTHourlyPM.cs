using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EnergyIOT
{
    public class EnergyIOTHourlyPM(ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<EnergyIOTHourlyPM>();
        private static ServiceProvider serviceProvider;
        private  int utcEndHour = 0;

        [Function("EnergyIOTHourlyPM")]
        public void Run([TimerTrigger("0 0 16-22 * * *")] TimerInfo myTimer)
        {

            _logger.LogInformation("C# Timer trigger EnergyIOTHourlyPM executed at: {DateTime.Now}",DateTime.Now);

            TimeZoneInfo homeTimeZone = TimeZoneInfo.FindSystemTimeZoneById(System.Environment.GetEnvironmentVariable("WEBSITE_TIME_ZONE"));

 #region GetConfiguration
            //Get CONFIGURATION------------------
            ConfigManagerFunction configManager;
            EnergyAPIConfig energyAPIConfig;
            DatabaseConfig databaseConfig;

            try
            {
                configManager = new ConfigManagerFunction();
                energyAPIConfig = configManager.GetEnergyAPIConfig();
                databaseConfig = configManager.GetDatabaseConfig();
            }
            catch (Exception ex)
            {
                _logger.LogError("Config Manager Failure {Type} msg: {Message}", ex.GetType(), ex.Message);
                return;
            }

            EmailConfig emailConfig = configManager.GetEmailConfig();
            if (emailConfig == null)
            {
                _logger.LogError($"EmailConfig Failure , emailConfig is null");
                return;

            }

            //Get colour codes for list email - non critical
            List<PriceListColour> pricelistColours =  configManager.GetPricelistColours();
            if(pricelistColours?.Count == 0)
            {
                _logger.LogError("Price List Colour Env Variables not found");
                //no need to exit, non critical
            }

            #endregion

#region IHttpClientFactory
            //Set up IHttpClientFactory ----------------------

            //Get HttpCLient for Injections
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient("clientEnergyAPI", x =>
            {
                x.BaseAddress = new Uri(energyAPIConfig.BaseURI);
                x.DefaultRequestHeaders.Accept.Clear();
                x.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            serviceCollection.BuildServiceProvider();

            serviceProvider = serviceCollection.BuildServiceProvider();

            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

#endregion

#region DataStore/DB
            DataStoreCosmoDB cosmosDBDataStore = new();
            cosmosDBDataStore.Config(databaseConfig);
#endregion

            UnitRates unitRates = GetAndSavePrices(cosmosDBDataStore, energyAPIConfig).GetAwaiter().GetResult();

            if (unitRates != null)
            {
                //Hourly Triggers
                TriggerManager triggerManager = new(_logger);
 
                triggerManager.Trigger_Hourly_Manager(cosmosDBDataStore, emailConfig, unitRates, pricelistColours);
            }

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation("Next timer schedule at: {NextTime}", myTimer.ScheduleStatus.Next);
            }

        }

        int DateParameterHour()
        {
            //End hour is UK 23 - but BST/GMT changes.
            //Get UTC of current version
            if (utcEndHour == 0)
            {
                //Get hour to - varies with BST/GMT
                DateTime ukEnd = new(DateOnly.FromDateTime(DateTime.Now), new TimeOnly(23, 0, 0));
                DateTime utcEnd = ukEnd.ToUniversalTime();
                utcEndHour = utcEnd.Hour;
                return utcEndHour;
            }
            else
            {
                return utcEndHour;
            }

        }

        string DateParameter()
        {
            //Get UTC period_from + period_to
            string periodfrom = new DateTime(DateOnly.FromDateTime(DateTime.Now), 
                                            new TimeOnly(DateParameterHour(), 0, 0))
                                            .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");

            string periodto = new DateTime(DateOnly.FromDateTime(DateTime.Now.AddDays(1)), 
                                            new TimeOnly(DateParameterHour(), 0, 0))
                                            .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");

            return "period_from=" + periodfrom + "&period_to=" + periodto;
        }

        async Task<UnitRates> GetAndSavePrices(IDataStore dataStore, EnergyAPIConfig energyConfig)
        {
            //test if price exists for next day
            DateTime lastDate = new(DateOnly.FromDateTime(DateTime.Now), new TimeOnly(DateParameterHour(), 00, 0));
            lastDate = lastDate.AddDays(1).AddMinutes(-30); //tomorrow, -30 for start time not end

            //var gotDataTest = TestDataAlreadyGot(dbConfig);
            EnergyPrice priceResponse = await dataStore.GetPriceItemByDate(lastDate);

            if (priceResponse != null)
            {
                _logger.LogInformation("TestDataAlreadyGot - already got result");
                return null;
            }


            //EnergyAPI stage
            var unitRates = await CallEnergyAPIAsync(energyConfig);

            //check to see if got 48 prices
            if(unitRates == null || unitRates.Results.Count == 0)
            {
                _logger.LogInformation("GetPrices - returned 0 results");
                return null;
            }
            else if (unitRates.Results.Count < 48)
            {
                _logger.LogInformation("GetPrices - got results but not 48 :{noresults}", unitRates.Results.Count.ToString());
                return null;
            }

            //Save results
            bool pricesSaved = await dataStore.SavePriceItems(unitRates);
            

            if (!pricesSaved)
            {
                _logger.LogError("SaveEnergyPrices returned false - prices not saved");
            }

            return unitRates;
        }

        async Task<UnitRates> CallEnergyAPIAsync(EnergyAPIConfig energyAPI)
        {
            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

            if (httpClientFactory == null)
            {
                _logger.LogError("CallEnergyAPIAsync : httpClientFactory is NULL");
                return null;
            }

            var client = httpClientFactory.CreateClient("clientEnergyAPI");

            string endpointURI = energyAPI.Section
                    + energyAPI.Product 
                    + energyAPI.SubSection
                    + energyAPI.TariffCode
                    + energyAPI.EndSection
                    + "?" + DateParameter();

            HttpResponseMessage response = await client.GetAsync(endpointURI);

            UnitRates unitRates = new();
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                unitRates = JsonSerializer.Deserialize<UnitRates>(jsonString);
                return unitRates;
            }

            return unitRates;
        }

    }
}