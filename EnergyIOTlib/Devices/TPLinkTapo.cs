using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using EnergyIOT.DataAccess;
using EnergyIOT.Models;
using Action = EnergyIOT.Models.Action;

namespace EnergyIOT.Devices
{
    internal class TPLinkTapo : IDevices
    {
        IHttpClientFactory _httpClientFactory;
        IDataStore _dataStore;
        DatabaseConfig _databaseConfig;
        ActionGroup _actionGroup;
        private List<ActionFailure> actionFailures;

        public string Name => "Tapo";

        public void DataConfig(DatabaseConfig databaseConfig)
        {
            _databaseConfig = databaseConfig;
            _dataStore.Config(_databaseConfig);
        }

        public Task AuthenticateFirst(DeviceAuthConfig _deviceAuthConfig)
        {
            throw new NotImplementedException();
        }

        public Task AuthenticateRefreshToken(DeviceAuthConfig _deviceAuthConfig)
        {
            throw new NotImplementedException();
        }

        public string DeviceGroupName()
        {
            throw new NotImplementedException();
        }

        public Task<List<ActionFailure>> Plug(ActionGroup actionGroup, Models.Action actionItem, string triggerName)
        {
            throw new NotImplementedException();
        }
    }
}
