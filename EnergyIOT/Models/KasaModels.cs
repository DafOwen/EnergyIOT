using System.Text.Json.Serialization;

namespace EnergyIOT.Models;


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