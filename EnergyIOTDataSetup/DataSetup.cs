using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Cosmos;
using System.Configuration;

namespace EnergyIOTDataSetup
{
    public class DataSetup
    {
        private static string _startDateTimeStrUTC;
        private static DateTime _startDateTimeUTC;
        private static string _endDateTimeStrUTC;
        private static DateTime _endDateTimeUTC;
        private static DatabaseConfig myDBConfig;

        public static async Task CreateCosmosContainers()
        {

            //Config
            var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false);

            var config = builder.Build();
            myDBConfig = config.GetSection("DatabaseConfig").Get<DatabaseConfig>();

            if (myDBConfig == null )
            {
                throw new ConfigurationErrorsException("DBConfig missing data");
            }

            using (CosmosClient client = new CosmosClient(myDBConfig.EndpointURI, myDBConfig.PrimaryKey))
            {
                //DB
                DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(myDBConfig.DatabaseName, myDBConfig.DatabaseRUMax);
                Database targetDatabase = databaseResponse.Database;

                ContainerResponse responseActionGroup = await targetDatabase.CreateContainerIfNotExistsAsync(
                id: myDBConfig.ActionGroupCollection,
                partitionKeyPath: myDBConfig.ActionGroupParition
                );

                ContainerResponse responseEnergyPrices = await targetDatabase.CreateContainerIfNotExistsAsync(
                id: myDBConfig.PriceCollection,
                partitionKeyPath: myDBConfig.PricePartition
                );

                ContainerResponse responseTriggers = await targetDatabase.CreateContainerIfNotExistsAsync(
                id: myDBConfig.TriggerCollection,
                partitionKeyPath: myDBConfig.TriggerParition
                );
            }
        }

