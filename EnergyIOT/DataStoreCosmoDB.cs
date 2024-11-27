using Microsoft.Azure.Cosmos;
using System.Net;

namespace EnergyIOT
{
    internal class DataStoreCosmoDB : IDataStore
    {
        private DatabaseConfig _databaseConfig;

        public void Config(DatabaseConfig dbConfig)
        {
            _databaseConfig = dbConfig;
        }

        public async Task<EnergyPrice> GetPriceItemByDate(DateTime dateToCheckUTC)
        {
            CheckConfig();

            using CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey);

            //DB
            DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
            Database targetDatabase = databaseResponse.Database;

            //Container
            Microsoft.Azure.Cosmos.Container priceContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.PriceCollection, _databaseConfig.PricePartition);

            //read item to see if exist
            EnergyPrice testPrice = new();

            //COnvert date to strings
            string dateToCheckUTCstring = dateToCheckUTC.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");

            testPrice.id = dateToCheckUTCstring;

            //see if price in DB for date-time
            try
            {
                ItemResponse<EnergyPrice> priceResponse = await priceContainer.ReadItemAsync<EnergyPrice>(testPrice.id, new PartitionKey(testPrice.id));
                //Found
                return priceResponse;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<bool> SavePriceItems(UnitRates unitRates)
        {
            CheckConfig();

            //CosmoDB RU counters to avoid maxing it
            Int32 costTotal = 0;
            DateTime? countTimer = null, startTimer = null;

            using CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey);

            //DB
            DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
            Database targetDatabase = databaseResponse.Database;

            //Container
            Microsoft.Azure.Cosmos.Container priceContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.PriceCollection, _databaseConfig.PricePartition);

            //Db unit Prices
            if (startTimer == null)
            {
                startTimer = DateTime.Now;
            }

            foreach (EnergyPrice priceItem in unitRates.Results)
            {
                try
                {
                    ItemResponse<EnergyPrice> unitPriceResponse = await priceContainer.ReadItemAsync<EnergyPrice>(priceItem.id, new PartitionKey(priceItem.id));
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    ItemResponse<EnergyPrice> unitPriceResponse = await priceContainer.CreateItemAsync<EnergyPrice>(priceItem, new PartitionKey(priceItem.id));

                    costTotal += (int)unitPriceResponse.RequestCharge;
                }

                //sleep if 0.8sec and above DatabaseRUMax (400) RU cost
                countTimer = DateTime.Now;
                if (countTimer > startTimer.Value.AddMilliseconds(800)
                    && costTotal > _databaseConfig.DatabaseRUMax)
                {
                    Thread.Sleep(400);
                    costTotal = 0;
                    startTimer = DateTime.Now;
                }

            }//EnergyPrice

