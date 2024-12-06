using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text;
using System.Net;
using EnergyIOT.Models;
using EnergyIOT.DataAccess;

namespace EnergyIOT
{
    public class EnergyIOTMonthly(ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<EnergyIOTMonthly>();
        private static ServiceProvider serviceProvider;
        private EmailConfig _emailConfig;

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

            KasaAuthConfig kasaAuthConfig = configManager.GetKasaAuthConfig();
            if (kasaAuthConfig == null)
            {
                _logger.LogError($"KasaAuth Config Failure , kasaAuthConfig is null");
                return;
            }

            #endregion

            #region IHttpClientFactory
            //Set up IHttpClientFactory ----------------------

            //Get HttpCLient for Injections
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient("kasaAuthAPI", x =>
            {
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

            try
            {
                //Get Kasa ActionGroup
                UpdateRefreshKasaToken(cosmosDBDataStore, kasaAuthConfig).GetAwaiter().GetResult();
            }
            catch (Exception err)
            {
                _logger.LogError($"EnergyIOTMonthly Error: {err.Message}");
                NotifyErrors("UpdateRefreshKasaToken", err.Message);

                if (myTimer.ScheduleStatus is not null)
                {
                    _logger.LogInformation("Next timer schedule at: {NextTime}", myTimer.ScheduleStatus.Next);
                }
            }


            async Task UpdateRefreshKasaToken(IDataStore dataStore, KasaAuthConfig kasaAuthConfig)
            {
                //Valid httpCLient
                var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

                if (httpClientFactory == null)
                {
                    _logger.LogError("GetRefreshKasaToken : httpClientFactory is NULL");
                    throw new Exception("GetRefreshKasaToken : httpClientFactory is NULL");
                }

                var kasaClient = httpClientFactory.CreateClient("kasaAuthAPI");


                //get orig ActionGroup from DB
                ActionGroup actionGroup = await dataStore.GetActionGroup("1");
                if (actionGroup == null)
                {
                    _logger.LogError("UpdateRefreshKasaToken: Kasa Action Group not found");
                    throw new Exception("UpdateRefreshKasaToken: Kasa Action Group not found");
                }


                // Call Kasa refresh token api
                KasaAuthRefreshParams kasaAuthRefreshParams = new()
                {
                    AppType = kasaAuthConfig.AppType,
                    TerminalUUID = kasaAuthConfig.TerminalUUID,
                    RefreshToken = actionGroup.RefreshToken
                };

                KasaAuthRefresh kasaAuthRefresh = new()
                {
                    Method = "refreshToken",
                    Kasarefreshparams = kasaAuthRefreshParams
                };

                JsonSerializerOptions serializeOptions = new()
                {
                    //WriteIndented = true
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string stringcontent = System.Text.Json.JsonSerializer.Serialize(kasaAuthRefresh, serializeOptions);
                var content = new StringContent(stringcontent, Encoding.UTF8, "application/json");


                //Call Refresh API
                try
                {
                    var result = await kasaClient.PostAsync(new Uri(kasaAuthConfig.BaseURI), content);

                    //check
                    if (result.StatusCode != HttpStatusCode.OK)
                    {
                        _logger.LogError("AuthenticateKasaRefresh Status Code not Ok : {statuscode}", result.StatusCode.ToString());
                        throw new Exception("AuthenticateKasaRefresh Status Code not Ok");
                    }

                    string responseBody = await result.Content.ReadAsStringAsync();

                    KasaAuthRefreshReturn returnKasa = System.Text.Json.JsonSerializer.Deserialize<KasaAuthRefreshReturn>(responseBody);

                    if (returnKasa.ErrorCode > 0)
                    {
                        string msg = "";
                        if (returnKasa.Msg != null) { msg = returnKasa.Msg; }

                        _logger.LogError("AuthenticateKasaRefresh Kasa err_code : {error_code} Msg : {message}", returnKasa.ErrorCode, msg);
                        throw new Exception("AuthenticateKasaRefresh Kasa err_code " + returnKasa.ErrorCode + " Msg : " + msg);
                    }

                    //Update 
                    dataStore.SetActionGroupToken(actionGroup.id, returnKasa.Result.Token);

                }
                catch (Exception ex)
                {
                    _logger.LogError("AuthenticateKasaRefresh clientKasaAPI.PostAsync Exception : {msg}", ex.Message);
                    throw new Exception("AuthenticateKasaRefresh clientKasaAPI.PostAsync Exception : " + ex.Message);
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

            SendEmail.SendEmailMsg( _emailConfig, _logger, subject, message);
        }


    }
}
