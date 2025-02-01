using System.Collections;
using EnergyIOT.Models;

namespace EnergyIOT
{
    public class ConfigManagerFunction
    {
        private readonly DatabaseConfig _DatabaseConfig;
        private EmailConfig _EmailConfig;
        private readonly EnergyAPIConfig _EnergyAPIConfig;

        public ConfigManagerFunction()
        {
            //Get + Store database COnfig
            _DatabaseConfig = new DatabaseConfig
            {
                EndpointURI = GetEnvAriableStr("Database_EndpointURI"),
                DatabaseName = GetEnvAriableStr("Database_DatabaseName"),
                DatabaseRUMax = GetEnvAriableInt("Database_DatabaseRUMax"),
                PrimaryKey = GetEnvAriableStr("Database_PrimaryKey"),
                PriceCollection = GetEnvAriableStr("Database_PriceCollection"),
                PricePartition = GetEnvAriableStr("Database_PricePartition"),
                TriggerCollection = GetEnvAriableStr("Database_TriggerCollection"),
                TriggerPartition = GetEnvAriableStr("Database_TriggerPartition"),
                ActionGroupCollection = GetEnvAriableStr("Database_ActionGroupCollection"),
                ActionGroupPartition = GetEnvAriableStr("Database_ActionGroupPartition"),
                OverrideCollection = GetEnvAriableStr("Database_OverrideCollection"),
                OverridePartition = GetEnvAriableStr("Database_OverridePartition"),
                ConfigCollection = GetEnvAriableStr("Database_ConfigCollection"),
                ConfigPartition = GetEnvAriableStr("Database_ConfigPartition")
            };

            ////Get EnergyAPIConfig
            _EnergyAPIConfig = new EnergyAPIConfig
            {
                BaseURI = GetEnvAriableStr("EnergyAPI_BaseURI"),
                Section = GetEnvAriableStr("EnergyAPI_Section"),
                Product = GetEnvAriableStr("EnergyAPI_Product"),
                SubSection = GetEnvAriableStr("EnergyAPI_SubSection"),
                TariffCode = GetEnvAriableStr("EnergyAPI_TariffCode"),
                EndSection = GetEnvAriableStr("EnergyAPI_EndSection")
            };

        }

        public DatabaseConfig GetDatabaseConfig() { return _DatabaseConfig; }

        public EnergyAPIConfig GetEnergyAPIConfig() { return _EnergyAPIConfig; }

        public EmailConfig GetEmailConfig()
        {
            _EmailConfig ??= new EmailConfig();

            _EmailConfig.From = GetEnvAriableStr("Email_From");
            _EmailConfig.To = GetEnvAriableStr("Email_To");
            _EmailConfig.Server = GetEnvAriableStr("Email_Server");
            _EmailConfig.Port = GetEnvAriableInt("Email_Port");
            _EmailConfig.SSL = GetEnvAriableBool("Email_SSL");
            _EmailConfig.Pwd = GetEnvAriableStr("Email_Pwd");
            _EmailConfig.Username = GetEnvAriableStr("Email_Username");

            return _EmailConfig;
        }

        public DeviceAuthConfig GetDeviceAuthConfig(string deviceGroupName)
        {
            DeviceAuthConfig deviceAuthConfig = new DeviceAuthConfig();

            deviceAuthConfig.AppType = GetEnvAriableStr(deviceGroupName+"Auth_appType");
            deviceAuthConfig.Method = GetEnvAriableStr(deviceGroupName+"Auth_method");
            deviceAuthConfig.AuthURI = GetEnvAriableStr(deviceGroupName+"Auth_AuthURI");
            deviceAuthConfig.DeviceURI = GetEnvAriableStr(deviceGroupName+"Auth_DeviceURI");
            deviceAuthConfig.CloudUserName = GetEnvAriableStr(deviceGroupName+"Auth_cloudUserName");
            deviceAuthConfig.CloudPassword = GetEnvAriableStr(deviceGroupName+"Auth_cloudPassword");
            deviceAuthConfig.TerminalUUID = GetEnvAriableStr(deviceGroupName+"Auth_terminalUUID");
            deviceAuthConfig.RefreshTokenNeeded = GetEnvAriableBool(deviceGroupName+"Auth_refreshTokenNeeded");

            return deviceAuthConfig;
        }

        public RetryConfig GetRetryConfig()
        {
            RetryConfig retryConfig = new RetryConfig();

            try
            {
                retryConfig.Count = GetEnvAriableInt("PlugRetry_Count");
                retryConfig.TimeMs = GetEnvAriableInt("PlugRetry_TimeMs");
            }catch(Exception Err)
            {
                retryConfig.Count = 0;
                retryConfig.TimeMs = 0;
            }

            return retryConfig;
        }

        public string GetEnvAriableStr(string parameterName)
        {
            string newVar = System.Environment.GetEnvironmentVariable(parameterName);
            if (newVar == null)
            { throw new ArgumentNullException(parameterName); }
            else
            {
                return newVar;
            }
        }

        public int GetEnvAriableInt(string parameterName)
        {
            var newVar = System.Environment.GetEnvironmentVariable(parameterName);
            if (newVar == null)
            { throw new ArgumentNullException(parameterName); }
            else
            {
                if (int.TryParse(newVar.ToString(), out int returnInt))
                { return returnInt; }
                else { throw new ArgumentException(parameterName); }
            }
        }

        public bool GetEnvAriableBool(string parameterName)
        {
            var newVar = System.Environment.GetEnvironmentVariable(parameterName);
            if (newVar == null)
            { throw new ArgumentNullException(parameterName); }
            else
            {
                if (bool.TryParse(newVar.ToString(), out bool returnBool))
                { return returnBool; }
                else { throw new ArgumentException(parameterName); }
            }
        }

        public List<PriceListColour> GetPricelistColours()
        {
            //can't search - get all variables
            var envVariablesDic = Environment.GetEnvironmentVariables();

            //filter and parse
            //Couldn't find a suitable Linq/Cast expression
            List<PriceListColour> listColours = [];

            string key = "", value = "";
            string keyParsed = "";

            decimal decFrom, decTo;

            //Get PriceColour_ entries andget values
            foreach (DictionaryEntry entry in envVariablesDic)
            {

                key = (string)entry.Key;
                if (key.StartsWith("PriceColour_"))
                {
                    keyParsed = key.Replace("PriceColour_", "");
                    decFrom = decimal.Parse(keyParsed.Substring(0, keyParsed.IndexOf(':')));
                    decTo = decimal.Parse(keyParsed.Substring(keyParsed.IndexOf(':') + 1, keyParsed.Length - (keyParsed.IndexOf(':') + 1)));

                    listColours.Add(new PriceListColour { From = decFrom, To = decTo, Colour = entry.Value.ToString() });
                }
            }

            //sort
            if (listColours.Count > 0)
            {
                listColours = listColours.OrderBy(c => c.From).ToList();
            }
            else
            {
                //log error in calling function - but not critical.
            }

            return listColours;

        }
    }
}