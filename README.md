# cs2-LiteVIP
plugin for cs2 with basic functions VIP

<img src="https://github.com/partiusfabaa/cs2-LiteVIP/assets/96542489/e99c9cb4-b456-4947-bd6d-d2efd7fc98b0" width="194" height="293">

# Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp) and [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
3. Download [LiteVIP](https://github.com/partiusfabaa/cs2-LiteVIP/releases)
4. Unzip the archive and upload it to the game server

# Config
The config is created automatically in the same place where the dll is located
```

{
  "Admins": [76561144096558223],//SteamID64 should be separated by commas
  "Delay": 2, 			//At what interval to issue the VIP(second)
  "Users": {
    "76561144096558223": { 	//steamid64
      "Health": 175,		//amount of health
      "Armor": 175,		//amount of armor
      "Gravity": 0.7,		//Gravity: less than 1 is low, more than 1 is high.
      "Money": 16000,		//amount of money at spawning
      "SmokeColor": "255 11 22",//color R G B is spelled with a space.
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
  }
}

```

# Commands
`css_vip_reload`,`!vip_reload` - reloads the configuration. Only for those specified in the configuration

`css_vip_gravity`,`!vip_gravity` - allows you to turn gravity on and off

`css_vip`,`!vip` - opens the VIP menu
