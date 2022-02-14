
# Ponita0 - PoniLCU

### A C# League of Legends LCU library

# Introduction
PoniLCU is a library to communicate with the League of Legends Client API. It gives an easy way to send requests as well as subscribe to websocket events.

Don't know about the League of Legends Client API? Learn more from [Hextech Docs](https://hextechdocs.dev/getting-started-with-the-lcu-api/). 

# Installation 

You can install a NuGet package here https://www.nuget.org/packages/PoniLCU/  

Or just run
`Install-Package PoniLCU`
in the Package Manager Console

# The Basics
Make an instance of LeagueClient
```cs
  //Don't Forget to do using PoniLCU;

LeagueClient leagueClient = new LeagueClient();
//just for making sure we dont do any requests before Connecting 
 while (!leagueClient.IsConnected)
         {
             continue;
         }
 ```
# Request Example

## get request
```cs
  var currentSummoner = await leagueClient.Request("get", "/lol-summoner/v1/current-summoner", null).Result.Content.ReadAsStringAsync();
  MessageBox.Show(currentSummoner);
```
![get request using](https://i.imgur.com/9v5azuK.gif)
### if not going to use async .... then add .Result to the end of the line
like this
```cs
    var currentSummoner =  leagueClient.Request("get", "/lol-summoner/v1/current-summoner", null).Result.Content.ReadAsStringAsync().Result;
    MessageBox.Show(currentSummoner);
```

## put request
```cs
string body = "{\"profileIconId\": "+23+"}";
leagueClient.Request("put", "/lol-summoner/v1/current-summoner/icon", body);
```

![Usage Request Run](https://i.imgur.com/EZHsl1f.gif)

# Websocket Events Example
With PoniLCU you can subscribe to LCU events using the following code:
```cs
leagueClient.Subscribe("/lol-gameflow/v1/gameflow-phase", GameFlowPhase);
```
You can unsubscribe from events with the following code:
```cs
leagueClient.Unsubscribe("/lol-gameflow/v1/gameflow-phase", GameFlowPhase);
```
You can Subscribe to an event multiple times with different functions.
This is the GameFlowPhase Function I'm calling in this example:
```cs
 private void GameFlowPhase(OnWebsocketEventArgs obj)
  {
            Console.WriteLine(obj.Data);
  }
  ```
![Usage Request Run](https://i.imgur.com/nuM34lT.gif)
