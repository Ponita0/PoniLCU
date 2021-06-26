# Ponita0 - PoniLCU
### A league of legend LCU library
# Installation ...
before going through all of this ... u need to have some info about the lcu endpoints and how it works if u have no idea u can check https://hexdocs.communitydragon.org/lol/lcuapi/6.getting-started-with-the-lcu-api  

You can install a Nuget package here https://www.nuget.org/packages/PoniLCU/  

or just copy
`Install-Package PoniLCU -Version 1.0.0`
and paste it in package manager Console

# How to use it ?
make an instance of LeagueClient
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
```cs
string body = "{\"profileIconId\": "+23+"}";
leagueClient.Request("put", "/lol-summoner/v1/current-summoner/icon", body);
```
![Usage Request Run](https://i.imgur.com/EZHsl1f.gif)

# Events Example
with PoniLCU you can subscribe to LCU events using the following code
```cs
leagueClient.Subscribe("/lol-gameflow/v1/gameflow-phase", GameFlowPhase);
```
and you can unsubscribe from events with the following code
```cs
leagueClient.Unsubscribe("/lol-gameflow/v1/gameflow-phase", GameFlowPhase);
```
You can Subscribe to event multiple times with diffrent Functions  
here is the GameFlowPhase Function im calling in this example
```cs
 private void GameFlowPhase(OnWebsocketEventArgs obj)
  {
            Console.WriteLine(obj.Data);
  }
  ```
![Usage Request Run](https://i.imgur.com/nuM34lT.gif)
