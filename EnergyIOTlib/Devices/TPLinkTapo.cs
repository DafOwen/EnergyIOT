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
    public class TPLinkTapo : IDevices
    {
        IHttpClientFactory _httpClientFactory;
        IDataStore _dataStore;
        DatabaseConfig _databaseConfig;
        ActionGroup _actionGroup;
        private List<ActionFailure> actionFailures;

        public TPLinkTapo(IDataStore datastore, IHttpClientFactory httpClientFactory)
        {
            _dataStore = datastore;
            _httpClientFactory = httpClientFactory;
        }

        public string Name => "Tapo";

        public void DataConfig(DatabaseConfig databaseConfig)
        {
            _databaseConfig = databaseConfig;
            _dataStore.Config(_databaseConfig);
        }

        public async Task AuthenticateFirst(DeviceAuthConfig deviceAuthConfig)
        {
            if (_httpClientFactory == null)
            {
                throw new Exception("Tpo:AuthenticateFirst : httpClientFactory is NULL");
            }
            if (_databaseConfig == null)
            {
                throw new Exception("Tapo:AuthenticateFirst : databaseConfig is NULL");
            }


            //get orig ActionGroup from DB
            _actionGroup = await _dataStore.GetActionGroup("Tapo");
            if (_actionGroup != null)
            {
                throw new Exception("Tapo:AuthenticateFirst: Tapo Action Group already set - run Refresh");
            }


            TapoTryAuthenticateParams tapoAuthParams = new()
            {
                AppType = deviceAuthConfig.AppType,
                TerminalUUID = deviceAuthConfig.TerminalUUID,
                CloudUserName = deviceAuthConfig.CloudUserName,
                CloudPassword = deviceAuthConfig.CloudPassword,
                RefreshTokenNeeded = deviceAuthConfig.RefreshTokenNeeded,
                Platform = ""
            };

            TapoTryAuthenticate tapoTryAuthenticate = new()
            {
                TapoAuthenticateParams = tapoAuthParams,
                Method = deviceAuthConfig.Method
            };

            var serializeOptions = new JsonSerializerOptions
            {
                //WriteIndented = true
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string stringcontent = System.Text.Json.JsonSerializer.Serialize(tapoTryAuthenticate, serializeOptions);
            var content = new StringContent(stringcontent, Encoding.UTF8, "application/json");

            var tapoClient = _httpClientFactory.CreateClient("tapoAPI");
            if (tapoClient.BaseAddress == null)
            {
                tapoClient.BaseAddress = new Uri(deviceAuthConfig.AuthURI);
            }


            try
            {
                var result = await tapoClient.PostAsync(new Uri(deviceAuthConfig.AuthURI), content);

                if (result.StatusCode != HttpStatusCode.OK)
                {
                    throw new HttpRequestException("Tapo:AuthenticateFirst - Status Code not Ok : " + result.StatusCode.ToString());
                }

                string responseBody = await result.Content.ReadAsStringAsync();

                TapoAuthenticateResponse returnObject = System.Text.Json.JsonSerializer.Deserialize<TapoAuthenticateResponse>(responseBody);

                if (returnObject.ErrorCode != 0)
                {
                    //Log error or retry ?
                    throw new Exception("Tapo:AuthenticateFirst - Error code not 0 : " + returnObject.ErrorCode
                                        + " Msg: " + returnObject.Result.ErrorMsg);
                }

                if (returnObject.Result.Token == null)
                {
                    throw new Exception("Tapo:AuthenticateFirst - Tapo returned token null, Msg:" + returnObject.Result.ErrorMsg);
                }

                ActionGroup kasaGroup = new()
                {
                    id = "Tapo",
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
                throw ex;
            }

        }

        public async Task AuthenticateRefreshToken(DeviceAuthConfig deviceAuthConfig)
        {
            //Could not find Token Refresh details, method refreshToken returns method not known error

            //Could maybe just call AuthenticateFirst - will test how long auth token lasts for
            //AuthenticateFirst(deviceAuthConfig).GetAwaiter().GetResult();

            return;
        }

        public string DeviceGroupName()
        {
            if (_actionGroup == null)
            {
                return "Tapo";
            }
            return _actionGroup.id;
        }

        public async Task<List<ActionFailure>> Plug(ActionGroup actionGroup, Action actionItem, string triggerName)
        {
            //client
            var clientTapo = _httpClientFactory.CreateClient("tapoAPI");
            clientTapo.DefaultRequestHeaders.Add("Authorization", "ut|" + actionGroup.Token);
            clientTapo.DefaultRequestHeaders.Add("app-cid", "app:TP-Link_Tapo_Android:app");

            TaspoShadowsResponse shadowsResponse = Version(actionGroup, actionItem, clientTapo, triggerName).GetAwaiter().GetResult();

            if (actionFailures != null)
            {
                return actionFailures;
            }

            int nextVersion = shadowsResponse.Shadows[0].Version + 1;
            bool isOn = shadowsResponse.Shadows[0].State.Reported.On;

            //compare current to action
            if (actionItem.StateTo == 1 && !isOn
                || actionItem.StateTo == 0 && isOn)
            {

                //change state
                Uri plugActionURI = new Uri(actionGroup.DeviceURL + actionItem.DeviceId + "/shadows");
                //E.G. - https://euw1-app-server.iot.i.tplinknbu.com/v1/things/<DEVICEID>/shadows

                TapoPlugDesired plugDesired = new()
                {
                    On = !isOn
                };
                TapoPlugState plugState = new()
                {
                    Desired = plugDesired
                };
                TapoPlugChange plugChange = new()
                {
                    State = plugState,
                    Version = nextVersion.ToString()
                };

                var serializeOptions = new JsonSerializerOptions
                {
                    //WriteIndented = true
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string stringcontent = System.Text.Json.JsonSerializer.Serialize(plugChange, serializeOptions);
                var content = new StringContent(stringcontent, Encoding.UTF8, "application/json");

                var result = await clientTapo.PatchAsync(plugActionURI, content);

                if (result.StatusCode != HttpStatusCode.OK)
                {
                    try
                    {
                        TapoPlugErrorResponse returnObject = new();
                        var temp = await result.Content.ReadAsStringAsync();
                        returnObject = System.Text.Json.JsonSerializer.Deserialize<TapoPlugErrorResponse>(temp, serializeOptions);

                        FailureAdd(triggerName, actionItem, "Status Code:" + result.StatusCode.ToString() +
                            " Msg:" + returnObject.Message +
                            " Code:" + returnObject.Code);
                    }
                    catch (Exception ex)
                    {
                        FailureAdd(triggerName, actionItem, "Status Code:" + result.StatusCode.ToString());
                    }
                }//not Ok


            }
            else
            {
                //Could log a pass
            }

            return actionFailures;
        }


        private async Task<TaspoShadowsResponse> Version(ActionGroup actionGroup, Action actionItem, HttpClient clientTapoVersion, string triggerName)
        {

            Uri versionURI = new Uri(actionGroup.DeviceURL + "shadows?thingNames=" + actionItem.DeviceId);
            //E.g. https://euw1-app-server.iot.i.tplinknbu.com/v1/things/shadows?thingNames=<DEVICEID>

            TaspoShadowsResponse shadowsResponse = new();

            var serializeOptions = new JsonSerializerOptions
            {
                //WriteIndented = true
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            try
            {
                //Get
                var result = await clientTapoVersion.GetAsync(versionURI);

                if (result.StatusCode != HttpStatusCode.OK)
                {
                    //show error todo
                    TaspoShadowsError shadowErrors = new();
                    var errResult = await result.Content.ReadAsStringAsync();
                    shadowErrors = System.Text.Json.JsonSerializer.Deserialize<TaspoShadowsError>(errResult, serializeOptions);

                    FailureAdd(triggerName, actionItem, "Status Code:" + result.StatusCode.ToString() + " Msg:" + shadowErrors.Message);
                }
                else
                {
                    var temp = await result.Content.ReadAsStringAsync();
                    shadowsResponse = System.Text.Json.JsonSerializer.Deserialize<TaspoShadowsResponse>(temp, serializeOptions);
                }
            }
            catch (Exception ex)
            {
                FailureAdd(triggerName, actionItem, "Exception:" + ex.Message);
            }

            return shadowsResponse;
        }//Version



        /// <summary>
        /// Add failure to a list of Failures
        /// </summary>
        /// <param name="triggerName">Trigger name</param>
        /// <param name="actionItem">This Action</param>
        /// <param name="failureMessage">Message of the failure</param>
        private void FailureAdd(string triggerName, Action? actionItem, string failureMessage)
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
                Message = failureMessage
            };

            actionFailures ??= new List<ActionFailure>();

            actionFailures.Add(failure);
        }

    }
}
