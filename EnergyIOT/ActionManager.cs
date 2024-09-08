using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Logging;

namespace EnergyIOT
{
    internal class ActionManager(ILogger logger)
    {

        private IHttpClientFactory _httpClientFactory;
        private HttpClient clientKasaPlug;
        private List<ActionFailure> actionFailures;

        /// <summary>
        /// Action handler
        /// </summary>
        /// <param name="dataStore">Implementation of IDataStore - Database etc store</param>
        /// <param name="trigger">Trigger that's calling action</param>
        /// <param name="httpClientFactory">IHttpCLlientFactory to get httpClient</param>
        public async Task<List<ActionFailure>> RunActions(IDataStore dataStore, Trigger trigger, IHttpClientFactory httpClientFactory)
        {

            actionFailures = [];

            if (httpClientFactory == null)
            {
                logger.LogError("ActionManager-RunActions: httpClientFactor is null");
                FailureAdd(trigger.Name, null, "ActionManager-RunActions: httpClientFactor is null");
                return actionFailures;
            }

            _httpClientFactory = httpClientFactory;
            clientKasaPlug = _httpClientFactory.CreateClient("clientKasaPlugAPI");

            //get action group info for all
            var actiongroupIDsUnique = trigger.Actions.Select(a => a.GroupId).Distinct().ToList();


            List<ActionGroup> actionGroups = await ActionGroups_ForTrigger(dataStore, actiongroupIDsUnique);

            if(actionGroups?.Count == 0)
            {
                logger.LogError("ActionManager-RunActions ActionGroups_ForTrigger returns no ActionGroups");
                FailureAdd(trigger.Name, null, "ActionManager-RunActions ActionGroups_ForTrigger returns no ActionGroups");
                return actionFailures;
            }

            //mutiple actions
            foreach (Action actionItem in trigger.Actions)
            {
                //get group
                ActionGroup singleActionGroup = actionGroups.Find(x => x.id == actionItem.GroupId.ToString());

                switch (actionItem.Type)
                {
                    case "Plug":
                        await Action_KasaPlug(singleActionGroup, actionItem, trigger.Name);
                        break;

                    default:
                        logger.LogError("ActionManager - RunActions - found no actionItem Type");
                        break;
                }
            }

            return actionFailures;


        }


        //Get Action Pages--------------------------------------------

        /// <summary>
        /// Fetch Action Group data from Data Store - gets all then filters
        /// </summary>
        /// <param name="dataStore">IMplementation of IDataStore - Database etc store</param>
        /// <param name="actionGroupIDs">List of Action Group ID's</param>
        public async Task<List<ActionGroup>> ActionGroups_ForTrigger(IDataStore dataStore, List<int> actionGroupIDs)
        {
            List<ActionGroup> actionGroups = await dataStore.GetActionGroups(actionGroupIDs);

            //if null - log/alert later
            return actionGroups;

        }//ActionGroups_ForTrigger


        //----------------------------- Plug Actions -----------------------------------

        /// <summary>
        /// Run actions on Kasa Plug
        /// </summary>
        /// <param name="actionGroup">The action group for this action</param>
        /// <param name="actionItem">This Action</param>
        /// <param name="triggerName">Name fo the trigger</param>
        public async Task Action_KasaPlug(ActionGroup actionGroup, Action actionItem, string triggerName)
        {
            if (clientKasaPlug.BaseAddress == null)
            {
                clientKasaPlug.BaseAddress = new Uri(actionGroup.BaseURL);
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

                if (result.StatusCode != HttpStatusCode.OK)
                {
                    logger.LogError("clientKasaPlug.PostAsync not Ok, Status:{}", result.StatusCode);
                    FailureAdd(triggerName, actionItem, "Status Code:" + result.StatusCode.ToString());
                }

                //Return Object
                KasaReturn returnObject = new();
                var temp = await result.Content.ReadAsStringAsync();
                returnObject = System.Text.Json.JsonSerializer.Deserialize<KasaReturn>(temp, serializeOptions);

                if (returnObject.ErrorCode != 0)
                {
                    if (returnObject.Msg != null)
                    {
                        logger.LogError("Kasa item error code not zero, code:{errcode} msg: {msg}", returnObject.ErrorCode, returnObject.Msg);
                        FailureAdd(triggerName, actionItem, returnObject.Msg);
                    }
                    else
                    {
                        logger.LogError("Kasa item error code not zero, code:{errcode} msg: null", returnObject.ErrorCode);
                        FailureAdd(triggerName, actionItem, "Null");
                    }
                }

            }
            catch (Exception ex)
            {
                logger.LogError("clientKasaPlug.PostAsync Fail, Msg:{msg}", ex.Message);
                FailureAdd(triggerName, actionItem, "Exception:" + ex.Message);
            }


        }//Action_KasaPlug


        // ----------------- Failure alert ----------------------

        /// <summary>
        /// Add failure to a list of Failures
        /// </summary>
        /// <param name="triggerName">Trigger name</param>
        /// <param name="actionItem">This Action</param>
        /// <param name="failureMessage">Message of the failure</param>
        private void FailureAdd(string triggerName, Action? actionItem, string failureMessage)
        {
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