        /// <summary>
        /// Create containers
        /// Populate the basics : Triggers, Actions 
        /// NOTE : ActionGroup is not done here - instead by KasaAuthenticate
        /// </summary>
        /// <param name="myDBConfig"></param>
        /// <returns></returns>
        public static async Task PopulateCosmosBasics(DatabaseConfig myDBConfig)
        {
            //Trigger + Action data import
            IConfigurationBuilder dataImportBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("DataImport.json", optional: false);

            IConfigurationRoot dataConfig = dataImportBuilder.Build();

            List<TriggerImport> triggerImportList = dataConfig.GetSection("TriggersForDb").Get<List<TriggerImport>>();
            List<Action> actionImportList = dataConfig.GetSection("ActionsForDb").Get<List<Action>>();


            using (CosmosClient client = new CosmosClient(myDBConfig.EndpointURI, myDBConfig.PrimaryKey))
            {
                //DB
                DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(myDBConfig.DatabaseName, myDBConfig.DatabaseRUMax);
                Database targetDatabase = databaseResponse.Database;

                //Container - actionGroup
                Microsoft.Azure.Cosmos.Container actionGroupContainer = await targetDatabase.CreateContainerIfNotExistsAsync(myDBConfig.ActionGroupCollection, myDBConfig.ActionGroupParition);

                //Container - Trigger
                Microsoft.Azure.Cosmos.Container triggerContainer = await targetDatabase.CreateContainerIfNotExistsAsync(myDBConfig.TriggerCollection, myDBConfig.TriggerParition);

                //Container - EnergyPrices
                Microsoft.Azure.Cosmos.Container energyPricesContainer = await targetDatabase.CreateContainerIfNotExistsAsync(myDBConfig.PriceCollection, myDBConfig.PricePartition);

                //Container - Overide
                Microsoft.Azure.Cosmos.Container overideCollection = await targetDatabase.CreateContainerIfNotExistsAsync(myDBConfig.OverrideCollection, myDBConfig.OverrideParition);


                //Convert the TriggerImport list + Action list to List<Trigger>
                List<Trigger> triggerList = new();
                foreach (TriggerImport importTrigger in triggerImportList)
                {
                    Trigger newTrigger = new()
                    {
                        id = importTrigger.id,
                        Name = importTrigger.Name,
                        Interval = importTrigger.Interval,
                        Type = importTrigger.Type,
                        Order = importTrigger.Order,
                        Value = importTrigger.Value,
                        Modes = importTrigger.Modes
                    };

                    List<Action> actionsList = new();

                    foreach (int actionId in importTrigger.ActionIDs)
                    {
                        actionsList.Add(actionImportList.Single(a => a.ItemId == actionId.ToString()));
                    }

                    newTrigger.Actions = actionsList;

                    triggerList.Add(newTrigger);
                }


                //Insert Triggers (+actions)
                foreach (Trigger triggerItem in triggerList)
                {
                    try
                    {
                        ItemResponse<Trigger> triggerResponse = await triggerContainer.ReadItemAsync<Trigger>(triggerItem.id, new PartitionKey(triggerItem.id));
                    }
                    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        ItemResponse<Trigger> triggerResponse = await triggerContainer.CreateItemAsync<Trigger>(triggerItem, new PartitionKey(triggerItem.id));
                    }

                }//foreach


            }//using (CosmosClient client
        }


        public static async Task TestOverridInsertAndSearch()
        {
            //Config
            var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false);

            var config = builder.Build();
            var myDBConfig = config.GetSection("DatabaseConfig").Get<DatabaseConfig>();

            if (myDBConfig == null)
            {
                return;
            }

            OverrideTrigger overrideItem = new OverrideTrigger();

            //"Now" start
            //Interval = 4
            int interval = 6;

            //TEST start - Now - 1 hr
            DateTime startUTC = DateTime.UtcNow.AddHours(-1);
            SetStartDateTime(startUTC);

            //calculate end time
            _endDateTimeUTC = _startDateTimeUTC.AddMinutes(30 * interval);
            _endDateTimeStrUTC = _endDateTimeUTC.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");

            overrideItem.id = _startDateTimeStrUTC;
            overrideItem.EndDate = _endDateTimeStrUTC;
            overrideItem.Interval = interval;
            overrideItem.Updated = DateTime.Now;


            //Test insert
            //then test search
            try
            {
                using (CosmosClient client = new CosmosClient(myDBConfig.EndpointURI, myDBConfig.PrimaryKey))
                {
                    //DB
                    DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(myDBConfig.DatabaseName);
                    Database targetDatabase = databaseResponse.Database;

                    //Container - overrideTrigger
                    Microsoft.Azure.Cosmos.Container overrideContainer = await targetDatabase.CreateContainerIfNotExistsAsync(myDBConfig.OverrideCollection, myDBConfig.OverrideParition);

                    try
                    {
                        ItemResponse<OverrideTrigger> overrideTestResponse = await overrideContainer.ReadItemAsync<OverrideTrigger>(overrideItem.id, new PartitionKey(overrideItem.id));
                        //replace
                        await overrideContainer.UpsertItemAsync<OverrideTrigger>(overrideItem);
                    }
                    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        ItemResponse<OverrideTrigger> overrideTestResponse = await overrideContainer.CreateItemAsync<OverrideTrigger>(overrideItem, new PartitionKey(overrideItem.id));
                    }

                    //Now trysearching
                    DateTime nowUTC = DateTime.UtcNow;
                    string nowUTCStr = nowUTC.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");

                    var parameterizedQuery = new QueryDefinition(
                            query: "SELECT * FROM ot WHERE ot.id <= @currentDateTime AND ot.EndDate >= @currentDateTime"
                        )
                            .WithParameter("@currentDateTime", nowUTCStr);


                    // Query multiple items from container
                    using FeedIterator<OverrideTrigger> filteredFeed = overrideContainer.GetItemQueryIterator<OverrideTrigger>(
                        queryDefinition: parameterizedQuery
                    );

                    // Iterate query Result pages
                    while (filteredFeed.HasMoreResults)
                    {
                        FeedResponse<OverrideTrigger> response = await filteredFeed.ReadNextAsync();

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            Console.WriteLine("Error");
                            throw new Exception("Error test");
                        }

                        // Iterate query results
                        foreach (OverrideTrigger item in response)
                        {
                            Console.WriteLine($"Found item:\t{item.id}");
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
            }
        }

        internal static void SetStartDateTime(DateTime passedDateTimeStart)
        {
            //get 00 or 30
            if (passedDateTimeStart.Minute < 30)
            {
                //if now mins 0-29 - set min to 00
                _startDateTimeUTC = new DateTime(DateOnly.FromDateTime(passedDateTimeStart), new TimeOnly(passedDateTimeStart.Hour, 0, 0));
            }
            else
            {
                //if now mins 30-59 - set min to 30
                _startDateTimeUTC = new DateTime(DateOnly.FromDateTime(passedDateTimeStart), new TimeOnly(passedDateTimeStart.Hour, 30, 0));
            }

            _startDateTimeStrUTC = _startDateTimeUTC.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");
        }

    }
}
