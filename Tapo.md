# Tapo System


### Updates
| Date | Description |
| :---: | :---: |
| 2025-01-12 | Authentication token expired after a month - call AuthenticateFirst from AuthenticateRefreshToken |


<hr>
I initially wrote the system using (TP-Link) Kasa plugs.

It looks like TP-Link have discontinued the Kasa line of products. When one of my Kasa plugs stopped working fully recently (wouldn't switch power off) TP-Link sent me a Tapo plug replacement (Tapo P110) to replace my Kasa KP115.

There is a seperate Tapo app from the Kasa App. 

Interestingly you can link your Kasa account in your Tapo app, and control Kasa devices in the Tapo app. but not the other way around.

I tried controlling the Tapo plug via the Kasa API.

Interestingly - the Kasa `"method":"getDeviceList"` API call does list the Tapo plug after I linked the accounts!
However when trying to turn the plug on, the API would only reply with a "Device is offline" error:
<pre>
{
    "error_code": -20571,
    "msg": "Device is offline"
}
</pre>

So - this made me think that the Tapo devices can't directly be controlled by the Kasa API - instead my guess is that the Tapo app authenticates the Kasa account and change Kasa items via the Kasa API seperately.
However, it is strange that the Tapo Devices are listed in the Kasa API but not my Kasa API

Unfortunately I wasn't able to find much in the way of documentation about the API for Tapo plugs/devices.
The majority of references I found were for Home Assist - which sends messages to the direct to the Plug/Device, on the local IP address rather than via TP-Linke servers aka "TP-Link cloud".
There was only one source I found that utilised the TP-Link cloud/romote API. It's directed toward controling a Tapo device (Lamp) through PowerShell:

https://github.com/roflsandwich/Tapo_L530E

Unfortunately - it doesn't seem to include the call to Refresh the Auth Token - something that I used in the Kasa API when the 2FA/MFA was swithed onto the account : I used this to renew the authorisaation with 2FA/MFA switched on - see main [README](.\README.md).

## Authentication

**URL** 

https://n-wap-gw.tplinkcloud.com/api/v1/account

Method : POST

**Body**
<pre>
{
    "method": "login",
    "params": {
        "appType": "TP-Link_Tapo_Android",
        "cloudUserName": "******@****.***",
        "cloudPassword": "************",
        "platform": "",
        "refreshTokenNeeded": true,
        "terminalUUID": "078aaf55-c93b-4285-95c1-7b3846a00dcf"
    }
}
</pre>

**Response**

E.g. (Sensitive details redacted)
<pre>
{
    "error_code": 0,
    "result": {
        "accountId": "*********",
        "regTime": "2024-03-18 19:15:21",
        "countryCode": "GB",
        "riskDetected": 0,
        "nickname": "**********",
        "errorCode": "0",
        "email": "**********@*****.**",
        "token": "***********************",
        "refreshToken": "***********************"
    }
}
</pre>


## Refresh Authorisation
As mentioned above - I've not managed to get a refresh autherisation call to work.
If I try a call similar to Kasa - then I get a method not supported message.

I've seen a setting measured by some people in the tapo App :

Tapo App → Me → Tapo Lab → Third-Party Compatibility → On

I thought this might not expire the Auth token, but this was incorrect - it expired after a month.

Previously I had a simple shadow/copy action : If the Kasa was turned on - the Tapo app would turn on the Tapo plug, and visa versa.

For time being - I'm going to call AuthenticateFirst from AuthenticateRefreshToken.


## Device List / Things

The call to list the devices in the account seems to be different for Tapo.

Kasa used a method similar to authentication call byt using: `{"method":"getDeviceList"}`

**URL** 

https://euw1-app-server.iot.i.tplinknbu.com/v1/things?page=0

Method: GET

**Body**

None

**Header**

| Key | Value |
| :---: | :---: |
| Authorization | ut\|*******-***************** <br> The asterix: the refreshToken from auth call|
| app-cid | app:TP-Link_Tapo_Android:app |

**Response**

E.g. (Sensitive details redacted)

<pre>
{
    "page": 0,
    "pageSize": 20,
    "total": 1,
    "data": [
        {
            "thingName": "******************************************",
            "appServerUrl": "https://euw1-app-server.iot.i.tplinknbu.com",
            "cloudGatewayUrl": "euw1-app-cloudgateway.iot.i.tplinknbu.com",
            "cloudGatewayUrlV2": "wss://euw1-app-cloudgateway.iot.i.tplinknbu.com",
            "status": 1,
            "thingModelId": "******",
            "role": 0,
            "familyId": "default",
            "roomId": "*******",
            "commonDevice": true,
            "nickname": "******************",
            "avatarUrl": "plug",
            "onboardingTime": **********,
            "category": "plug",
            "model": "P110",
            "crossRegion": true,
            "deviceName": "P110",
            "deviceType": "SMART.TAPOPLUG",
            "oemId": "******************************",
            "hwId": "******************************",
            "hwVer": "1.0",
            "fwVer": "1.3.1 Build 240621 Rel.162048",
            "fwId": "00000000000000000000000000000000",
            "region": "Europe/London",
            "mac": "************",
            "isSubThing": false,
            "migrateState": ""
        }
    ]
}
</pre>



## Version
The "Version" call seem to be something that Tapo requires that Kasa didn't use.
The version seems to be an incremental state counter.
Each time you change the state of a plug (i.e. switch it on or off) you need to get the current "version" and re-send with +1. 


**URL**

https://euw1-app-server.iot.i.tplinknbu.com/v1/things/shadows?thingNames=*********PlugThingID************

Method: GET

**Body**

Nnone

**Header**

| Key | Value |
| :---: | :---: |
| Authorization | ut\|*******-***************** <br> The asterix: the refreshToken from auth call|
| app-cid | app:TP-Link_Tapo_Android:app |


**Response e.g.**
<pre>
{
    "shadows": [
        {
            "thingName": "*****************************************",
            "state": {
                "desired": {
                    "on": false
                },
                "reported": {
                    "auto_off_remain_time": 0,
                    "power_protection_status": "normal",
                    "auto_off_status": "off",
                    "charging_status": "normal",
                    "overcurrent_status": "normal",
                    "on": false
                }
            },
            "version": 118
        }
    ],
    "failThingList": []
}
</pre>



## State

The call to turn the plug On / Off.
requires the Version number from the above call with +1 added.
If the version number isn't high enought - it will complain.

**URL**

https://euw1-app-server.iot.i.tplinknbu.com/v1/things/************ThingDevicaID***********/shadows

Methods: PATCH

**Body**

The version item : SHould be +1 more than the existing version from the above call.
"on" : True = On, False = off

<pre>
{
    "version":"[version from above + 1 e.g.119]",
    "state" : {
        "desired" :{
            "on" : false
    	    }
        }
}
</pre>


**Response**

Sucess
<pre>
{
    "version": 119,
    "requestId": "************************"
}
</pre>

Error

e.g. If the Version number isn't high enough
<pre>
{
    "code": 11000,
    "message": "[**************************************] Update version is smaller than present version.",
    "data": {
        "curVersion": 118
    }
}
</pre>
