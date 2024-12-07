using System.Text.Json.Serialization;

namespace EnergyIOT.Models;

public class EnergyPrice
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


public class UnitRates
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
