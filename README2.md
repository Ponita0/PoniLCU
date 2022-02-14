
# Ponita0 - PoniLCU

### A C# League of Legends LCU library

# Introduction
PoniLCU is a library to communicate with the League of Legends Client API. It gives an easy way to send requests as well as subscribe to websocket events.

Don't know about the League of Legends Client API? Learn more from [Hextech Docs](https://hextechdocs.dev/getting-started-with-the-lcu-api/). 

# Installation 

just run
`Install-Package PoniLCU`
in the Package Manager Console

You can find a NuGet package here https://www.nuget.org/packages/PoniLCU/  

# How to use it ?
well , let me teach you how to use it 

Make an instance of LeagueClient
```cs
  //At top of you code 
  //using PoniLCU;
  //using static PoniLCU.LeagueClient; 
  
  
//put this in the top of your code and make it public to access it from anywhere
LeagueClient leagueClient = new LeagueClient(credentials.cmd);

//You can do either credentials.cmd or credentials.lockfile
//it doesnt really matter
 ```
 
 like this : 
 
 ```cs
using System;
using PoniLCU;
using static PoniLCU.LeagueClient;
namespace MyApp
{
    internal class Program
    {
      //--------------
        static LeagueClient leagueClient = new LeagueClient(credentials.cmd);
      //--------------
      
        static void Main(string[] args)
        {
            Console.WriteLine("hey users");
        }
     }
}
 ```
# Requests Examples

## get request

*I recommaned you use Async/await*

```cs
  var data = await leagueClient.Request(requestMethod.get, "/lol-summoner/v1/current-summoner");
  Console.WriteLine(data);
```
![get request using](https://i.imgur.com/fTWw1Gm.gif)

### if not going to use async .... then add .Result to the end of the line
like this


```cs
    var data = leagueClient.Request(requestMethod.get, "/lol-summoner/v1/current-summoner").Result;
    Console.WriteLine(data);
```
and as a tip information i will teach you how to get one specific data from the whole json ;D

```cs
var data = await leagueClient.Request(requestMethod.get, "/lol-summoner/v1/current-summoner");

var DataButJsonDeserialized = Newtonsoft.Json.Linq.JObject.Parse(data);

Console.WriteLine(DataButJsonDeserialized["displayName"]);

//The console will output your name only
```

## put request

 now .. for the body , you have two options
* define the body string manually
* let C# do that for us (with help of Newtonsoft.Json )
  
  - - - -
  The First Solution : 
```cs
string body = "{\"profileIconId\": " + 23 + "}";
await leagueClient.Request(requestMethod.put, "/lol-summoner/v1/current-summoner/icon", body);        
```

  - - - -
  The second solution : 
 ```cs
 var body = Newtonsoft.Json.JsonConvert.SerializeObject( new
            {
                profileIconId = 23
            });
            await leagueClient.Request(requestMethod.put, "/lol-summoner/v1/current-summoner/icon", body);
 ```
 
![Put Request Example](https://i.imgur.com/uT9lNr5.gif)

 ### they both will work 


# Websocket Events Example
With PoniLCU you can subscribe to or unsubscribe from LCU events using the following codes :
```cs
// Subscribe to event
leagueClient.Subscribe("/lol-gameflow/v1/gameflow-phase", GameFlowPhase);

// Subscribe to event
leagueClient.Unsubscribe("/lol-gameflow/v1/gameflow-phase", GameFlowPhase);

 private void GameFlowPhase(OnWebsocketEventArgs obj)
  {
            Console.WriteLine(obj.Data);
  }
  ```
![Usage Request Run](https://i.imgur.com/nuM34lT.gif)
