using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EnergyIOT
{
    public class EnergyIOTMode
    {
        private readonly ILogger<EnergyIOTMode> _logger;

        public EnergyIOTMode(ILogger<EnergyIOTMode> logger)
        {
            _logger = logger;
        }

        [Function("EnergyIOTMode")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function EnergyIOTMode processed a request.");

            string? newMode = req.Query["mode"];
            if (string.IsNullOrEmpty(newMode))
            {
                return new BadRequestObjectResult("Mode Parameter empty");
            }
            else
            {
                newMode = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(newMode);
            }


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
                return new NotFoundObjectResult($"Config Manager Failure {ex.GetType()} msg: {ex.Message}");
            }

            DataStoreCosmoDB cosmosDBDataStore = new();
            cosmosDBDataStore.Config(databaseConfig);

            DBConfigString configString = new()
            {
                Value = newMode,
                id = "Mode"
            };

            try
            {
                cosmosDBDataStore.SetConfigString(configString);

                return new OkObjectResult("Mode changed to: " + newMode);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult("DB Config update error:" + ex.Message);
            }


        }
    }
}
