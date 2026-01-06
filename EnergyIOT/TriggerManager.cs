using System.Globalization;
using Microsoft.Extensions.Logging;
using EnergyIOT.Models;
using EnergyIOT.DataAccess;
using EnergyIOT.Devices;

namespace EnergyIOT
{
    internal class TriggerManager(ILogger logger, IDataStore dataStore, IEnumerable<IDevices> devicesGroups, RetryConfig retryConfig)
    {
        private readonly ILogger _logger = logger;
        private List<ActionFailure> _actionFailures;
        private EmailConfig _emailConfig;
        private List<EnergyPrice> energyDailyPricesDB = [];
        private int utcEndHour = 0;

        //-----------------------Trigger MANAGERS ---------------------

        /// <summary>
        /// Price Manager - Fetch then call the per price (30min) Triggers
        /// </summary>
        /// <param name="emailConfig">Emailconfig settings</param>
        /// <param name="mode">mode e.g. Defaul</param>
        internal async Task Trigger_PerPrice_Manager(EmailConfig emailConfig, string mode)
        {

            _actionFailures = [];

            if (emailConfig == null)
            {
                _logger.LogError("Trigger_PerPrice_Manager - emailConfig is null");
                throw new ArgumentNullException("Trigger_PerPrice_Manager - emailConfig is null");
            }
            else
            {
                _emailConfig = emailConfig;
            }

            List<Trigger> sortedTriggerList = await dataStore.GetPerPriceTriggers(mode);

            _logger.LogInformation("PerPrice Mode: {mode} Active triggers found :{count}", mode, sortedTriggerList.Count);

            //go through each - call trigger
            foreach (Trigger triggerItem in sortedTriggerList)
            {

                switch (triggerItem.Type)
                {

                    case "Price_Above":
                        Trigger_PerPrice_PriceAboveBelowValue(triggerItem).GetAwaiter().GetResult();
                        break;

                    case "Section_Low":
                        Trigger_PerPrice_SectionLow(triggerItem).GetAwaiter().GetResult();
                        break;

                    case "Price_Below":
                        Trigger_PerPrice_PriceAboveBelowValue(triggerItem).GetAwaiter().GetResult();
                        break;

                    case "Average_Above":
                        Trigger_PerPrice_AverageAboveBelow(triggerItem).GetAwaiter().GetResult();
                        break;

                    case "Average_Below":
                        Trigger_PerPrice_AverageAboveBelow(triggerItem).GetAwaiter().GetResult();
                        break;

                    case "SectionLow_Multi_Day":
                        Trigger_PerPrice_SectionLowMultiDays(triggerItem).GetAwaiter().GetResult();
                        break;
                }
            }

            if (_actionFailures?.Count > 0)
            {
                NotifyErrors();

                string errmessageConcat = String.Join(',', _actionFailures);
                //throw new Exception($"Action Failures count:{_actionFailures.Count} error messages: {errmessageConcat}");
            }

            return;
            // }
        }

