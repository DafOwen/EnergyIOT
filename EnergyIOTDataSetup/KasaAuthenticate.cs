using System.Text;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;

namespace EnergyIOTDataSetup
{
    public class KasaAuthenticate
    {
        private readonly HttpClient _clientKasaAPI;
        private readonly ServiceProvider serviceProvider;
        private readonly KasaAuthConfig _kasaAuthConfig;

        /// <summary>
        /// Constructor - fetch Config values and set up IHttpClientFactiry
        /// </summary>
        public KasaAuthenticate()
        {
            //Config
            var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false);

            var config = builder.Build();
            _kasaAuthConfig = config.GetSection("KasaAuthConfig").Get<KasaAuthConfig>();

            //HttpClient
            _clientKasaAPI = new HttpClient();
            _clientKasaAPI.DefaultRequestHeaders.Accept.Clear();
            _clientKasaAPI.DefaultRequestHeaders.Add("Accept", "application/json");
        }


        /// <summary>
        /// First time authentication from Kasa
        /// - NEED 2FA OFF FIRST
        /// </summary>
        /// <param Name="myDBConfig">Database config</param>
        /// <returns></returns>
        public async Task AuthenticateKasaFirst(DatabaseConfig myDBConfig)
        {
            //clientKasaAPI = _httpClientFactory.CreateClient("clientKasaAuthAPI");
            KasaTryAuthenticateParams kasaAuthParams = new()
            {
                AppType = _kasaAuthConfig.AppType,
                TerminalUUID = _kasaAuthConfig.TerminalUUID,
                CloudUserName = _kasaAuthConfig.CloudUserName,
                CloudPassword = _kasaAuthConfig.CloudPassword,
                RefreshTokenNeeded = _kasaAuthConfig.RefreshTokenNeeded
            };

            KasaTryAuthenticate kasaTryAuthenticate = new()
            {
                KasaAuthenticateParams = kasaAuthParams,
                Method = "login"
            };

            var serializeOptions = new JsonSerializerOptions
            {
                //WriteIndented = true
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string stringcontent = System.Text.Json.JsonSerializer.Serialize(kasaTryAuthenticate, serializeOptions);
            var content = new StringContent(stringcontent, Encoding.UTF8, "application/json");

            try
            {
                var result = await _clientKasaAPI.PostAsync(new Uri(_kasaAuthConfig.BaseURI), content);

                if (result.StatusCode != HttpStatusCode.OK)
                {
                    Console.WriteLine("AuthenticateKasaFirst Status Code not Ok : " + result.StatusCode.ToString());
                    return;
                }

                string responseBody = await result.Content.ReadAsStringAsync();

                KasaAuthenticated returnObject = System.Text.Json.JsonSerializer.Deserialize<KasaAuthenticated>(responseBody);

                if (returnObject.ErrorCode != 0)
                {
                    //Log error or retry ?
                    Console.WriteLine("AuthenticateKasaFirst - Error code not 0 : " + returnObject.ErrorCode.ToString());
                    Console.WriteLine("Error : " + returnObject.Msg);
                    return;
                }

                if (returnObject.Result.Token == null)
                {
                    Console.WriteLine("AuthenticateKasaFirst - Kasa returned token null");
                    return;
                }

                ActionGroup kasaGroup = new()
                {
                    id = "1",
                    GroupName = "Kasa",
                    BaseURL = "https://eu-wap.tplinkcloud.com",
                    Token = returnObject.Result.Token,
                    TerminalUUID = _kasaAuthConfig.TerminalUUID,
                    RefreshToken = returnObject.Result.RefreshToken,
                    LastUpdated = DateTime.Now
                };

                using (CosmosClient client = new CosmosClient(myDBConfig.EndpointURI, myDBConfig.PrimaryKey))
                {
                    //DB
                    DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(myDBConfig.DatabaseName);
                    Database targetDatabase = databaseResponse.Database;

                    //Container - actionGroup
                    Microsoft.Azure.Cosmos.Container actionGroupContainer = await targetDatabase.CreateContainerIfNotExistsAsync("ActionGroup", "/id");

                    try
                    {
                        ItemResponse<ActionGroup> actionGroup = await actionGroupContainer.ReadItemAsync<ActionGroup>(kasaGroup.id, new PartitionKey(kasaGroup.id));
                    }
                    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        ItemResponse<ActionGroup> actionGroup = await actionGroupContainer.CreateItemAsync<ActionGroup>(kasaGroup, new PartitionKey(kasaGroup.id));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw ex;
            }

        }

        /// <summary>
        /// Refresh Authentication Token 
        /// - get current actiongroup (refresh Token)
        /// - call Kasa API
        /// - Save new Token to database
        /// </summary>
        /// <param Name="myDBConfig">Database Config</param>
        /// <returns></returns>
        public async Task AuthenticateKasaRefresh(DatabaseConfig myDBConfig)
        {
            //Steps:
            //get current Token from ActionGroup
            //call refresh api
            //Update with new Token

            using (CosmosClient client = new CosmosClient(myDBConfig.EndpointURI, myDBConfig.PrimaryKey))
            {
                //DB
                DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(myDBConfig.DatabaseName);
                Database targetDatabase = databaseResponse.Database;

                //Container - actionGroup
                Microsoft.Azure.Cosmos.Container actionGroupContainer = await targetDatabase.CreateContainerIfNotExistsAsync("ActionGroup", "/id");

                ActionGroup kasaGroup = new()
                {
                    id = "1"
                };

                ItemResponse<ActionGroup> actionGroup;
                //ActionGroup actionGroupFetch;

                try
                {
                    actionGroup = await actionGroupContainer.ReadItemAsync<ActionGroup>(kasaGroup.id, new PartitionKey(kasaGroup.id));
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine("Kasa Group ID 1 not found!");
                    return;
                }

                // Call Kasa refresh Token api
                KasaAuthRefreshParams kasaAuthRefreshParams = new()
                {
                    AppType = _kasaAuthConfig.AppType,
                    TerminalUUID = _kasaAuthConfig.TerminalUUID,
                    RefreshToken = actionGroup.Resource.RefreshToken
                };

                KasaAuthRefresh kasaAuthRefresh = new()
                {
                    Method = "refreshToken",
                    Kasarefreshparams = kasaAuthRefreshParams
                };

                var serializeOptions = new JsonSerializerOptions
                {
                    //WriteIndented = true
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string stringcontent = System.Text.Json.JsonSerializer.Serialize(kasaAuthRefresh, serializeOptions);
                var content = new StringContent(stringcontent, Encoding.UTF8, "application/json");

                //Call Refresh API
                try
                {
                    var result = await _clientKasaAPI.PostAsync(new Uri(_kasaAuthConfig.BaseURI), content);

                    //check
                    if (result.StatusCode != HttpStatusCode.OK)
                    {
                        Console.WriteLine("AuthenticateKasaRefresh Status Code not Ok : {0}", result.StatusCode.ToString());
                        return;
                    }

                    string responseBody = await result.Content.ReadAsStringAsync();

                    KasaAuthRefreshReturn returnKasa = System.Text.Json.JsonSerializer.Deserialize<KasaAuthRefreshReturn>(responseBody);

                    if (returnKasa.ErrorCode > 0)
                    {
                        string msg = "";
                        if (returnKasa.Msg != null) { msg = returnKasa.Msg; }

                        Console.WriteLine("AuthenticateKasaRefresh Kasa err_code : {0} Msg : {1}", returnKasa.ErrorCode, msg);
                        return;
                    }

                    //Update ActionGroup

                    List<PatchOperation> operations =
                    [
                        PatchOperation.Replace("/Token", returnKasa.Result.Token),
                        PatchOperation.Replace("/LastUpdated", DateTime.Now)
                    ];

                    ItemResponse<ActionGroup> responseUpdate = await actionGroupContainer.PatchItemAsync<ActionGroup>(
                        id: kasaGroup.id,
                        new PartitionKey(kasaGroup.id),
                        patchOperations: operations
                    );

                    if (responseUpdate.StatusCode != HttpStatusCode.OK)
                    {
                        Console.WriteLine("Kasa Auth refresh - Ststus Not Ok : {0}", responseUpdate.StatusCode);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("AuthenticateKasaRefresh clientKasaAPI.PostAsync Exception : {0}", ex.Message);
                    return;
                }

            }


        }
    }
}

