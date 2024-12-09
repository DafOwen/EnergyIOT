using EnergyIOT.Models;
using Action = EnergyIOT.Models.Action;

namespace EnergyIOT.Devices;
public interface IDevices
{
    string Name { get; }
    public void DataConfig(DatabaseConfig databaseConfig);
    public string DeviceGroupName();
    public Task AuthenticateFirst(DeviceAuthConfig _deviceAuthConfig);
    public Task AuthenticateRefreshToken(DeviceAuthConfig _deviceAuthConfig);

    public Task<List<ActionFailure>> Plug(ActionGroup actionGroup, Action actionItem, string triggerName);
}