        /// <summary>
        /// Fetch then call the Hourly (~4-11) Triggers
        /// </summary>
        /// <param name="emailConfig">Emailconfig settings</param>
        /// <param name="unitRates">Energy unit rates</param>
        /// <param name="priceListColours">List of PriceListColour for colour coding in email</param>
        internal async Task Trigger_Hourly_Manager(EmailConfig emailConfig, UnitRates unitRates, List<PriceListColour> priceListColours)
        {
            _actionFailures = [];

            //Validation
            if (emailConfig == null)
            {
                _logger.LogError("Trigger_PerPrice_Manager - emailConfig is null");
                throw new ArgumentNullException("Trigger_PerPrice_Manager - emailConfig is null");
            }
            else
            {
                _emailConfig = emailConfig;
            }

            //Get triggers, sorted by order
            List<Trigger> sortedTriggerList = await dataStore.GetHourlyTriggersSorted();

            if (sortedTriggerList?.Count == 0)
            {
                _logger.LogInformation("Trigger_Hourly_Manager found no triggers");
                return;
            }


            //Common Message body + Subject
            string notifyMessageBody = "New Prices Saved";
            string notifyMessageSubject = "";

            foreach (Trigger triggerItem in sortedTriggerList)
            {
                switch (triggerItem.Type)
                {
                    case "Hourly_NotifyPricesList":
                        string notifyPricesListBody = Trigger_Hourly_NotifyPricesList(triggerItem, unitRates, priceListColours);

                        if (notifyPricesListBody.Trim() != "")
                        {
                            notifyMessageBody += notifyPricesListBody;
                            notifyMessageBody += "<br/><hr>";
                            notifyMessageSubject += "PriceList ";
                        }
                        break;

                    case "Hourly_NotifyPricesBelowValue":
                        string notifyBelowBody = Trigger_Hourly_PricesBelowValue(triggerItem, unitRates);

                        if (notifyBelowBody.Trim() != "")
                        {
                            notifyMessageBody += notifyBelowBody;
                            notifyMessageBody += "<br/><hr>";
                            notifyMessageSubject += "+ PricesBelow ";
                        }
                        else
                        {
                            notifyMessageBody += $"No price below value set: {triggerItem.Value.ToString()} p/kWh";
                            notifyMessageBody += "<br/><hr>";
                        }
                        break;

                    case "Hourly_NotifyLowestSection":
                        string notifyLowestSectionBody = await Trigger_Hourly_NotifyLowestSection(triggerItem, unitRates, priceListColours);

                        if (notifyLowestSectionBody.Trim() != "")
                        {
                            notifyMessageBody += notifyLowestSectionBody;
                            notifyMessageBody += "<br/><hr>";
                            notifyMessageSubject += "+ LowestSection ";
                        }
                        break;

                    case "Hourly_Summary":
                        string notifyPriceSummary = Trigger_Hourly_PricesSummary(triggerItem, unitRates, priceListColours);

                        if (notifyPriceSummary.Trim() != "")
                        {
                            notifyMessageBody += "<br/><br/>";
                            notifyMessageBody += notifyPriceSummary;
                            notifyMessageBody += "<br/><hr>";
                        }
                        break;
                }
            }//foreach trigger

            SendEmail.SendEmailMsg(_emailConfig, _logger, "Energy Prices Update : " + notifyMessageSubject, notifyMessageBody);

            if (_actionFailures?.Count > 0)
            {
                NotifyErrors();
            }


        }//Trigger_Hourly_Manager

        //-----------------------HOURLY Triggers ---------------------

        /// <summary>
        /// Trigger (Hourly) to notify of energy prices below value - likely 0
        /// </summary>
        /// <param name="triggerItem">The trigger </param>
        /// <param name="unitRates">The dailt energy prices</param>
        public string Trigger_Hourly_PricesBelowValue(Trigger triggerItem, UnitRates unitRates)
        {
            string body = "";
            string table = "";

            LogTriggerCall("Trigger_Hourly_PricesBelowValue", triggerItem);

            List<EnergyPrice> energyPricesBelowValue = [];

            foreach (EnergyPrice energyPrice in unitRates.Results)
            {
                if (energyPrice.ValueIncVat < triggerItem.Value)
                {
                    energyPricesBelowValue.Add(energyPrice);
                }
            }

            if (energyPricesBelowValue?.Count > 0)
            {
                body = "Sub-Value/Zero Prices found! <br /><br />";

                table += "<table border='1' style='border-collapse:collapse'>";
                table += "<tr><th style='padding: 5px;'>Time</th><th style='padding: 5px;'>Price</th></tr>";

                List<EnergyPrice> pricesSorted = energyPricesBelowValue.OrderBy(o => o.id).ToList();

                CultureInfo cultureInfo = new("en-GB");

                foreach (EnergyPrice price in pricesSorted)
                {
                    //set locale ?
                    DateTime tmpDateTime = DateTime.Parse(price.id).ToLocalTime(); //conversion to Uk Ok

                    table += $"<tr><td style='padding: 5px;'>{tmpDateTime.ToString(cultureInfo)}</td><td style='padding: 5px;'>{price.ValueIncVat}</td></tr>";
                }//foreach

                table += "</table>";

                body += table + "<br />";
            }

            return body;
        }

