using EnergyIOT.Models;

namespace EnergyIOT.DataAccess
{
    public interface IDataStore
    {
        void Config(DatabaseConfig dbConfig);
        Task<EnergyPrice> GetPriceItemByDate(DateTime dateToCheckUTC);
        Task<bool> SavePriceItems(UnitRates unitRates);
        Task<List<Trigger>> GetPerPriceTriggers(string mode);
        Task<List<Trigger>> GetHourlyTriggersSorted();
        Task<List<EnergyPrice>> GetDateSpanPrices(DateTime dateFrom, DateTime dateTo);
        Task<List<ActionGroup>> GetActionGroups(List<int> actionGroupIDs);
        Task<ActionGroup> GetActionGroup(string actionGroupID);
        Task SetActionGroupToken(string actionGroupID, string token);
        Task OverrideInsertUpdate(OverrideTrigger overrideItem);
        Task<OverrideTrigger> GetOverride(string idStartDate);
        Task<DBConfigString> GetConfigString(string configName);
        Task SetConfigString(DBConfigString configString);
    }
}