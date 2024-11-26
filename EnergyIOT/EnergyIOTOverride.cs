using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EnergyIOT
{
    public class EnergyIOTOverride(ILogger<EnergyIOTOverride> logger)
    {
        private readonly ILogger<EnergyIOTOverride> _logger = logger;

        private string _startDateTimeStrUTC;
        private DateTime _startDateTimeUTC;
        private string _endDateTimeStrUTC;
        private DateTime _endDateTimeUTC;

        [Function("EnergyIOTOverride")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function EnergyIOTOverride processed a request.");

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
                return new NotFoundObjectResult($"Config Manager Failure {ex.GetType()} msg: {ex.Message}");
            }

            EmailConfig emailConfig = configManager.GetEmailConfig();
            if (emailConfig == null)
            {
                _logger.LogError("EmailConfig Failure , emailConfig is null");
                return new NotFoundObjectResult($"EmailConfig Failure , emailConfig is null");
            }
#endregion

#region DataStore/DB
            DataStoreCosmoDB cosmosDBDataStore = new();
            cosmosDBDataStore.Config(databaseConfig);
#endregion


            //start parameter
            string? startStringInput = req.Query["start"];
            if (string.IsNullOrEmpty(startStringInput))
            {
                _logger.LogError("start parameter missing");
                return new BadRequestObjectResult("start parameter missing");
            }
            else if (startStringInput.Equals("NOW", StringComparison.CurrentCultureIgnoreCase))
            {
                DateTime nowUTC = DateTime.UtcNow;
                SetStartDateTime(nowUTC);
            }
            else
            {
                //check input is valid
                //todo - test parse with no mm:ss
                bool isValidDate = DateTime.TryParse(startStringInput, out _startDateTimeUTC);
                if (!isValidDate)
                {
                    _logger.LogError("start DateTime not valid");
                    return new BadRequestObjectResult("start DateTime not valid");
                }
                //Convert to UTC
                _startDateTimeUTC = _startDateTimeUTC.ToUniversalTime();
                _startDateTimeStrUTC = _startDateTimeUTC.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");
            }

            //interval prameter
            int intervalInt;
            string? intervalString = req.Query["interval"];
            if (string.IsNullOrEmpty(intervalString))
            {
                _logger.LogError("interval parameter missing");
                return new BadRequestObjectResult("interval parameter missing");
            }
            else
            {
                //check if integer
                bool isNumeric = int.TryParse(intervalString, out intervalInt);
                if (!isNumeric)
                {
                    _logger.LogError("interval not integer: {interval}", intervalString);
                    return new BadRequestObjectResult("interval not integer");
                }
            }

            //calculate end time
            _endDateTimeUTC = _startDateTimeUTC.AddMinutes(30 * intervalInt);
            _endDateTimeStrUTC = _endDateTimeUTC.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");

            //Insert/Update ovveride

            //if exist for id (starttime)- replace
            OverrideTrigger overrideEntry = new()
            {
                id = _startDateTimeStrUTC, //UTC
                EndDate = _endDateTimeStrUTC, //UTC
                Interval = intervalInt,
                Updated = DateTime.Now //Local time
            };

            //insert/replace
            cosmosDBDataStore.OverrideInsertUpdate(overrideEntry);

            return new OkObjectResult("Override Inserted");
        }

        internal void SetStartDateTime(DateTime passedDateTimeStart)
        {
            //get 00 or 30
            if (passedDateTimeStart.Minute < 30)
            {
                //if now mins 0-29 - set min to 00
                _startDateTimeUTC = new DateTime(DateOnly.FromDateTime(passedDateTimeStart), new TimeOnly(passedDateTimeStart.Hour, 0, 0));
            }
            else
            {
                //if now mins 30-59 - set min to 30
                _startDateTimeUTC = new DateTime(DateOnly.FromDateTime(passedDateTimeStart), new TimeOnly(passedDateTimeStart.Hour, 30, 0));
            }

            _startDateTimeStrUTC = _startDateTimeUTC.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");
        }

        internal bool ValidateInputDate(string inputDate)
        {
            if (string.IsNullOrEmpty(inputDate))
            { return false; }
            else
            { return true; }
        }

    }
}