        /// <summary>
        /// Trigger (Hourly) to notify of the daily low section
        /// </summary>
        /// <param name="triggerItem">The trigger </param>
        /// <param name="unitRates">The dailt energy prices</param>
        public async Task<string> Trigger_Hourly_NotifyLowestSection(Trigger triggerItem, UnitRates unitRates, List<PriceListColour> priceListColours)
        {
            string table = "";
            string sectionColourCell = "";

            LogTriggerCall("Trigger_Hourly_NotifyLowestSection", triggerItem);

            //convert EnergyPrice to EnergyPrice
            List<EnergyPrice> pricesToday = [];
            foreach (EnergyPrice price in unitRates.Results)
            {
                pricesToday.Add(price);
            }
            //double check is same order
            pricesToday.Sort((x, y) => x.id.CompareTo(y.id));

            //get average per section, ordered
            List<(int i, string id, decimal price)> sectionTotals = Trigger_GetSectionAverageOrdered(pricesToday, (Int32)triggerItem.Value);

            //get first - the lowest
            DateTime sectionLocalDate = DateTime.Parse(sectionTotals[0].id).ToLocalTime();

            decimal sectionPrice = sectionTotals[0].price;

            //Save lowest section price for other use
            LowestDailySection lowestDailySection = new()
            {
                id = sectionTotals[0].id,
                AvgValueIncVat = sectionPrice,
                NoIntervals = (Int32)triggerItem.Value
            };

            bool saveOk = await dataStore.SaveDailyLowest(lowestDailySection);
            if (!saveOk)
            {
                _logger.LogWarning("SaveDailyLowest Failed saving daily lowest");
            }


            if (priceListColours != null)
            {
                sectionColourCell = priceListColours.Where(d => d.From <= sectionPrice && d.To >= sectionPrice)
                                                .FirstOrDefault().Colour;
            }

            table += "<table border='1' style='border-collapse:collapse'><tr>";
            table += "<th style=' padding: 5px;'> Interval </th><th style=' padding: 5px;'>Start Time</th><th style='padding: 5px;'>Avg Price</th></tr>";
            table += $"<tr><td style='padding: 5px;text-align: center;'>{triggerItem.Value?.ToString("F0")}</td>";
            table += $"<td style=' padding: 5px;text-align: center;'>{sectionLocalDate.ToString("dd/MM/yyyy HH:mm")}</th>";
            table += $"<td style='padding: 5px; text-align: center; background-color:{sectionColourCell}'>{sectionPrice.ToString("F")} p/kWh</th>";
            table += "</tr></table>";

            string emailSection = "Lowest price section of the day:<br/><br/>";
            emailSection += table;

            return emailSection;
        }


        /// <summary>
        /// Trigger (Hourly) to notify of energy prices below value - likely 0
        /// </summary>
        /// <param name="triggerItem">The trigger </param>
        /// <param name="unitRates">The dailt energy prices</param>
        /// <param name="priceListColours">Colour coding for price list email</param>
        public string Trigger_Hourly_NotifyPricesList(Trigger triggerItem, UnitRates unitRates, List<PriceListColour> priceListColours)
        {
            LogTriggerCall("Trigger_Hourly_NotifyPricesList", triggerItem);

            string table = "";
            decimal total = 0;
            string colourRow = "";

            //html table
            table = "<table style=\"border: 1px solid black; border-collapse: collapse;\"> <tr>" +
                    "<th style=\" padding-top: 5px; padding-bottom: 5px; padding-left: 15px;  padding-right: 15px;\">Rate (p/kWh)</th><th style=\" padding-top: 5px; padding-bottom: 5px; padding-left: 15px;  padding-right: 15px;\">Time</th>" +
                    "</tr>";


            List<EnergyPrice> sortedList = unitRates.Results.OrderBy(u => u.id).ToList();

            foreach (EnergyPrice price in sortedList)
            {
                if (priceListColours != null)
                {
                    colourRow = priceListColours.Where(d => d.From <= price.ValueIncVat && d.To >= price.ValueIncVat).FirstOrDefault().Colour;
                }

                table += $"<tr style=\"border: 1px solid black; border-collapse: collapse; background-color:{colourRow}\">";
                table += "<td style=\" padding-top: 5px; padding-bottom: 5px; padding-left: 15px;  padding-right: 15px;\">" + price.ValueIncVat.ToString()
                        + "</td><td style=\" padding-top: 5px; padding-bottom: 5px; padding-left: 15px;  padding-right: 15px;\">"
                        + DateTime.Parse(price.id).ToLocalTime().ToString("dd/MM/yyyy HH:mm") //should be ok - tested elsewhere
                        + "</tr>";

                colourRow = "";
            }

            table += "</table>";

            string section = "<strong>Prices</strong><br/><br/>" + table;

            return section;
        }


