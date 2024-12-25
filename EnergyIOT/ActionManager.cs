using Microsoft.Extensions.Logging;
using EnergyIOT.Models;
using Action = EnergyIOT.Models.Action;
using EnergyIOT.DataAccess;
using EnergyIOT.Devices;

namespace EnergyIOT
{
    internal class ActionManager(ILogger logger, IDataStore dataStore, IEnumerable<IDevices> devicesGroups)
    {
        private List<ActionFailure> actionFailures;

        /// <summary>
        /// Action handler
        /// </summary>
        /// <param name="trigger">Trigger that's calling action</param>
        public async Task<List<ActionFailure>> RunActions(Trigger trigger)
        {

            actionFailures = [];

            //get action group info for all
            var actiongroupIDsUnique = trigger.Actions.Select(a => a.GroupId).Distinct().ToList();


            List<ActionGroup> actionGroups = await ActionGroups_ForTrigger(actiongroupIDsUnique);

            if (actionGroups?.Count == 0)
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

                //get DI Device item
                IDevices singleDevices = devicesGroups.SingleOrDefault(p => p.Name == actionItem.GroupId);

                if (singleDevices == null)
                {
                    logger.LogError("ActionManager - RunActions - found no DevicesGroup matching Name:{0}", actionItem.GroupId);
                    continue;
                }


                switch (actionItem.Type)
                {
                    case "Plug":
                        // await Action_KasaPlug(singleActionGroup, actionItem, trigger.Name);
                        List<ActionFailure> plugFailures = singleDevices.Plug(singleActionGroup, actionItem, trigger.Name).GetAwaiter().GetResult();
                        if (plugFailures != null)
                        {
                            actionFailures.AddRange(plugFailures);
                        }
                        break;

                    default:
                        logger.LogError("ActionManager - RunActions - found no actionItem Type");
                        break;
                }

                foreach (ActionFailure failure in actionFailures)
                {
                    logger.LogError("Kasa item error code not zero, msg: {msg}", failure.Message);
                }
            }

            return actionFailures;
        }


        //Get Action Pages--------------------------------------------

        /// <summary>
        /// Fetch Action Group data from Data Store - gets all then filters
        /// </summary>
        /// <param name="actionGroupIDs">List of Action Group/Name ID's</param>
        public async Task<List<ActionGroup>> ActionGroups_ForTrigger(List<string> actionGroupIDs)
        {
            List<ActionGroup> actionGroups = await dataStore.GetActionGroups(actionGroupIDs);

            //if null - log/alert later
            return actionGroups;
        }


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