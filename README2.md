# Ponita0 - PoniLCU

### A C# League of Legends LCU library

# Introduction
PoniLCU is a library to communicate with the League of Legends Client API. It gives an easy way to send requests as well as subscribe to websocket events.

Don't know about the League of Legends Client API? Learn more from [Hextech Docs](https://hextechdocs.dev/getting-started-with-the-lcu-api/). 

# Installation 

Just run
`Install-Package PoniLCU`
in the Package Manager Console

You can find a NuGet package here https://www.nuget.org/packages/PoniLCU/  

# The Basics

Make an instance of LeagueClient
```cs
//At top of your code 
//using PoniLCU;
//using static PoniLCU.LeagueClient; 
  
  
//Put this in the top of your code and make it public to access it from anywhere
LeagueClient leagueClient = new LeagueClient(credentials.cmd);

//You can do either credentials.cmd or credentials.lockfile
//It doesn't really matter
 ```
 
Like this:
 
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

## GET request

*I recommend you use Async/await*

```cs
var data = await leagueClient.Request(requestMethod.get, "/lol-summoner/v1/current-summoner");
Console.WriteLine(data);
```
![get request using](https://i.imgur.com/fTWw1Gm.gif)

### If you're not going to use async, add .Result to the end of the line

```cs
var data = leagueClient.Request(requestMethod.get, "/lol-summoner/v1/current-summoner").Result;
Console.WriteLine(data);
```
An example on how to get specific information from the json:

```cs
var data = await leagueClient.Request(requestMethod.get, "/lol-summoner/v1/current-summoner");

var DataButJsonDeserialized = Newtonsoft.Json.Linq.JObject.Parse(data);

Console.WriteLine(DataButJsonDeserialized["displayName"]);

//The console will output your name only
```

## PUT request

For the body, you have two options:
* Define the body string manually
* Let C# do that for us (with help of Newtonsoft.Json)

Both of these solutions will work, it is preference on how you would like to define it.
  - - - -
 Using a manual body string: 
```cs
string body = "{\"profileIconId\": " + 23 + "}";
await leagueClient.Request(requestMethod.put, "/lol-summoner/v1/current-summoner/icon", body);        
```

 - - - -
Using Newtonsoft Json:
```cs
var body = Newtonsoft.Json.JsonConvert.SerializeObject( new
	{
		profileIconId = 23
	});
await leagueClient.Request(requestMethod.put, "/lol-summoner/v1/current-summoner/icon", body);
 ```
 
![PUT Request Example](https://i.imgur.com/uT9lNr5.gif)


# Websocket Events Example
With PoniLCU you can subscribe to or unsubscribe from LCU events using the following code:
```cs
// Subscribe to event
leagueClient.Subscribe("/lol-gameflow/v1/gameflow-phase", GameFlowPhase);

// Unsubscribe from event
leagueClient.Unsubscribe("/lol-gameflow/v1/gameflow-phase", GameFlowPhase);

private void GameFlowPhase(OnWebsocketEventArgs obj)
{
	Console.WriteLine(obj.Data);
}
  ```
![Websocket Request Example](https://i.imgur.com/nuM34lT.gif)