        /// <summary>
        /// Trigger (Hourly) to notify of the daily low section
        /// </summary>
        /// <param name="triggerItem">The trigger </param>
        /// <param name="unitRates">The dailt energy prices</param>
        /// <param name="priceListColours">Colour coding for price list email</param>
        public string Trigger_Hourly_PricesSummary(Trigger triggerItem, UnitRates unitRates, List<PriceListColour> priceListColours)
        {
            LogTriggerCall("Trigger_Hourly_PricesSummary", triggerItem);

            string table = "";
            decimal total = 0;
            string minColourCel = "", maxColourCell = "", averageColourCell = "";

            //Average
            total = unitRates.Results.Sum(p => p.ValueIncVat);
            decimal averagePrice = total / unitRates.Results.Count;
            string average = averagePrice.ToString("F") + " p/kWh";
            if (priceListColours != null)
            {
                averageColourCell = priceListColours.Where(d => d.From <= averagePrice && d.To >= averagePrice)
                                                .FirstOrDefault().Colour;
            }


            List<EnergyPrice> sortedList = unitRates.Results.OrderBy(u => u.ValueIncVat).ToList();

            //Min
            string minPrice = sortedList.First().ValueIncVat.ToString("F") + " p/kWh";
            DateTime minDateTime = DateTime.Parse(sortedList.First().id).ToLocalTime(); //conversion to Uk
            string minTime = minDateTime.ToString("dd/MM HH:mm");

            if (priceListColours != null)
            {
                minColourCel = priceListColours.Where(d => d.From <= sortedList.First().ValueIncVat && d.To >= sortedList.First().ValueIncVat)
                                                .FirstOrDefault().Colour;
            }

            //Max
            string maxPrice = sortedList.Last().ValueIncVat.ToString("F") + " p/kWh";
            DateTime maxDateTime = DateTime.Parse(sortedList.Last().id).ToLocalTime(); //conversion to Uk
            string maxTime = maxDateTime.ToString("dd/MM HH:mm");
            if (priceListColours != null)
            {
                maxColourCell = priceListColours.Where(d => d.From <= sortedList.Last().ValueIncVat && d.To >= sortedList.Last().ValueIncVat)
                                                .FirstOrDefault().Colour;
            }

            table += "<table border='1' style='border-collapse:collapse'>";
            table += "<tr><th>Lowest</th><th>Average</th><th>Highest</th></tr>";
            table += "<tr>";
            table += $"<td style='padding-top: 5px; padding-bottom: 5px; padding-left: 15px; padding-right: 15px; background-color:{minColourCel}'>";
            table += minPrice + "</td>";
            table += $"<td style='padding-top: 5px; padding-bottom: 5px; padding-left: 15px;  padding-right: 15px;background-color:{averageColourCell}'>";
            table += average + "</td>";
            table += $"<td style='padding-top: 5px; padding-bottom: 5px; padding-left: 15px;  padding-right: 15px;background-color:{maxColourCell}'>";
            table += maxPrice + "</td>";
            table += "</tr><tr>";
            table += $"<td style='padding-top: 5px; padding-bottom: 5px; padding-left: 15px;  padding-right: 15px;background-color:{minColourCel}'>";
            table += minTime + "</td>";
            table += $"<td style='padding-top: 5px; padding-bottom: 5px; padding-left: 15px;  padding-right: 15px;background-color:{averageColourCell}'>&nbsp;</td>";
            table += $"<td style='padding-top: 5px; padding-bottom: 5px; padding-left: 15px;  padding-right: 15px;background-color:{maxColourCell}'>";
            table += maxTime + "</td>";
            table += "</tr></table>";

            return table;
        }
        
