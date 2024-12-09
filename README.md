# EnergyIOT

Octopus Energy + (TP-Link Kasa) Smart Switch API
Built as a replacement to IFTTT which I wasn't very happy with.

### Updates
| Date | Description |
| :---: | :---: |
| 2024-11-27 | Modes : Introducing modes where Triggers can be assigned a mode where they work e.g. only >0 or <0 price |
| 2024-12-09 | Refactor : Refactor to enable Dependency Injection for Devices (Kasa Plugs) + move core code to library project<br> <strong>Note: Breaking changes</strong> : spelling correction done for some Environmental variables "Parition" to "Partition" e.g. Database_ActionGroupPartition <br> Also ActionGroup database entry has changed (GroupName replacing id)|


### EnergyIOT

Main project of the solution - Azure function app with four functions

### EnergyIOTlib

Library file containing common code shared between EnergyIOT and EnergyIOTDataSetup

### EnergyIOTDataSetup

Console App used to enter data to CosmosDB (NoSQL).
Based on an earlier developement version, so does things a little differently.
A later addition includes creating/fetching the initial TP-link/kasa authentication token and refreshing it.



## EnergyIOT : Azure Functions

There are four Functions.
I have them within one Azure "Function App" - this makes deployment easier and allows them to share the same Environmental Variables etc.

__EnergyIOTPerPrice__ 

Timed trigger: Runs every 30 mins to check current energy price against triggers. (Prices are per 30 min).

