using System.Text.Json.Serialization;

// Allow "id" property name - needed by Cosmos else swap tp Newtonsoft or custom Cosmos serializer
#pragma warning disable IDE1006 

namespace EnergyIOT.Models;
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

internal class EnergyAPIConfig
{
    public string BaseURI { get; set; }
    public string Section { get; set; }
    public string Product { get; set; }
    public string SubSection { get; set; }
    public string TariffCode { get; set; }
    public string EndSection { get; set; }
}

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
