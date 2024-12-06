using System.Text.Json.Serialization;

namespace EnergyIOTDataSetup.Models;

//--------------------COnfig Section--------------------------
public class DatabaseConfig
{
    public string EndpointURI { get; set; }
    public string DatabaseName { get; set; }
    public int? DatabaseRUMax { get; set; }
    public string PrimaryKey { get; set; }
    public string PriceCollection { get; set; }
    public string PricePartition { get; set; }
    public string TriggerCollection { get; set; }
    public string TriggerPartition { get; set; }
    public string ActionGroupCollection { get; set; }
    public string ActionGroupPartition { get; set; }
    public string OverrideCollection { get; set; }
    public string OverridePartition { get; set; }
    public string ConfigCollection { get; set; }
    public string ConfigPartition { get; set; }

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


//-----------------DB Config------------------------

internal class DBConfigString
{
    public string id { get; set; }

    public string Value { get; set; }

}

internal class PriceListColour
{
    public decimal From { get; set; }
    public decimal To { get; set; }
    public string Colour { get; set; }
}