# cs2-LiteVIP
plugin for cs2 with basic functions VIP

#### If you find an error or anything else. Please message me in discord: @thesamefabius

<img src="https://github.com/partiusfabaa/cs2-LiteVIP/assets/96542489/e99c9cb4-b456-4947-bd6d-d2efd7fc98b0" width="194" height="293">

# Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp) and [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
3. Download [LiteVIP](https://github.com/partiusfabaa/cs2-LiteVIP/releases)
4. Unzip the archive and upload it to the game server

# Config
The config is created automatically in the same place where the dll is located
```

Now it is not necessary to write all parameters to a group, you can delete those that are not needed for a particular group
{
  "Admins": [76561144096558223],//SteamID64 should be separated by commas
  "Delay": 2,			//At what interval to issue the VIP(second)
  "VipTestSettings": {
    "VipTestEnabled": true, 	//is VIP-TEST enabled? true - yes, false - no
    "VipTestTime": 3600,	//Duration of VIP test issuance (in seconds)
    "VipTestGroup": "VIP1",	//Assigned VIP Group
    "VipTestCount": 4		//How many times can a player take the VIP test
  },
  "Groups": {
    "VIP1": {
      "Health": 115,
      "Armor": 115,
      "Gravity": 0.9,
      "Money": 5000,
      "SmokeColor": "random",
      "Healthshot": 1,
      "JumpsCount": 1
    },
    "VIP2": {
      "Health": 175,		//amount of health
      "Armor": 175,		//amount of armor
      "Gravity": 0.7,		//Gravity: less than 1 is low, more than 1 is high.
      "Money": 16000,		//amount of money at spawning
      "SmokeColor": "255 11 22",//color R G B is spelled with a space or "random" and then you'll have a different color for every shot.
      "Healthshot": 1,		//syringe count at revival
      "JumpsCount": 2,		//number of additional jumps
      "RainbowModel": true, 	//true - on, false - off
      "Items": ["weapon_molotov", "weapon_ak47"], //items given out at revival. If you don't need anything, leave the field blank
      "DecoySettings": {	
        "DecoyTeleport": true,	  //Is the teleportation grenade enabled? true - yes, false - no
        "DecoyCountInOneRound": 1,//how many teleportation grenades can be used in one round?
	"DecoyCountToBeIssued": 1 //number of teleportation grenades issued
      }
    }
  },
  "Connection": {
    "Host": 	"HOST",
    "Database": "DATABASE",
    "User": 	"USER",
    "Password": "PASSWORD"
  }
}

```

# Commands

`css_vip` | `!vip` - opens the VIP menu

`css_vip_gravity` | `!vip_gravity` - allows you to turn gravity on and off

`css_vip_key key` | `!vip_key key` - allows the user to activate a key with VIP privileges.

`css_viptest` | `!viptest` - allows the user to take the VIP test

### Commands for chief administrators

`css_vip_reload`,`!vip_reload` - reloads the configuration. Only for those specified in the configuration

`css_vip_createkey "vipgroup" "time_seconds"` - allows you to create a key for vip-activation (for server console only)

`css_vip_adduser "steamid" "vipgroup" "time_second"` - adds a vip player (for server console only)

`css_vip_deleteuser "steamid"` - allows you to delete a player by steamid identifier (for server console only)