            return true;
        }

        public async Task<List<Trigger>> GetHourlyTriggersSorted()
        {
            CheckConfig();

            List<Trigger> sortedTriggerList = [];

            using (CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey))
            {

                //DB
                DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
                Database targetDatabase = databaseResponse.Database;

                Microsoft.Azure.Cosmos.Container triggerContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.TriggerCollection, _databaseConfig.TriggerParition);

                using FeedIterator<Trigger> triggerFeed = triggerContainer.GetItemQueryIterator<Trigger>(queryText: "select * from c WHERE c.Interval = 'Hourly' AND c.Active = true ");

                //triggerFeed.
                List<Trigger> triggers = [];
                while (triggerFeed.HasMoreResults)
                {
                    FeedResponse<Trigger> response = await triggerFeed.ReadNextAsync();
                    // Iterate query results
                    foreach (Trigger triggerItem in response)
                    {
                        triggers.Add(triggerItem);

                    }
                }

                //sort Trigger List
                sortedTriggerList = triggers.OrderBy(o => o.Order).ToList();

            }

            return sortedTriggerList;
        }

        public async Task<List<Trigger>> GetPerPriceTriggers(string mode)
        {
            CheckConfig();

            List<Trigger> sortedTriggerList = [];

            using (CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey))
            {
                //DB
                DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
                Database targetDatabase = databaseResponse.Database;

                Microsoft.Azure.Cosmos.Container triggerContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.TriggerCollection, _databaseConfig.TriggerParition);

                using FeedIterator<Trigger> triggerFeed = triggerContainer.GetItemQueryIterator<Trigger>(queryText: string.Format("select * from c WHERE c.Interval = 'PerPrice' AND ARRAY_CONTAINS(c.Modes,{{'Mode': '{0}', 'Active': true}}, true) ", mode));

                //triggerFeed.
                List<Trigger> triggers = [];

                while (triggerFeed.HasMoreResults)
                {
                    FeedResponse<Trigger> response = await triggerFeed.ReadNextAsync();

                    // Iterate query results
                    foreach (Trigger triggerItem in response)
                    {
                        triggers.Add(triggerItem);
                    }
                }

                sortedTriggerList = triggers.OrderBy(o => o.Order).ToList();

            }

            return sortedTriggerList;
        }

        public async Task<List<EnergyPrice>> GetDateSpanPrices(DateTime dateFrom, DateTime dateTo)
        {
            List<EnergyPrice> energyPrices = [];

            using (CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey))
            {
                //DB
                DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
                Database targetDatabase = databaseResponse.Database;

                //Container
                Microsoft.Azure.Cosmos.Container priceContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.PriceCollection, _databaseConfig.PricePartition);

                QueryDefinition queryDefinition = new QueryDefinition(
                    "select * from c where c.id >= @dateFrom and c.ValidTo <= @dateTo ")
                        .WithParameter("@dateFrom", dateFrom)
                        .WithParameter("@dateTo", dateTo);

                FeedIterator<EnergyPrice> queryResultSetIterator = priceContainer.GetItemQueryIterator<EnergyPrice>(queryDefinition);

                FeedResponse<EnergyPrice> currentResultSet = await queryResultSetIterator.ReadNextAsync();

                //Sort
                energyPrices = currentResultSet.OrderBy(p => p.id).ToList();

            }

            return energyPrices;
        }

        public async Task<List<ActionGroup>> GetActionGroups(List<int> actionGroupIDs)
        {
            //simpler to get all action groups then filter to those in actionGroupIDs

            List<ActionGroup> actionGroups = [];

            using (CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey))
            {
                //DB
                DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
                Database targetDatabase = databaseResponse.Database;

                //Container
                Microsoft.Azure.Cosmos.Container actionGroupContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.ActionGroupCollection, _databaseConfig.ActionGroupParition);
                PartitionKey partitionKey = new(_databaseConfig.ActionGroupParition);

                //list of Tuples
                List<(string, PartitionKey)> itemsToFind = [];

                foreach (int groupID in actionGroupIDs)
                {
                    itemsToFind.Add((groupID.ToString(), partitionKey));
                }


                //Fetch multiple
                FeedResponse<ActionGroup> feedResponse = await actionGroupContainer.ReadManyItemsAsync<ActionGroup>(
                    items: itemsToFind
                );

                actionGroups = feedResponse.ToList();
            }

            return actionGroups;
        }

        public async Task<ActionGroup> GetActionGroup(string actionGroupID)
        {
            ItemResponse<ActionGroup> actionGroup;

            using CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey);
            //DB
            DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
            Database targetDatabase = databaseResponse.Database;

            //Container
            Microsoft.Azure.Cosmos.Container actionGroupContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.ActionGroupCollection, _databaseConfig.ActionGroupParition);
            PartitionKey partitionKey = new(_databaseConfig.ActionGroupParition);

            try
            {
                actionGroup = await actionGroupContainer.ReadItemAsync<ActionGroup>(actionGroupID, new PartitionKey(actionGroupID));
                return actionGroup;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                //log later
                return null;
            }
        }

        public async Task SetActionGroupToken(string actionGroupID, string newToken)
        {
            using CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey);

            //DB
            DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
            Database targetDatabase = databaseResponse.Database;

            //Container
            Microsoft.Azure.Cosmos.Container actionGroupContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.ActionGroupCollection, _databaseConfig.ActionGroupParition);
            PartitionKey partitionKey = new(_databaseConfig.ActionGroupParition);

            //Update ActionGroup
            List<PatchOperation> operations =
             [
                PatchOperation.Replace("/Token", newToken),
                PatchOperation.Replace("/LastUpdated", DateTime.Now)
             ];

            ItemResponse<ActionGroup> responseUpdate = await actionGroupContainer.PatchItemAsync<ActionGroup>(
                id: actionGroupID,
                new PartitionKey(actionGroupID),
                patchOperations: operations
            );

        }

        internal void CheckConfig()
        {
            if (_databaseConfig == null)
            {
                throw new ArgumentNullException("_databaseCOnfig is null in CosmosDBDataStore");
            }
        }

        public async Task OverrideInsertUpdate(OverrideTrigger overrideItem)
        {
            using CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey);

            //DB
            DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
            Database targetDatabase = databaseResponse.Database;

            //Container
            Microsoft.Azure.Cosmos.Container overrideContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.OverrideCollection, _databaseConfig.OverrideParition);

            try
            {
                ItemResponse<OverrideTrigger> unitPriceResponse = await overrideContainer.ReadItemAsync<OverrideTrigger>(overrideItem.id, new PartitionKey(overrideItem.id));
                //replace
                await overrideContainer.UpsertItemAsync<OverrideTrigger>(overrideItem);

            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                ItemResponse<OverrideTrigger> unitPriceResponse = await overrideContainer.CreateItemAsync<OverrideTrigger>(overrideItem, new PartitionKey(overrideItem.id));
            }
        }

        public async Task<OverrideTrigger> GetOverride(string idStartDate)
        {
            using CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey);

            //DB
            DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
            Database targetDatabase = databaseResponse.Database;

            //Container
            Microsoft.Azure.Cosmos.Container overrideContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.OverrideCollection, _databaseConfig.OverrideParition);

            OverrideTrigger overrideItem = new();

            try
            {
                var parameterizedQuery = new QueryDefinition(
                query: "SELECT * FROM ot WHERE ot.id <= @currentDateTimeUTC AND ot.EndDate >= @currentDateTimeUTC"
            )
                .WithParameter("@currentDateTimeUTC", idStartDate);

                //overrideItem = await overrideContainer.ReadItemAsync<OverrideTrigger>(idStartDate, new PartitionKey(idStartDate));
                using FeedIterator<OverrideTrigger> filteredFeed = overrideContainer.GetItemQueryIterator<OverrideTrigger>(
                    queryDefinition: parameterizedQuery
                );

                FeedResponse<OverrideTrigger> responseOne = await filteredFeed.ReadNextAsync();
                if (responseOne.StatusCode != HttpStatusCode.OK)
                {
                    //tnot log here
                    throw new Exception("OverrideTrigger DataStore call failed");
                }
                else
                {
                    return responseOne.Resource.FirstOrDefault();
                }

            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                //no error log - might not exist
                return null;
            }

        }

        public async Task<DBConfigString> GetConfigString(string configName)
        {
            CheckConfig();

            using (CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey))
            {
                DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
                Database targetDatabase = databaseResponse.Database;

                Microsoft.Azure.Cosmos.Container configContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.ConfigCollection, _databaseConfig.ConfigPartition);

                DBConfigString dbconfigString = new();

                try
                {
                    var parameterizedQuery = new QueryDefinition(
                    query: "SELECT * FROM cfg WHERE cfg.id = @configName"
                )
                    .WithParameter("@configName", configName);

                    //overrideItem = await overrideContainer.ReadItemAsync<OverrideTrigger>(idStartDate, new PartitionKey(idStartDate));
                    using FeedIterator<DBConfigString> filteredFeed = configContainer.GetItemQueryIterator<DBConfigString>(
                        queryDefinition: parameterizedQuery
                    );

                    FeedResponse<DBConfigString> responseOne = await filteredFeed.ReadNextAsync();
                    if (responseOne.StatusCode != HttpStatusCode.OK)
                    {
                        //not log here
                        throw new Exception("COnfig DataStore call failed");
                    }
                    else
                    {
                        return responseOne.Resource.FirstOrDefault();
                    }

                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    //no error log - might not exist
                    return null;
                }
            }

        }

        public async Task SetConfigString(DBConfigString configString)
        {
            CheckConfig();

            using CosmosClient client = new(_databaseConfig.EndpointURI, _databaseConfig.PrimaryKey);

            //DB
            DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_databaseConfig.DatabaseName);
            Database targetDatabase = databaseResponse.Database;

            //Container
            Microsoft.Azure.Cosmos.Container configContainer = await targetDatabase.CreateContainerIfNotExistsAsync(_databaseConfig.ConfigCollection, _databaseConfig.ConfigPartition);

            PartitionKey partitionKey = new(_databaseConfig.ConfigPartition);

            //Update ActionGroup
            List<PatchOperation> operations =
             [
                PatchOperation.Replace("/Value", configString.Value)
             ];

            ItemResponse<ActionGroup> responseUpdate = await configContainer.PatchItemAsync<ActionGroup>(
                id: configString.id,
                new PartitionKey(configString.id),
                patchOperations: operations
            );

            return;
        }

    }
}