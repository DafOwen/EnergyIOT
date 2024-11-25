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

        [Function("Function")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");


            string currentMode = Environment.GetEnvironmentVariable("EnergyIOTMode");
            if (string.IsNullOrEmpty(currentMode))
            {
                currentMode = "Default";
            }


            string? newMode = req.Query["mode"];

            if (string.IsNullOrEmpty(newMode))
            {
                return new BadRequestObjectResult("mode Parameter empty");

            }
            else
            {
                newMode = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(newMode);
            }

            if (newMode == currentMode)
            {
                return new OkObjectResult("Mode already :" + currentMode);
            }

            Environment.SetEnvironmentVariable("EnergyIOTMode", newMode);

            return new OkObjectResult("Mode set to:" + newMode);
        }
    }
}
