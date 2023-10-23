# cs2-LiteVIP
plugin for cs2 with basic functions VIP

# Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp) and [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
3. Download [LiteVIP](https://github.com/partiusfabaa/cs2-LiteVIP/releases)
4. Unzip the archive and upload it to the game server

# Config
The config is created automatically in the same place where the dll is located
```

{
    "Admins":"SteamID64", 	//You can have more than one, write via ';'
    "Delay":2, 			//At what interval to issue the VIP(second)
    "Users":{
	"76543299452378634": {	//steamid64
	    "Health":175,	//amount of health
	    "Armor":175,	//amount of armor
	    "Gravity":0.7,	//Gravity: less than 1 is low, more than 1 is high.
	    "Money":16000,	//amount of money at spawning
	    "SmokeColor":"255 0 0",	//color R G B is spelled with a space.
	    "Healthshot":3 //the number of health points given on spawning
       }
    }
}

```

# Commands
`css_vip_reload` - reloads the configuration. Only for those specified in the configuration
`css_vip_gravity` - allows you to turn gravity on and off
