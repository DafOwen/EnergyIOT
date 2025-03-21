using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using EnergyIOT.Models;
using EnergyIOT.Devices;

namespace EnergyIOT
{
    public class EnergyIOTMonthly
    {
        private readonly ILogger<EnergyIOTMonthly> _logger;
        private readonly IEnumerable<IDevices> _deviceGroupList;
        private EmailConfig _emailConfig;

        public EnergyIOTMonthly(ILogger<EnergyIOTMonthly> logger, IEnumerable<IDevices> devicesGroups)
        {
            _logger = logger;
            _deviceGroupList = devicesGroups;
        }


        [Function("EnergyIOTMonthly")]
        public void Run([TimerTrigger("0 15 0 1,15 * *")] TimerInfo myTimer)
        {
            _logger.LogInformation("C# Timer trigger function EnergyIOTMonthly executed at: {}", DateTime.Now);

            TimeZoneInfo homeTimeZone = TimeZoneInfo.FindSystemTimeZoneById(System.Environment.GetEnvironmentVariable("WEBSITE_TIME_ZONE"));

            #region GetConfiguration
            //Get CONFIGURATION------------------
            ConfigManagerFunction configManager;
            DatabaseConfig databaseConfig;


            try
            {
                configManager = new ConfigManagerFunction();
                databaseConfig = configManager.GetDatabaseConfig();
            }
            catch (Exception ex)
            {
                _logger.LogError("Config Manager Failure {Type} msg: {Msg}", ex.GetType(), ex.Message);
                return;
            }

            _emailConfig = configManager.GetEmailConfig();
            if (_emailConfig == null)
            {
                _logger.LogError($"EmailConfig Failure , emailConfig is null");
                return;
            }

            #endregion


            foreach (var device in _deviceGroupList)
            {

                DeviceAuthConfig deviceAuthConfig = configManager.GetDeviceAuthConfig(device.Name);
                if (deviceAuthConfig == null)
                {
                    _logger.LogError($"DeviceAuth Config Failure , {device.Name}AuthConfig is null");
                    return;
                }

                device.DataConfig(databaseConfig);

                try
                {
                    device.AuthenticateRefreshToken(deviceAuthConfig).GetAwaiter().GetResult();
                }
                catch (Exception err)
                {
                    _logger.LogError($"EnergyIOTMonthly Error: {err.Message}");
                    NotifyErrors("RefreshRoken", err.Message);

                    if (myTimer.ScheduleStatus is not null)
                    {
                        _logger.LogInformation("Next timer schedule at: {NextTime}", myTimer.ScheduleStatus.Next);
                    }
                }
            }


            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation("EnergyIOTMonthly: Next timer schedule at: {NextTime}", myTimer.ScheduleStatus.Next);
            }
        }


        private void NotifyErrors(string section, string errorMsg)
        {
            string subject = $"Octopus Energy IOT Failure: {section}";

            string message = "<br/>";
            message += "<br/>Error : " + errorMsg;

            SendEmail.SendEmailMsg(_emailConfig, _logger, subject, message);
        }


    }
}