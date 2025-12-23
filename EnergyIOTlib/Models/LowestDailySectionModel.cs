using System.Text.Json.Serialization;

namespace EnergyIOT.Models;

public class LowestDailySection
{

    [JsonPropertyName("from")]
    public string id { get; set; }

    [JsonPropertyName("no_intervals")]
    public Int32 NoIntervals{ get; set; }


    [JsonPropertyName("avg_value_inc_vat")]
    public decimal AvgValueIncVat { get; set; }
}