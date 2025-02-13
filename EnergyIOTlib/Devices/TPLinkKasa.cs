using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnergyIOT.DataAccess;
using EnergyIOT.Models;
using Action = EnergyIOT.Models.Action;

namespace EnergyIOT.Devices
{
    public class TPLinkKasa : IDevices
    {
        IHttpClientFactory _httpClientFactory;
        IDataStore _dataStore;
        DatabaseConfig _databaseConfig;
        ActionGroup _actionGroup;
        private List<ActionFailure> actionFailures;

        public TPLinkKasa(IDataStore datastore, IHttpClientFactory httpClientFactory)
        {
            _dataStore = datastore;
            _httpClientFactory = httpClientFactory;
        }

        public string Name => "Kasa";

        public void DataConfig(DatabaseConfig databaseConfig)
        {
            _databaseConfig = databaseConfig;
            _dataStore.Config(_databaseConfig);
        }

        public async Task AuthenticateFirst(DeviceAuthConfig deviceAuthConfig)
        {

            if (_httpClientFactory == null)
            {
                throw new Exception("Kasa:AuthenticateFirst : httpClientFactory is NULL");
            }
            if (_databaseConfig == null)
            {
                throw new Exception("Kasa:AuthenticateFirst : databaseConfig is NULL");
            }

            //get orig ActionGroup from DB
            _actionGroup = await _dataStore.GetActionGroup("Kasa");
            if (_actionGroup != null)
            {
                throw new Exception("Kasa:AuthenticateFirst: Kasa Action Group already set - run Refresh");
            }

            KasaTryAuthenticateParams kasaAuthParams = new()
            {
                AppType = deviceAuthConfig.AppType,
                TerminalUUID = deviceAuthConfig.TerminalUUID,
                CloudUserName = deviceAuthConfig.CloudUserName,
                CloudPassword = deviceAuthConfig.CloudPassword,
                RefreshTokenNeeded = deviceAuthConfig.RefreshTokenNeeded
            };

            KasaTryAuthenticate kasaTryAuthenticate = new()
            {
                KasaAuthenticateParams = kasaAuthParams,
                Method = "login"
            };

            var serializeOptions = new JsonSerializerOptions
            {
                //WriteIndented = true
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string stringcontent = System.Text.Json.JsonSerializer.Serialize(kasaTryAuthenticate, serializeOptions);
            var content = new StringContent(stringcontent, Encoding.UTF8, "application/json");

            var kasaClient = _httpClientFactory.CreateClient("kasaAPI");
            if (kasaClient.BaseAddress == null)
            {
                kasaClient.BaseAddress = new Uri(deviceAuthConfig.AuthURI);
            }

            try
            {
                var result = await kasaClient.PostAsync(new Uri(deviceAuthConfig.AuthURI), content);

                if (result.StatusCode != HttpStatusCode.OK)
                {
                    throw new HttpRequestException("Kasa:AuthenticateFirst - Status Code not Ok : " + result.StatusCode.ToString());
                }

                string responseBody = await result.Content.ReadAsStringAsync();

                KasaAuthenticated returnObject = System.Text.Json.JsonSerializer.Deserialize<KasaAuthenticated>(responseBody);

                if (returnObject.ErrorCode != 0)
                {
                    //Log error or retry ?
                    throw new Exception("Kasa:AuthenticateFirst - Error code not 0 : " + returnObject.ErrorCode.ToString()
                                        + " Msg: " + returnObject.Msg);
                }

                if (returnObject.Result.Token == null)
                {
                    throw new Exception("Kasa:AuthenticateFirst - Kasa returned token null");
                }

                ActionGroup kasaGroup = new()
                {
                    id = "Kasa",
                    AuthURL = deviceAuthConfig.AuthURI,
                    DeviceURL = deviceAuthConfig.DeviceURI,
                    Token = returnObject.Result.Token,
                    TerminalUUID = deviceAuthConfig.TerminalUUID,
                    RefreshToken = returnObject.Result.RefreshToken,
                    LastUpdated = DateTime.Now
                };

                _dataStore.SaveActionGroup(kasaGroup).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw ex;
            }


        }

        public async Task AuthenticateRefreshToken(DeviceAuthConfig deviceAuthConfig)
        {

            if (_httpClientFactory == null)
            {
                throw new Exception("Kasa:AuthenticateRefreshToken : httpClientFactory is NULL");
            }
            if (_databaseConfig == null)
            {
                throw new Exception("Kasa:AuthenticateRefreshToken : databaseConfig is NULL");
            }

            //get orig ActionGroup from DB
            _actionGroup = await _dataStore.GetActionGroup("Kasa");
            if (_actionGroup == null)
            {
                throw new Exception("Kasa:AuthenticateRefreshToken: Kasa Action Group not found");
            }

            // Call Kasa refresh token api
            KasaAuthRefreshParams kasaAuthRefreshParams = new()
            {
                AppType = deviceAuthConfig.AppType,
                TerminalUUID = deviceAuthConfig.TerminalUUID,
                RefreshToken = _actionGroup.RefreshToken
            };

            KasaAuthRefresh kasaAuthRefresh = new()
            {
                Method = "refreshToken",
                Kasarefreshparams = kasaAuthRefreshParams
            };

            JsonSerializerOptions serializeOptions = new()
            {
                //WriteIndented = true
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string stringcontent = System.Text.Json.JsonSerializer.Serialize(kasaAuthRefresh, serializeOptions);
            var content = new StringContent(stringcontent, Encoding.UTF8, "application/json");

            var kasaClient = _httpClientFactory.CreateClient("kasaAPI");
            if (kasaClient.BaseAddress == null)
            {
                kasaClient.BaseAddress = new Uri(deviceAuthConfig.AuthURI);
            }

            //Call Refresh API
            try
            {
                var result = await kasaClient.PostAsync(new Uri(deviceAuthConfig.AuthURI), content);

                //check
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Kasa:AuthenticateRefreshToken: Status Code not Ok : " + result.StatusCode.ToString());
                }

                string responseBody = await result.Content.ReadAsStringAsync();

                KasaAuthRefreshReturn returnKasa = System.Text.Json.JsonSerializer.Deserialize<KasaAuthRefreshReturn>(responseBody);

                if (returnKasa.ErrorCode > 0)
                {
                    string msg = "";
                    if (returnKasa.Msg != null) { msg = returnKasa.Msg; }

                    throw new Exception("Kasa:AuthenticateRefreshToken: Kasa err_code " + returnKasa.ErrorCode + " Msg : " + msg);
                }

                //Update 
                _dataStore.SetActionGroupToken(_actionGroup.id, returnKasa.Result.Token).GetAwaiter().GetResult();

            }
            catch (Exception ex)
            {
                throw new Exception("Kasa:AuthenticateRefreshToken: clientKasaAPI.PostAsync Exception : " + ex.Message);
            }
        }

        public string DeviceGroupName()
        {
            if (_actionGroup == null)
            {
                return "Kasa";
            }
            return _actionGroup.id;
        }

        public async Task<List<ActionFailure>> Plug(ActionGroup actionGroup, Action actionItem, string triggerName, RetryConfig retryConfig)
        {
            var clientKasaPlug = _httpClientFactory.CreateClient("kasaAPI");

            if (clientKasaPlug.BaseAddress == null)
            {
                clientKasaPlug.BaseAddress = new Uri(actionGroup.DeviceURL);
            }

            string path = "?token=" + actionGroup.Token;

            //Preepare KASA data JSON
            KasaRequestRelayState kasaRelayState = new()
            {
                State = actionItem.StateTo //Passed value
            };

            KasaRequestSystem kasaSystem = new()
            {
                SetRelayState = kasaRelayState
            };

            KasaRequestData kasaRequestDate = new()
            {
                System = kasaSystem
            };

            KasaParams kasaParams = new()
            {
                DeviceId = actionItem.DeviceId,
                RequestDataObj = kasaRequestDate
            };

            KasaPassthrough kasaPassthrough = new()
            {
                Method = "passthrough",
                Params = kasaParams
            };

            //Encode to JSON
            var serializeOptions = new JsonSerializerOptions
            {
                //WriteIndented = true
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            //Double Encode Part
            string stringcontent = System.Text.Json.JsonSerializer.Serialize(kasaPassthrough, serializeOptions);
            var content = new StringContent(stringcontent, Encoding.UTF8, "application/json");

            try
            {
                var result = await clientKasaPlug.PostAsync(path, content);

                int retries = 0;
                while ((result.StatusCode != HttpStatusCode.OK) && retryConfig.Count > retries)
                {
                    Thread.Sleep(retryConfig.TimeMs);
                    result = await clientKasaPlug.PostAsync(path, content);
                    retries++;
                }

                if (result.StatusCode != HttpStatusCode.OK)
                {
                    FailureAdd(triggerName, actionItem, "Status Code:" + result.StatusCode.ToString(), "Retries:" + retries);
                }

                //Return Object
                KasaReturn returnObject = new();
                var temp = await result.Content.ReadAsStringAsync();
                returnObject = System.Text.Json.JsonSerializer.Deserialize<KasaReturn>(temp, serializeOptions);

                if (returnObject.ErrorCode != 0)
                {
                    if (returnObject.Msg != null)
                    {
                        FailureAdd(triggerName, actionItem, returnObject.Msg, "ErrorCode:" + returnObject.ErrorCode + " Retries:"+retries);
                    }
                    else
                    {
                        FailureAdd(triggerName, actionItem, "No Error Msg", "Retries:" + retries);
                    }
                }

            }
            catch (Exception ex)
            {
                FailureAdd(triggerName, actionItem, "Exception:" + ex.Message, "Unexpected Exception");
            }

            return actionFailures;

        }


        /// <summary>
        /// Add failure to a list of Failures
        /// </summary>
        /// <param name="triggerName">Trigger name</param>
        /// <param name="actionItem">This Action</param>
        /// <param name="failureMessage">Message of the failure</param>
        private void FailureAdd(string triggerName, Action? actionItem, string failureMessage, string additionalDetails)
        {
            if (actionFailures == null)
            {
                actionFailures = new();
            }

            ActionFailure failure = new()
            {
                ItemId = actionItem?.ItemId ?? string.Empty,
                ItemName = actionItem?.ItemName ?? string.Empty,
                TriggerName = triggerName,
                FailureDatetime = DateTime.Now.ToString("dd/mm/yyyy HH:mm:ss"), //should be UK
                Message = failureMessage,
                Additional = additionalDetails
            };

            actionFailures ??= new List<ActionFailure>();

            actionFailures.Add(failure);
        }
    }
}
