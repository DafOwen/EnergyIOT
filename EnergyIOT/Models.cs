using System.Text.Json.Serialization;

// Allow "id" property name - needed by Cosmos else swap tp Newtonsoft or custom Cosmos serializer
#pragma warning disable IDE1006 

namespace EnergyIOT
{
    //---------------------Config Sections----------------------
    internal class DatabaseConfig
    {
        public string EndpointURI { get; set; }
        public string DatabaseName { get; set; }
        public int DatabaseRUMax { get; set; }
        public string PrimaryKey { get; set; }
        public string PriceCollection { get; set; }
        public string PricePartition { get; set; }
        public string TriggerCollection { get; set; }
        public string TriggerParition { get; set; }
        public string ActionGroupCollection { get; set; }
        public string ActionGroupParition { get; set; }
        public string OverrideCollection { get; set; }
        public string OverrideParition { get; set; }
    }

    internal class EmailConfig
    {
        public string Server { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public int Port { get; set; }
        public bool SSL { get; set; }
        public string Pwd { get; set; }
        public string Username { get; set; }
    }

    internal class EnergyAPIConfig
    {
        public string BaseURI { get; set; }
        public string Section { get; set; }
        public string Product { get; set; }
        public string SubSection { get; set; }
        public string TariffCode { get; set; }
        public string EndSection {get; set;}
    }

    internal class KasaAuthConfig
    {
        public string BaseURI { get; set; }
        public string Method { get; set; }
        public string AppType { get; set; }
        public string CloudUserName { get; set; }
        public string CloudPassword { get; set; }
        public string TerminalUUID { get; set; }
        public bool RefreshTokenNeeded { get; set; }
    }

    //-------------------Energy Prices - DB and Units--------------------------------

    internal class EnergyPrice
    {

        [JsonPropertyName("valid_from")]
        public string id { get; set; }

        [JsonPropertyName("valid_to")]
        public string ValidTo { get; set; }

        [JsonPropertyName("value_exc_vat")]
        public decimal ValueExcVat { get; set; }

        [JsonPropertyName("value_inc_vat")]
        public decimal ValueIncVat { get; set; }
    }

    
    internal class UnitRates
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("next")]
        public string Next { get; set; }

        [JsonPropertyName("previous")]
        public string Previous { get; set; }