        //--------------------Per price (30min) triggers----------------------------

        /// <summary>
        /// Trigger (30min) if energy price is above or below value in trigger - handles both
        /// </summary>
        /// <param name="triggerItem">The trigger </param>
        public async Task Trigger_PerPrice_PriceAboveBelowValue(Trigger triggerItem)
        {
            LogTriggerCall("Trigger_PerPrice_PriceAboveBelowValue", triggerItem);

            //CHeck No Actions
            if (triggerItem.Actions.Count == 0)
            {
                _logger.LogInformation("Trigger_PerPrice_PriceAboveBelowValue - finds no Actions");
                return;
            }

            bool doActions = false;

            //get current price matching 30 min time
            DateTime searchDate = Trigger_NowSegmentDate();

            EnergyPrice priceDB = await dataStore.GetPriceItemByDate(searchDate);


            if (priceDB == null)
            {
                _logger.LogError("Trigger_PerPrice_PriceAboveBelowValue - Trigger_PerPrice_GetPrice NO prices");
                return;
            }

            //Price Trigger is ABOVE or BELOW
            if (triggerItem.Type.Contains("Above"))
            {
                if (priceDB.ValueIncVat > triggerItem.Value)
                { doActions = true; }

            }
            else if (triggerItem.Type.Contains("Below"))
            {
                if (priceDB.ValueIncVat < triggerItem.Value)
                { doActions = true; }
            }

            //Do actions
            if (doActions)
            {
                LogTriggerResult("Trigger_PerPrice_PriceAboveBelowValue", triggerItem, "Fire Actions");
                ActionManager actionManager = new(_logger, dataStore, devicesGroups, retryConfig);
                List<ActionFailure> actionFailures = await actionManager.RunActions(triggerItem);

                if (actionFailures?.Count > 0)
                {
                    _actionFailures.AddRange(actionFailures);
                }
            }
            else
            {
                LogTriggerResult("Trigger_PerPrice_PriceAboveBelowValue", triggerItem, "Skip Actions");
            }


        }//Trigger_PerPrice_PriceAboveBelowValue


        /// <summary>
        /// Trigger (30min) finds daily low section, fires if within that
        /// </summary>
        /// <param name="triggerItem">The trigger </param>
        public async Task Trigger_PerPrice_SectionLow(Trigger triggerItem)
        {
            LogTriggerCall("Trigger_PerPrice_SectionLow", triggerItem);

            string pricesType;

            List<EnergyPrice> pricesToCheck;

            //CHeck No Actions
            if (triggerItem.Actions.Count == 0)
            {
                _logger.LogInformation("Trigger_PerPrice_SectionLow - finds no Actions");
                return;
            }

            //if MaxCheck + MinCheck - do interval prices
            if (!string.IsNullOrEmpty(triggerItem.MaxCheck) && !string.IsNullOrEmpty(triggerItem.MinCheck))
            {
                pricesType = "Trigger_GetIntervalPrices";
                pricesToCheck = await Trigger_GetIntervalPrices(triggerItem.MinCheck, triggerItem.MaxCheck);
            }
            else
            {
                //Get day's prices
                pricesType = "Trigger_GetDaysPrices";
                pricesToCheck = await Trigger_GetDaysPrices();
            }
 

            if (pricesToCheck.Count == 0)
            {
                _logger.LogError($"Trigger_PerPrice_SectionLow - {pricesType} found no prices");
                return;
            }

            //Get sections + averaged price
            List<(int i, string id, decimal price)> sectionTotals = Trigger_GetSectionAverageOrdered(pricesToCheck, (Int32)triggerItem.Value);

            //check section v now
            DateTime checkDate = Trigger_NowSegmentDate();
            string checkDateString = checkDate.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");

            bool doAction = false;

            // if check date == lowest section date - do actions
            if (checkDateString == pricesToCheck[sectionTotals[0].i].id)
            {
                doAction = true;
            }

            //Do actions
            if (doAction)
            {
                ActionManager actionManager = new(_logger, dataStore, devicesGroups, retryConfig);
                List<ActionFailure> actionFailures = await actionManager.RunActions(triggerItem);
                LogTriggerResult("Trigger_PerPrice_SectionLow", triggerItem, "Fire Actions");

                if (actionFailures?.Count > 0)
                {
                    _actionFailures.AddRange(actionFailures);
                }
            }
            else
            {
                LogTriggerResult("Trigger_PerPrice_SectionLow", triggerItem, "Skip Actions");
            }


        }//Trigger_PerPrice_SectionLow


