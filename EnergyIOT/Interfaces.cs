namespace EnergyIOT
{
    internal interface IDataStore
    {
        void Config(DatabaseConfig dbConfig);
        Task<EnergyPrice> GetPriceItemByDate(DateTime dateToCheckUTC);
        Task<bool> SavePriceItems(UnitRates unitRates);
        Task<List<Trigger>> GetPerPriceTriggers();
        Task<List<Trigger>> GetHourlyTriggersSorted();
        Task<List<EnergyPrice>> GetDateSpanPrices(DateTime dateFrom, DateTime dateTo);
        Task<List<ActionGroup>> GetActionGroups(List<int> actionGroupIDs);
        Task<ActionGroup> GetActionGroup(string actionGroupID);
        Task SetActionGroupToken(string actionGroupID, string token);
        Task OverrideInsertUpdate(OverrideTrigger overrideItem);
        Task<OverrideTrigger> GetOverride(string idStartDate);
    }
}
