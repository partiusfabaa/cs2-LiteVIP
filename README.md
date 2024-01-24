# This plugin is no longer supported

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
```json

Now it is not necessary to write all parameters to a group, you can delete those that are not needed for a particular group
{
  "DisableAllOrPartial": 2, 	// Should I disable VIP in the first round, and in the second half? 0 - no, 1 - items and money, 2 - everything
  "JumpsNoVip": 1,
  "Admins": [76561144096558223],//SteamID64 should be separated by commas
  "Delay": 2,			//At what interval to issue the VIP(second)
  "VipTestSettings": {
    "VipTestEnabled": true, 	//is VIP-TEST enabled? true - yes, false - no
    "VipTestTime": 3600,	//Duration of VIP test issuance (in seconds)
    "VipTestCooldown": 3600, 	//How long before I can retest for VIP (in seconds)
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
      "Respawn": 2, 		//How many revivals per round are available to a player
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

| Command          | Description                      |
|------------------|-------------------------------|
| `css_vip` or `!vip` | opens the VIP menu          |
| `css_vip_gravity` or `!vip_gravity` | allows you to turn gravity on and off |
| `css_vip_respawn` or `!vip_respawn` | allows the player to be revived |
| `css_vip_key key` or `!vip_key key` | allows the user to activate a key with VIP privileges. |
| `css_viptest` or `!viptest` | allows the user to take the VIP test |
| `css_vips` or `!vips` | shows vip players online |

### Commands for chief administrators

| Command                             | Description                                               |
|-------------------------------------|-----------------------------------------------------------|
| `css_vip_reload` or `!vip_reload`    | Reloads the configuration. Only for those specified in the configuration |
| `css_vip_createkey "vipgroup" "time_seconds or 0 permanently"` or `!vip_createkey "vipgroup" "time_seconds or 0 permanently"` | Allows you to create a key for VIP activation (for server console only) |
| `css_vip_adduser "steamid" "vipgroup" "time_seconds or 0 permanently"` or `!vip_adduser "steamid" "vipgroup" "time_seconds or 0 permanently"` | Adds a VIP player (for server console only) |
| `css_vip_deleteuser "steamid"` or `!vip_deleteuser "steamid"` | Allows you to delete a player by SteamID identifier (for server console only) |

