using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EnergyIOT.Models;
using EnergyIOT.DataAccess;
using EnergyIOT.Devices;

namespace EnergyIOT
{
    public class EnergyIOTPerPrice
    {
        private readonly ILogger<EnergyIOTPerPrice> _logger;
        private readonly IDataStore _dataStore;
        private readonly IEnumerable<IDevices> _devicesGroups;
        IHttpClientFactory _httpClientFactory;

        public EnergyIOTPerPrice(ILogger<EnergyIOTPerPrice> logger, IDataStore dataStore, 
                                IEnumerable<IDevices> devicesGroups, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _dataStore = dataStore;
            _devicesGroups = devicesGroups;
            _httpClientFactory = httpClientFactory;
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
            TriggerManager triggerManager = new(_logger, _dataStore, _devicesGroups);
            triggerManager.Trigger_PerPrice_Manager(emailConfig, mode).GetAwaiter().GetResult();


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