        /// <summary>
        /// Trigger (30min) - finds daily average - fires if above or below that - handles both
        /// </summary>
        /// <param name="triggerItem">The trigger </param>
        public async Task Trigger_PerPrice_AverageAboveBelow(Trigger triggerItem)
        {

            LogTriggerCall("Trigger_PerPrice_AverageAboveBelow", triggerItem);

            //Get average Price
            decimal dailyAverage = await Trigger_GetDailyAverage();

            bool doAction = false;

            //CHeck No Actions
            if (triggerItem.Actions.Count == 0)
            {
                _logger.LogInformation("Trigger_PerPrice_AverageAboveBelow - finds no Actions");
                return;
            }

            // Get current price
            EnergyPrice priceDB = await Trigger_PerPrice_GetPrice();
            if (priceDB == null)
            {
                _logger.LogError("Trigger_PerPrice_PriceAboveBelowValue - Trigger_PerPrice_GetPrice NO prices");
                return;
            }

            //"Average_Above"
            if (triggerItem.Type.Contains("Above"))
            {
                if (priceDB.ValueIncVat > dailyAverage)
                {
                    doAction = true;
                }

            }
            else if (triggerItem.Type.Contains("Below"))
            {
                if (priceDB.ValueIncVat < dailyAverage)
                {
                    doAction = true;
                }
            }


            //Do actions
            if (doAction)
            {
                ActionManager actionManager = new(_logger, dataStore, devicesGroups, retryConfig);
                List<ActionFailure> actionFailures = await actionManager.RunActions(triggerItem);
                LogTriggerResult("Trigger_PerPrice_PriceAboveBelowValue", triggerItem, "Fire Actions");

                if (actionFailures?.Count > 0)
                {
                    _actionFailures.AddRange(actionFailures);
                }
            }
            else
            {
                LogTriggerResult("Trigger_PerPrice_PriceAboveBelowValue", triggerItem, "Skip Actions");
            }

        }

        /// <summary>
        /// Trigger (30min) finds if price is lowest section from multiple days
        /// </summary>
        /// <param name="triggerItem">The trigger </param>
        public async Task Trigger_PerPrice_SectionLowMultiDays(Trigger triggerItem)
        {
            LogTriggerCall("Trigger_PerPrice_SectionLowMultiDays", triggerItem);

            string pricesType;

            List<EnergyPrice> pricesToCheck;

            //Check No Actions
            if (triggerItem.Actions.Count == 0)
            {
                _logger.LogInformation("Trigger_PerPrice_SectionLowMultiDays - finds no Actions");
                return;
            }

            //get current price matching 30 min time
            DateTime searchDate = Trigger_NowSegmentDate();

            EnergyPrice priceDB = await dataStore.GetPriceItemByDate(searchDate);

            if (priceDB == null)
            {
                _logger.LogError("Trigger_PerPrice_SectionLowMultiDays - Trigger_PerPrice_GetPrice NO prices");
                return;
            }

            LowestDailySection lowSection = await dataStore.GetDailyLowestForPeriod((Int32)triggerItem.Value);

            DateTime lowestLow =  DateTime.Parse(lowSection.id).ToLocalTime();
            DateTime lowestTop = DateTime.Parse(lowSection.id).AddMinutes(4 * 30).ToLocalTime();


            //Do Actions or not
            if (lowestLow <= DateTime.Now && DateTime.Now <= lowestTop)
            {
                ActionManager actionManager = new(_logger, dataStore, devicesGroups, retryConfig);
                List<ActionFailure> actionFailures = await actionManager.RunActions(triggerItem);
                LogTriggerResult("Trigger_PerPrice_SectionLowMultiDays", triggerItem, "Fire Actions");

                if (actionFailures?.Count > 0)
                {
                    _actionFailures.AddRange(actionFailures);
                }
            }
            else
            {
                LogTriggerResult("Trigger_PerPrice_SectionLowMultiDays", triggerItem, "Skip Actions");
            }

        }