[Cron expression](https://en.wikipedia.org/wiki/Cron#CRON_expression) : `0 */30 * * * *`

__EnergyIOTHourlyPM__ 


Timed trigger: Runs hourly between 16:00 and 22:00 to get the updated prices (from Octopus, published after 4pm).

[Cron expression](https://en.wikipedia.org/wiki/Cron#CRON_expression) : `0 0 16-22 * * *`

__EnergyIOTMonthly__ 

Timed trigger: Runs on 1st +15th of the month at 00:15 to refresh the TP-Link/Kasa smart-plug API authentication token.

Initially had it as [00 0 1 * *] - midnight on the 1st of the month, but discovered that the token had expired on the 31st. Can't do an expression for every 30 days - so 1st and 15th it is. Set to 00:15 so it doesn't clash with price function.

[Cron expression](https://en.wikipedia.org/wiki/Cron#CRON_expression) : `15 0 1,15 * *`


__EnergyIOTOverride__ 


Http Trigger: To insert an "Override" which stops the On/Off triggers from running for a set interval or time. Useful to keep the sockets on if needed during higher prices.

__EnergyIOTMode__

Http Trigger: To set the mode - enable/disable some triggers, currently: Default, Away or Off. Useful for longer term than an Override.

<br>

#### Platform choice

Other than the fact that I had some experience with them, the intention of using Azure Functions and Cosmos DB was to keep the cost as low as possible.
In theory it should keep within the free tier.

To be free, Azure Function App needs to be of type [Consumption Plan](https://learn.microsoft.com/en-us/azure/azure-functions/consumption-plan)
Notes on [Azure Function pricing](https://azure.microsoft.com/en-in/pricing/details/functions/) and (all your) CosmosDB's need to be within a certain throuput - 1000 RU's. 

<br/>

### Configuration Files
There are several configuration files.
In order to avoid loading private data to GitHub I've copied and renamed these files and put their original versions in the gitignore file.
So you will need to rename these.

#### EnergyIOT

__local.settings.json_EXAMPLE__ => Rename to local.settings.json

Replace values that are all ?'s e.g. 
`"Email_Username": "???????????@????????.???",`

This file is for local debugging - these values should be reproduced as environmental variables in the Azure Function.See below.

Contains numersous config information e.g. Database name, Cosmos DB container names (equivalent of tables), some (but not all) usernames, passwords etc.



#### EnergyIOTDataSetup

__appsettings.json_EXAMPLE__ => Rename to appsettings.json

Replace values that are all ?'s e.g. `"Username": "?????.?????@?????.???",`

Contains similar information to local.settings.json above (Database, container, names, usernames, etc) - but this app is just to be run locally.

__DataImport.json_EXAMPLE__ => Rename to DataImport.json

Replace values that are all ?'s - in this file that will just be the Device ID of my Kasa plugs. e.g. `"Deviceid": "????????????????????????????????????????????????",`

This contains json of my Triggers and Actions for easy import into the database. Only sensitive informaiton is the DeciveIDs of my plugs.

<br>

__Octopus Product + Tarrif Codes__

In several of the config files and the Environmental Variables equivalent - you should also double check/update your Product Tarriff codes. Mine are for London:

In local.settings.json / Environment variables:

`"EnergyAPI_Product": "AGILE-23-12-06",`

`"EnergyAPI_TariffCode": "E-1R-AGILE-23-12-06-C"`

In appsettings.json:
```
"EnergyAPI": {
    "BaseURI": "https://api.octopus.energy",
    "Section": "/v1/products/",
    "Product": "AGILE-23-12-06",
    "SubSection": "/electricity-tariffs/",
    "TariffCode": "E-1R-AGILE-23-12-06-C",
    "EndSection": "/standard-unit-rates/"
  },  
  ```

<br>


### Function Environmnent variables

Numerous configuration values are stored in the Azure Function Application Setttings / Environment Variables, for local debug these are in [local.settings.json](EnergyIOT/local.settings.json) (See rename note above). See that file for these.
In the Azure portal these can be set in the Azure Function App : Settings -> Configuration.

Replace required values for those with "?????" or your Octopus Product/Tariff.

As the four functions are in the Same Function App  they share the values.


In addition to custom setting the following is used to run the Azure function on UK time (I am Uk based).
`"WEBSITE_TIME_ZONE": "GMT Standard Time"`
(It's worth noting that Octopus API returns energy prices using UTC, they are stored as such and the functions convert when needed.)

<br>


### Database Config values
2024-11-27 Update : 
A new database collection to save config values. This was introduced for Modes so that they can be updated via Http Funcation. Environmental variables are not suited to such updates.

<br>

## Triggers and Actions

The system is set up/worded in terms of triggers and actions.

Triggers : A condition to check e.g. price above/below certain condition.

Actions : The result to carry out if that considtion is met i.e. turn a plug on, off, or email.


Code exists for several different triggers.
There is only one action type currently - TP-Link Kasa spart plugs

Hourly triggers - when fetched next day's prices.


- __Trigger_Hourly_NotifyPricesList__ - If prices found, email a list
- __Trigger_Hourly_PricesBelowValue__ - If price below set value, mention in the email
- __Trigger_Hourly_NotifyLowestSection__ - Calculate lowest daily section

Per price/30min triggers

They are similar to Octopus/IFTTT triggers but I left out the time interval.
- __Trigger_PerPrice_PriceAboveBelowValue__ - Trigger if price is above or below set value, depending if "Above" or "Bellow" in trigger name
- __Trigger_PerPrice_SectionLow__ - Calculate lowest period in the day for set number of 30min prices, trigger if in that time
- __Trigger_PerPrice_AverageAboveBelow__ - Trigger if current price above or below daily avaerage

Unlike IFTTT - they are not set for a specific timespan.


### Http Function networking
To allow access to the HTTP trigger - be sure to add your IP address to the allow lis (or allow all).
Mainly: Azure Home -> Function App -> Scroll down to Setting: Netowrking -> Add your home IP address

The Funtion code is set for "AuthorizationLevel.Function" access level i.e. it needs a Function or higher access key to be called.
To get the Function Key, e.g.:
Azure Home -> Function App -> Function (EnergyIOTOverride) -> Developer -> Function Keys

There are two Http Triggers

### EnergyIOTMode

The pattern to calling ths Http Trigger is:
https://__[APP_NAME]__.azurewebsites.net/api/__EnergyIOTMode__?mode=__[MODE]__

Current modes:

__Default__: Standard PerPrice triggers

__Away__: Only >0 or <0 PerPrice triggers - intended to just charge powerbanks etc when price is below 0p

__Off__: Not assigned to any triggers, but intended to switch off triggers - a longer term solution than adding a long term Override


### EnergyIOTOverride

The pattern to calling the Http Trigger is:
https://__[APP_NAME]__.azurewebsites.net/api/__EnergyIOTOverride__?code=__[APP_KEY]__&start=__[NOW OR DATE-TIME]__&interval=__[NUMBER]__

Where : __<NOW OR DATE-TIME>__
is either just "Now" (without quotes)
or Date-time inthe format : yyyy-MM-ddTHH-mm e.g. 2024-05-08T00:00
The date/time is passed in Local (UK for me) and then converted and saved in UTC.

A successful insert will give the message:
"Override Inserted"

## CosmosDB
Cosmos DB is used to keep the project within the free tier.
Currently max RU on free tier is 1000, however I already have a container for something else - so have set the shared max RU to 400 for this.
One of the Environment variables `Database_DatabaseRUMax` is used to keep updates within the RU.

Access to the Cosmos DB can be given either through allowing access to all of Azure (bad idea), via VNet or specific IP addresses. Free tier Azure functions cannot be put in a VNet, therefore you need to get the IP addresses the Function may use.

The outbound IP address of the functions is available in the portal, but not all possible IPs. Use the following [AZ CLI](https://learn.microsoft.com/en-gb/cli/azure/) commands.
`az functionapp show --resource-group [RESOURCEGROUPNAME] --name [FUNCTIONAPPNAME] --query outboundIpAddresses --output tsv`

`az functionapp show --resource-group [RESOURCEGROUPNAME] --name [FUNCTIONAPPNAME] --query possibleOutboundIpAddresses --output tsv`

<br>

## Octopus API

The Octopus API is well documented : https://developer.octopus.energy/docs/api/
Also handy - [Guy Lipman simplified guide](https://www.guylipman.com/octopus/api_guide.html)


Not needed to get prices, but for other calls you can get your [Octopus API access key here](https://octopus.energy/dashboard/new/accounts/personal-details/api-access)


Only one endpoint is used to retrieve the daily prices. No authentication is needed for this.
https://developer.octopus.energy/docs/api/#agile-octopus

E.g. of values, stored in Config file / Environment variables:
EnergyAPI_Product: "AGILE-23-12-06"
EnergyAPI_TariffCode: "E-1R-AGILE-23-12-06-C"
Tariff Code varies depending on your location - the above is for London.

<br>

## TP-Link Kasa plugs + API

Use of API to control TP-Link Kasa smart plugs. (I have 2x KP-115)

I initially bought __Kasa__ plugs because they could be used with IFTTT - but I found this to be unreliable. It's worth noting that TP-Link seem to be slowly discontinuing the Kasa range. KP-105 (non energy monitoring) is listed as "End of Life" and my KP-115 are listed as "Product phasing out: Replace with Tapo P110".

I've not investigated the API possiblities of Tapo plugs.

<br>

The Kasa API is not documented officially, however various people have found out the basics of calls to the Kasa servers.
This seems to work for now - but there's no guarantee that it will continue long term.
One possible solution I've seen is to add the devices to the SmartThings platform and then control via API calls to that.

There are vaious libraries online for the use of Kasa Devices, but none I found were C#
Some useful resources:

[Jason's Docs](https://docs.joshuatz.com/random/tp-link-kasa/) - TP-Link Kasa Dev CheatSheet
[Alexandre Dumont/IT Nerd Space](https://itnerd.space/?s=kasa) - Several articles on accessing the TP-Link Kasa API

### Kasa Login
As document by this [IT Nerd Space post](https://itnerd.space/2017/06/19/how-to-authenticate-to-tp-link-cloud-api/) to authenticate with the TP-Link API you need to obtain a TP-LInk Token.
This is done via a POST API call to `https://wap.tplinkcloud.com/` with payload:

```
{
 "method": "login",
 "params": {
 "appType": "Kasa_Android",
 "cloudUserName": "XXXXX",
 "cloudPassword": "XXXXX",
 "terminalUUID": "MY_UUID_v4",
 "refreshTokenNeeded": true
 }
}
```
Where "MY_UUID_v4" is any UUID v4 you can generate e.g. https://www.uuidgenerator.net/version4 .
This is not covered in this solution - use something like curl or Postman to obtain the TP-link API token.
Mor on `refreshTokenNeeded` below.

__2 Factor Authentiction / 2FA / MFA__

It's worth noting that TP-Link seem to have enforced 2 Factor Authentication after most of the resources I could find on TP-Link Kasa API authentication.
Calling the above with 2FA on resulted in an error response saying the application was old.
So you coul either switch off 2FA in the Kasa app,or better : switch it off temporarily, run the above to obtain the TP-link otken, then switch 2FA back on.
This seems to work - the token will continue to work.

__Authentication Token Duration__

However - As of writing this (May 2024) the Kasa Authentication token seems to last 30 days (I had it expire on 31st after a refresh on 1st).
So Initial authentication is done using the additional parameter ```refreshTokenNeeded```
This is not documented in most Kasa references, but is mentioned here :: [tokenRefresh](https://github.com/adumont/tplink-cloud-api/issues/43)

Since a month is relatively short I added the additional __EnergyIOTMonthly__ function to call the token refresh.
The tokenRefresh call can be run with 2 FA switched on.



### Kasa Devices

Once authenticated you need to find the endpoint (URL differ) and device ID for your Kasa Plugs

As per [IT Nerd Space - How to get the TP-Link HS100 cloud end-point URL?](https://itnerd.space/2017/05/21/how-to-get-the-tp-link-hs100-cloud-end-point-url/)

Make a Call to the TP-Link URI : `https://wap.tplinkcloud.com?token=[TOKENHERE]`
With Data payload:
```
{
"method":"getDeviceList"
}
```
And header: `Content-Type: application/json`

Check the `appServerUrl` value of the response, the URL can/is different from the above authentication calls e.g. `https://eu-wap.tplinkcloud.com`

<br>

## Email
several elements use E-mail. Either to notify me daily of the next day's new prices or to notify me of any issues.

For this I've used my personal gmail account.

Rather than having to code for OAuth authentication I've gone the slightly easier route:

My Gmail account has two factor authentication switched on, and so for these functions I've created a seperate App-password. You can find some [instructions here on Google](https://support.google.com/mail/answer/185833?hl=en-GB). I believe similar functionality is available via Office365 etc.

<br>

## References

Various useful References

### Misc tools
UUID generator [https://www.uuidgenerator.net/version4](https://www.uuidgenerator.net/version4)

[Microsoft Learn links to resources about Azure Functions](https://learn.microsoft.com/en-us/azure/azure-functions/)

[Microsoft Learn - notes on Timer Triggers](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer)

[Microsoft Learn links to resources about CosmosDB](https://learn.microsoft.com/en-us/azure/cosmos-db/)

### Octopus API

[The Octopus API Documentation](https://developer.octopus.energy/docs/api/) ,sub section :  [Endpoint for prices](https://developer.octopus.energy/docs/api/#agile-octopus)

[Octopus higher level API guide/references](https://docs.octopus.energy/)

[Guy Lipman simplified guide](https://www.guylipman.com/octopus/api_guide.html)

[Get your Octopus API access key here](https://octopus.energy/dashboard/new/accounts/personal-details/api-access)

[Octopus Smart Energy Forum](https://forum.octopus.energy/) (need to request a login)


### Kasa API

[IT Nerd Space - How to get the TP-Link HS100 cloud end-point URL?](https://itnerd.space/2017/05/21/how-to-get-the-tp-link-hs100-cloud-end-point-url/)

[Jason's Docs](https://docs.joshuatz.com/random/tp-link-kasa/) - TP-Link Kasa Dev CheatSheet

[Alexandre Dumont/IT Nerd Space](https://itnerd.space/?s=kasa) - Several articles on accessing the TP-Link Kasa API

[Mention of tokenRefresh](https://github.com/adumont/tplink-cloud-api/issues/43)

[Joshua's Docs - TP-Link Cloud, Kasa - Dev Cheatsheet](https://docs.joshuatz.com/random/tp-link-kasa/)

[API calls for KASA strip plug: childId](https://www.reddit.com/r/tasker/comments/hq1sjc/help_accessing_individual_plugs_on_a_tplink_power/) (Not used here, but maybe of use)