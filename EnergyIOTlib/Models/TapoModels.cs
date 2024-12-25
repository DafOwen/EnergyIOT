using System.Text.Json.Serialization;

namespace EnergyIOT.Models;

public class TapoTryAuthenticate
{
    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("params")]
    public TapoTryAuthenticateParams TapoAuthenticateParams { get; set; }
}

public class TapoTryAuthenticateParams
{
    [JsonPropertyName("appType")]
    public string AppType { get; set; }

    [JsonPropertyName("cloudUserName")]
    public string CloudUserName { get; set; }

    [JsonPropertyName("cloudPassword")]
    public string CloudPassword { get; set; }

    [JsonPropertyName("platform")]
    public string Platform { get; set; }

    [JsonPropertyName("terminalUUID")]
    public string TerminalUUID { get; set; }

    [JsonPropertyName("refreshTokenNeeded")]
    public bool RefreshTokenNeeded { get; set; }
}

public class TapoAuthenticateResponse
{
    [JsonPropertyName("error_code")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("result")]
    public TapoAuthenticateResult Result { get; set; }
}

public class TapoAuthenticateResult
{
    [JsonPropertyName("lockedMinutes")]
    public int LockedMinutes {  get; set; }

    [JsonPropertyName("failedAttempts")]
    public int FailedAttempts { get; set; }

    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; }

    [JsonPropertyName("remainAttempts")]
    public int RemainAttempts { get; set; }

    [JsonPropertyName("errorMsg")]
    public string ErrorMsg { get; set; }

    [JsonPropertyName("accountId")]
    public string AccountId { get; set; }

    [JsonPropertyName("regTime")]
    public string RegTime { get; set; }

    [JsonPropertyName("countryCode")]
    public string CountrCode { get; set; }

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



public class TapoAuthFailure
{
    [JsonPropertyName("error_code")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("msg")]
    public string Msg { get; set; }

}



public class TapoShadowsReported
{
    [JsonPropertyName("auto_off_remain_time")]
    public int AutoOffRemainTime { get; set; }

    [JsonPropertyName("power_protection_status")]
    public string PowerProtectionStatus { get; set; }

    [JsonPropertyName("auto_off_status")]
    public string AutoOffStatus { get; set; }

    [JsonPropertyName("charging_status")]
    public string ChargingStatus { get; set; }

    [JsonPropertyName("overcurrent_status")]
    public string OvercurrentStatus { get; set; }

    [JsonPropertyName("on")]
    public bool On { get; set; }
}

public class TapoShadowsDesired
{
    [JsonPropertyName("on")]
    public bool On { get; set; }
}

public class TapoShadowsState
{
    [JsonPropertyName("desired")]
    public TapoShadowsDesired Desired { get; set; }

    [JsonPropertyName("reported")]
    public TapoShadowsReported Reported { get; set; }
}

public class TapoShadows
{
    [JsonPropertyName("thingName")]
    public string ThingName { get; set; }

    [JsonPropertyName("state")]
    public TapoShadowsState State { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }
}

public class TaspoShadowsResponse
{
    [JsonPropertyName("shadows")]
    public TapoShadows[] Shadows { get; set; }

    [JsonPropertyName("failThingList")]
    public string[] FailThingList { get; set; } //unknown what kind of array
}

public class TaspoShadowsError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}

public class TapoPlugChange
{
    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("state")]
    public TapoPlugState State { get; set; }

}

public class TapoPlugState
{
    [JsonPropertyName("desired")]
    public TapoPlugDesired Desired { get; set; }
}

public class TapoPlugDesired
{
    [JsonPropertyName("on")]
    public bool On { get; set; }
}

public class TapoPlugErrorResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("data")]
    public TapoPlugErrorData Data { get; set; }
}

public class TapoPlugErrorData
{
    [JsonPropertyName("curVersion")]
    public int CurVersion { get; set; }

}