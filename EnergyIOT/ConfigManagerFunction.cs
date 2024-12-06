using System.Collections;
using EnergyIOT.Models;

namespace EnergyIOT
{
    internal class ConfigManagerFunction
    {
        private readonly DatabaseConfig _DatabaseConfig;
        private EmailConfig _EmailConfig;
        private readonly EnergyAPIConfig _EnergyAPIConfig;
        private KasaAuthConfig _KasaAuthConfig;


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
                TriggerParition = GetEnvAriableStr("Database_TriggerParition"),
                ActionGroupCollection = GetEnvAriableStr("Database_ActionGroupCollection"),
                ActionGroupParition = GetEnvAriableStr("Database_ActionGroupParition"),
                OverrideCollection = GetEnvAriableStr("Database_OverrideCollection"),
                OverrideParition = GetEnvAriableStr("Database_OverrideParition"),
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


        public KasaAuthConfig GetKasaAuthConfig()
        {
            _KasaAuthConfig ??= new KasaAuthConfig();

            _KasaAuthConfig.AppType = GetEnvAriableStr("KasaAuth_appType");
            _KasaAuthConfig.Method = GetEnvAriableStr("KasaAuth_method");
            _KasaAuthConfig.BaseURI = GetEnvAriableStr("KasaAuth_BaseURI");
            _KasaAuthConfig.CloudUserName = GetEnvAriableStr("KasaAuth_cloudUserName");
            _KasaAuthConfig.CloudUserName = GetEnvAriableStr("KasaAuth_cloudPassword");
            _KasaAuthConfig.TerminalUUID = GetEnvAriableStr("KasaAuth_terminalUUID");
            _KasaAuthConfig.RefreshTokenNeeded = GetEnvAriableBool("KasaAuth_refreshTokenNeeded");

            return _KasaAuthConfig;
        }

        private string GetEnvAriableStr(string parameterName)
        {
            string newVar = System.Environment.GetEnvironmentVariable(parameterName);
            if (newVar == null)
            { throw new ArgumentNullException(parameterName); }
            else
            {
                return newVar;
            }
        }

        private int GetEnvAriableInt(string parameterName)
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

        private bool GetEnvAriableBool(string parameterName)
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