        //-----------------------Getting one or several---------------------

        /// <summary>
        /// Gets single (current) price
        /// </summary>
        public async Task<EnergyPrice> Trigger_PerPrice_GetPrice()
        {

            EnergyPrice priceItemDB = new();

            DateTime searchDate = Trigger_NowSegmentDate();

            priceItemDB = await dataStore.GetPriceItemByDate(searchDate);

            return priceItemDB;

        }

        /// <summary>
        /// Gets the daily prices - but for price session e.g. 22:00-22:00 UTC
        /// </summary>
        public async Task<List<EnergyPrice>> Trigger_GetDaysPrices()
        {

            if (energyDailyPricesDB.Count > 0)
                return energyDailyPricesDB;

            DateTime dateFrom = new(DateOnly.FromDateTime(DateTime.Now.AddDays(-1)), new TimeOnly(DateParameterHour(), 0, 0));

            DateTime dateTo = new(DateOnly.FromDateTime(DateTime.Now), new TimeOnly(DateParameterHour(), 30, 0));

            energyDailyPricesDB = await dataStore.GetDateSpanPrices(dateFrom, dateTo);

            return energyDailyPricesDB;

        }

        /// <summary>
        /// Gets Prices between two times
        /// COuld be same or different days
        /// </summary>
        public async Task<List<EnergyPrice>> Trigger_GetIntervalPrices(string minCheck, string maxCheck)
        {
            var timeFrom = TimeOnly.Parse(minCheck);
            var timeTo = TimeOnly.Parse(maxCheck);

            DateTime dateTimeFrom = DateOnly.FromDateTime(DateTime.Now).ToDateTime(timeFrom).ToUniversalTime();
            DateTime dateTimeTo = DateOnly.FromDateTime(DateTime.Now).ToDateTime(timeTo).ToUniversalTime();


            if (dateTimeFrom > dateTimeTo)
            {
                //Overlap day
                //get the max day in the DB - To is that, from is day -1
                EnergyPrice lastPriceSaved = await dataStore.GePriceItemLast();

                DateTime lastPriceUK = DateTime.Parse(lastPriceSaved.id).ToLocalTime();

                dateTimeTo = lastPriceUK.Date + new TimeSpan(dateTimeTo.Hour, dateTimeTo.Minute, dateTimeTo.Second);
                //from : -1 day
                dateTimeFrom = lastPriceUK.AddDays(-1).Date + new TimeSpan(dateTimeFrom.Hour, dateTimeFrom.Minute, dateTimeFrom.Second);

            }

            energyDailyPricesDB = await dataStore.GetDateSpanPrices(dateTimeFrom, dateTimeTo);

            return energyDailyPricesDB;
        }

        int DateParameterHour()
        {
            //End hour is UK 23 - but BST/GMT changes.
            //Get UTC of current version
            if (utcEndHour == 0)
            {
                //Get hour to - varies with BST/GMT
                DateTime ukEnd = new(DateOnly.FromDateTime(DateTime.Now), new TimeOnly(23, 0, 0));
                DateTime utcEnd = ukEnd.ToUniversalTime();
                utcEndHour = utcEnd.Hour;
                return utcEndHour;
            }
            else
            {
                return utcEndHour;
            }

        }