        [JsonPropertyName("results")]
        public List<EnergyPrice> Results { get; set; }
    }

    //--------------------Overide-------------------------

    internal class OverrideTrigger
    {
        //id = startdate/time in UTC
        public string id { get; set; }

        //endDateTime in UTC
        public string EndDate { get; set; }

        public int Interval { get; set; }

        //updated - just for records - local
        public DateTime Updated { get; set; }
    }


    //--------------------Triggers and Actions-----------------------

    internal class Trigger
    {
        public string id { get; set; }
        public string Name { get; set; }
        public string Interval { get; set; }
        public string Type { get; set; }
        public int Order { get; set; }
        public decimal? Value { get; set; }
        public List<Action> Actions { get; set; }

        public List<ModesItem> Modes { get; set; }
    }

    internal class Action
    {
        public string ItemId { get; set; }
        public string GroupName { get; set; }
        public string ItemName { get; set; }
        public int GroupId { get; set; }
        public string DeviceId { get; set; }
        public string Type { get; set; }
        public int StateTo {get; set; }
    }

    internal class ModesItem
    {
        public string Mode { get; set; }

        public bool Active { get; set; }
    }
    internal class ActionGroup
    {
        public string id { get; set; }
        public string GroupName { get; set; }
        public string BaseURL { get; set; }
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public string TerminalUUID { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    internal class ActionFailure
    {
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public string TriggerName { get; set; }
        public string FailureDatetime { get; set; }
        public string Message { get; set; }
    }

    //---------------------------KASA------------------------------


    //-----------Kasa authenticate----------------

    //Used for 1st authentication - not in this project, instead EnergyIOTDataSetup
    internal class KasaTryAuthenticate
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("params")]
        public KasaTryAuthenticateParams KasaAuthenticateParams { get; set; }
    }

    internal class KasaTryAuthenticateParams
    {
        [JsonPropertyName("appType")]
        public string AppType { get; set; }

        [JsonPropertyName("cloudUserName")]
        public string CloudUserName { get; set; }

        [JsonPropertyName("cloudPassword")]
        public string CloudPassword { get; set; }

        [JsonPropertyName("terminalUUID")]
        public string TerminalUUID { get; set; }

        [JsonPropertyName("refreshTokenNeeded")]
        public bool RefreshTokenNeeded { get; set; }
    }

    internal class KasaAuthenticated
    {
        [JsonPropertyName("error_code")]
        public int ErrorCode { get; set; }

        //error msg
        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        [JsonPropertyName("result")]
        public KasaAuthenticatedResult Result { get; set; }
    }
    internal class KasaAuthenticatedResult
    {
        [JsonPropertyName("accountId")]
        public string AccountId { get; set; }

        [JsonPropertyName("regTime")]
        public string RegTime { get; set; }

        [JsonPropertyName("countryCode")]
        public string CountryCode { get; set; }

        [JsonPropertyName("riskDetected")]
        public int RiskDetected { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; }
    }


    //---------------KASA Refresh----------

    internal class KasaAuthRefresh
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("params")]
        public KasaAuthRefreshParams Kasarefreshparams { get; set; }
    }

    internal class KasaAuthRefreshParams
    {
        [JsonPropertyName("appType")]
        public string AppType { get; set; }

        [JsonPropertyName("terminalUUID")]
        public string TerminalUUID { get; set; }

        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; }
    }

    internal class KasaAuthRefreshReturnResult
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    internal class KasaAuthRefreshReturn
    {
        [JsonPropertyName("error_code")]
        public int ErrorCode { get; set; }

        //msg on error
        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        //return on successful
        [JsonPropertyName("result")]
        public KasaAuthRefreshReturnResult Result { get; set; }
    }

    //-------------Kasa Actions -----------------
    internal class KasaPassthrough
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("params")]
        public KasaParams Params { get; set; }
    }
   
    internal class KasaParams
    {
        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; }

        [JsonPropertyName("requestData")]
        public string RequestData
        {
            get { return System.Text.Json.JsonSerializer.Serialize(RequesDataObj); }
            set { RequesDataObj = System.Text.Json.JsonSerializer.Deserialize<KasaRequestData>(value); ; }
        }

        [JsonPropertyName("_requesDataObj")]
        private KasaRequestData RequesDataObj;

        [JsonIgnore]
        public KasaRequestData RequestDataObj
        {
            get { return RequesDataObj; }
            set { RequesDataObj = value; }
        }
    }

    internal class KasaRequestData
    {
        [JsonPropertyName("system")]
        public KasaRequestSystem System { get; set; }
    }

    internal class KasaRequestSystem
    {
        [JsonPropertyName("set_relay_state")]
        public KasaRequestRelayState SetRelayState { get; set; }
    }

    internal class KasaRequestRelayState
    {
        [JsonPropertyName("state")]
        public int State { get; set; }
    }

    internal class KasaReturn
    {
        [JsonPropertyName("error_code")]
        public int ErrorCode { get; set; }

        //msg on error
        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        //result on OK
        [JsonPropertyName("result")]
        public KasaReturnResponseData Result { get; set; }
    }

    internal class KasaReturnResponseData
    {
        [JsonPropertyName("responseData")]
        public string ResponseData { get; set; }

        [JsonPropertyName("responseDataObj")]
        public KasaReturnResponseDataObj ResponseDataObj
        {
            get { return System.Text.Json.JsonSerializer.Deserialize<KasaReturnResponseDataObj>(ResponseData); }
        }
    }

    internal class KasaReturnResponseDataObj
    {
        [JsonPropertyName("system")]
        public KasaReturnResponseSystem System { get; set; }
    }

    internal class KasaReturnResponseSystem
    {
        [JsonPropertyName("set_relay_state")]
        public KasaReturnErrCOde SetRelayState { get; set; }
    }

    internal class KasaReturnErrCOde
    {
        [JsonPropertyName("err_code")]
        public int ErrorCode { get; set; }
    }

    internal class KasaDevices
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }
    }

    internal class PriceListColour
    {
        public decimal From { get; set; }
        public decimal To { get; set; }
        public string Colour { get; set; }
    }
}
