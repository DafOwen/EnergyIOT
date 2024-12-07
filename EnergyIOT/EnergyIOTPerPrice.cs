using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EnergyIOT.Models;
using EnergyIOT.DataAccess;

namespace EnergyIOT
{
    public class EnergyIOTPerPrice
    {
        private readonly ILogger<EnergyIOTPerPrice> _logger;
        private readonly IDataStore _dataStore;
        private static ServiceProvider serviceProvider;

        public EnergyIOTPerPrice(ILogger<EnergyIOTPerPrice> logger, IDataStore dataStore)
        {
            _logger = logger;
            _dataStore = dataStore;
        }

        [Function("EnergyIOTPerPrice")]
        public void Run([TimerTrigger("0 */30 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation("C# Timer trigger function EnergyIOTPerPrice executed at: {Now}", DateTime.Now);

            #region GetConfiguration
            ConfigManagerFunction configManager;

            try
            {
                configManager = new ConfigManagerFunction();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to run {Type} msg: {Msg}", ex.GetType(), ex.Message);
                return;
            }

            EnergyAPIConfig energyAPIConfig = configManager.GetEnergyAPIConfig();
            DatabaseConfig databaseConfig = configManager.GetDatabaseConfig();

            EmailConfig emailConfig = configManager.GetEmailConfig();
            if (emailConfig == null)
            {
                _logger.LogError("EmailConfig Failure , emailConfig is null");
                return;

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
            serviceCollection.AddHttpClient("clientKasaPlugAPI", x =>
            {
                x.DefaultRequestHeaders.Accept.Clear();
                x.DefaultRequestHeaders.Add("Accept", "application/json");
            });
            serviceCollection.BuildServiceProvider();
            serviceProvider = serviceCollection.BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

            #endregion

            #region DataStore/DB
            _dataStore.Config(databaseConfig);
            #endregion

            //Check Override
            OverrideTrigger overrideTrigger = CheckForOverride().GetAwaiter().GetResult();

            if (overrideTrigger != null)
            {
                _logger.LogInformation("Override found - exiting EnergyIOTPerPrice");
                return;
            }

            //Mode
            string mode = "";
            DBConfigString dbConfig = _dataStore.GetConfigString("Mode").GetAwaiter().GetResult();
            if (dbConfig != null)
            {
                mode = dbConfig.Value;
            }
            else
            {
                mode = "Default";
            }

            //Call Trigger Manager
            TriggerManager triggerManager = new(_logger, _dataStore);
            triggerManager.Trigger_PerPrice_Manager( httpClientFactory, emailConfig, mode).GetAwaiter().GetResult();


            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation("EnergyIOTPerPrice: Next timer schedule at: {NextTime}", myTimer.ScheduleStatus.Next);
            }
        }


        internal async Task<OverrideTrigger> CheckForOverride()
        {
            //Get current datetime in str
            string strNowDateUtcShort = DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");

            OverrideTrigger overrideTrigger = await _dataStore.GetOverride(strNowDateUtcShort);

            return overrideTrigger;
        }

    }
}
