using System.Text.Json.Serialization;

namespace EnergyIOT.Models;


//-----------Kasa authenticate----------------

//Used for 1st authentication - not in this project, instead EnergyIOTDataSetup
public class KasaTryAuthenticate
{
    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("params")]
    public KasaTryAuthenticateParams KasaAuthenticateParams { get; set; }
}

public class KasaTryAuthenticateParams
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

public class KasaAuthenticated
{
    [JsonPropertyName("error_code")]
    public int ErrorCode { get; set; }

    //error msg
    [JsonPropertyName("msg")]
    public string Msg { get; set; }

    [JsonPropertyName("result")]
    public KasaAuthenticatedResult Result { get; set; }
}
public class KasaAuthenticatedResult
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

public class KasaAuthRefresh
{
    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("params")]
    public KasaAuthRefreshParams Kasarefreshparams { get; set; }
}

public class KasaAuthRefreshParams
{
    [JsonPropertyName("appType")]
    public string AppType { get; set; }

    [JsonPropertyName("terminalUUID")]
    public string TerminalUUID { get; set; }

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; }
}

public class KasaAuthRefreshReturnResult
{
    [JsonPropertyName("token")]
    public string Token { get; set; }
}

public class KasaAuthRefreshReturn
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
public class KasaPassthrough
{
    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("params")]
    public KasaParams Params { get; set; }
}

public class KasaParams
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

public class KasaRequestData
{
    [JsonPropertyName("system")]
    public KasaRequestSystem System { get; set; }
}

public class KasaRequestSystem
{
    [JsonPropertyName("set_relay_state")]
    public KasaRequestRelayState SetRelayState { get; set; }
}

public class KasaRequestRelayState
{
    [JsonPropertyName("state")]
    public int State { get; set; }
}

public class KasaReturn
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

public class KasaReturnResponseData
{
    [JsonPropertyName("responseData")]
    public string ResponseData { get; set; }

    [JsonPropertyName("responseDataObj")]
    public KasaReturnResponseDataObj ResponseDataObj
    {
        get { return System.Text.Json.JsonSerializer.Deserialize<KasaReturnResponseDataObj>(ResponseData); }
    }
}

public class KasaReturnResponseDataObj
{
    [JsonPropertyName("system")]
    public KasaReturnResponseSystem System { get; set; }
}

public class KasaReturnResponseSystem
{
    [JsonPropertyName("set_relay_state")]
    public KasaReturnErrCOde SetRelayState { get; set; }
}

public class KasaReturnErrCOde
{
    [JsonPropertyName("err_code")]
    public int ErrorCode { get; set; }
}

public class KasaDevices
{
    [JsonPropertyName("method")]
    public string Method { get; set; }
}