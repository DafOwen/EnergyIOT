using System.Text.Json.Serialization;

// Allow "id" property name - needed by Cosmos else swap tp Newtonsoft or custom Cosmos serializer
#pragma warning disable IDE1006 

namespace EnergyIOT.Models;

public class Trigger
{
    public string id { get; set; }
    public string Name { get; set; }
    public string Interval { get; set; }
    public string Type { get; set; }
    public int Order { get; set; }
    public bool Active { get; set; }
    public decimal? Value { get; set; }
    public List<Action> Actions { get; set; }

    public List<ModesItem> Modes { get; set; }
}

public class Action
{
    public string ItemId { get; set; }
    public string GroupName { get; set; }
    public string ItemName { get; set; }
    public int GroupId { get; set; }
    public string DeviceId { get; set; }
    public string Type { get; set; }
    public int StateTo { get; set; }
}

public class ModesItem
{
    public string Mode { get; set; }

    public bool Active { get; set; }
}
public class ActionGroup
{
    public string id { get; set; }
    public string GroupName { get; set; }
    public string BaseURL { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
    public string TerminalUUID { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class ActionFailure
{
    public string ItemId { get; set; }
    public string ItemName { get; set; }
    public string TriggerName { get; set; }
    public string FailureDatetime { get; set; }
    public string Message { get; set; }
}


//--------------------Overide-------------------------

public class OverrideTrigger
{
    //id = startdate/time in UTC
    public string id { get; set; }

    //endDateTime in UTC
    public string EndDate { get; set; }

    public int Interval { get; set; }

    //updated - just for records - local
    public DateTime Updated { get; set; }
}