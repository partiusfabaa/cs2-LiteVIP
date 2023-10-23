# cs2-LiteVIP
plugin for cs2 with basic functions VIP

# Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp)
2. Download [LiteVIP](https://github.com/partiusfabaa/cs2-LiteVIP/releases)
3. Unzip the archive and upload it to the game server

# Config
The config is created automatically in the same place where the dll is located
```
{
	"Delay":2, 						      //At what interval to issue the VIP(second)
	"Users":{
		"76543299452378634": {  	//steamid64
			"Health":175,           //amount of health
			"Armor":175,		        //amount of armor
			"Gravity":0.7,			    //Gravity: less than 1 is low, more than 1 is high.
			"Money":16000,          //amount of money at spawning
			"SmokeColor":"255 0 0"	//color R G B is spelled with a space.
		}
	}
}
```