        /// <summary>
        /// Calculate grouped pricing averages, return orderd list lowest to higher group by no : noSections
        /// </summary>
        /// <param name="dayEnergyPrices">List of EnergyPrice - daily prices</param>
        /// <param name="noSections">noSections - number of 30min sections</param>
        public List<(int i, string id, decimal price)> Trigger_GetSectionAverageOrdered(List<EnergyPrice> dayEnergyPrices, int noSections)
        {
            List<(int i, string id, decimal price)> sectionTotals = [];

            //sort by date to start early, go to late
            dayEnergyPrices.Sort((x, y) => x.id.CompareTo(y.id));

            decimal shortCycleTotal = 0;

            for (int i = 0; i < dayEnergyPrices.Count - noSections; i++)
            {
                shortCycleTotal = 0;

                for (int j = 0; j < noSections; j++)
                {
                    shortCycleTotal += dayEnergyPrices[i + j].ValueIncVat;
                }

                sectionTotals.Add((i, dayEnergyPrices[i].id, shortCycleTotal / noSections));

            }


            //order by decimal/average price to get lowest first
            sectionTotals = sectionTotals.OrderBy(valuetuple => valuetuple.price).ToList();

            return sectionTotals;
        }

        /// <summary>
        ///If trigger is run a time other than xx:30 or xx:00 - got to next or previous depending on minutes
        /// </summary>
        public DateTime Trigger_NowSegmentDate()
        {

            DateTime nowUTC = DateTime.UtcNow;

            DateTime searchDate;

            if (nowUTC.Minute >= 45)
            {
                //+ 1 hr - 00min
                if (nowUTC.Hour < 23)
                {
                    searchDate = new DateTime(DateOnly.FromDateTime(nowUTC), new TimeOnly(nowUTC.Hour + 1, 0, 0));
                }
                else
                {
                    searchDate = new DateTime(DateOnly.FromDateTime(nowUTC).AddDays(1), new TimeOnly(0, 0, 0));
                }
            }
            else if (nowUTC.Minute <= 15)
            {
                // +0 hr - 00min
                searchDate = new DateTime(DateOnly.FromDateTime(nowUTC), new TimeOnly(nowUTC.Hour, 0, 0));
            }
            else
            {
                //30min
                searchDate = new DateTime(DateOnly.FromDateTime(nowUTC), new TimeOnly(nowUTC.Hour, 30, 0));
            }

            return searchDate;
        }

        /// <summary>
        /// Fetches days prices, calculates average
        /// </summary>
        public async Task<decimal> Trigger_GetDailyAverage()
        {
            List<EnergyPrice> energyPrices = [];

            decimal dailyAverage;

            energyPrices = await Trigger_GetDaysPrices();

            dailyAverage = energyPrices.Select(x => x.ValueIncVat).DefaultIfEmpty(0).Average();

            return dailyAverage;

        }



        // --------------- Logging + Failures-----------

        private void LogTriggerCall(string triggerFunction, Trigger triggerItem)
        {
            _logger.LogInformation("Trigger Fired:{Func} - {Name}", triggerFunction, triggerItem.Name);
        }

        private void LogTriggerResult(string triggerFunction, Trigger triggerItem, string result)
        {
            _logger.LogInformation("Trigger Result:{Func} - {Name}, Result:{result}", triggerFunction, triggerItem.Name, result);
        }

        private void NotifyErrors()
        {

            _logger.LogInformation("Trigger Manager - Notify of Action errors");

            bool isNotEmpty = _actionFailures?.Count > 0;

            string message = "";
            string subject = "Octopus Energy IOT Failures: ";

            if (isNotEmpty)
            {
                subject += $"Action Failures:{_actionFailures.Count.ToString()}";

                foreach (ActionFailure failure in _actionFailures)
                {
                    message += "<br/>";
                    message += "<br/>Trigger : " + failure.TriggerName;
                    message += "<br/>Action : " + failure.ItemName;
                    message += "<br/>ActionID : " + failure.ItemId;
                    message += "<br/>DateTime : " + failure.FailureDatetime;
                    message += "<br/>Error Message : " + failure.Message;
                    message += "<br/>Additional details : " + failure.Additional;
                    message += "<br/><br/>";
                }

            }


            SendEmail.SendEmailMsg(_emailConfig, _logger, subject, message);
        }

    }//Class TriggerManager
}