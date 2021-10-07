using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using WebSocketSharp;
using System.Management;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Text;
using RestSharp;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Principal;
using System.Collections.Generic;

namespace PoniLCU
{
    public class OnWebsocketEventArgs : EventArgs
    {
        // URI    
        public string Path { get; set; }

        // Update create delete     
        public string Type { get; set; }

        // data :D
        public dynamic Data { get; set; }
    }

    public class LeagueClient
    {
        private static HttpClient HTTP_CLIENT;
        private Dictionary<string, List<Action<OnWebsocketEventArgs>>> Subscribers = new Dictionary<string, List<Action<OnWebsocketEventArgs>>>();
        private Tuple<string, string> processInfo;
        private bool connected;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<OnWebsocketEventArgs> OnWebsocketEvent;

        public bool IsConnected => connected;

        public LeagueClient()
        {
            try
            {
                HTTP_CLIENT = new HttpClient(new HttpClientHandler()
                {
                    SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls,
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true
                });
            }
            catch
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                HTTP_CLIENT = new HttpClient(new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true
                });
            }

            Task.Delay(2000).ContinueWith(e => TryConnectOrRetry());
            var trytimes = 0;
            while (!IsConnected)
            {
                if (trytimes != 5)
                {
                    trytimes++;
                    TryConnectOrRetry();
                }
                else
                {
                    Debug.WriteLine("Connection timed out ");
                    break;
                }
            }
        }

        public Task<HttpResponseMessage> Request(string method, string url, object body)
        {
            if (!connected) throw new InvalidOperationException("Not connected to LCU");

            return HTTP_CLIENT.SendAsync(
                new HttpRequestMessage(new HttpMethod(method), "https://127.0.0.1:" + processInfo.Item2 + url)
                {
                    Content = body == null
                        ? null
                        : new StringContent(body.ToString(), Encoding.UTF8, "application/json")
                });
        }

        public async Task<dynamic> getStringJsoned(string url)
        {
            if (!connected) throw new InvalidOperationException("Not connected to LCU");

            var res = await HTTP_CLIENT.GetAsync("https://127.0.0.1:" + processInfo.Item2 + url);
            var stringContent = await res.Content.ReadAsStringAsync();

            if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return SimpleJson.DeserializeObject(stringContent);
        }

        public async void GetData(string url, Action<dynamic> handler)
        {
            OnWebsocketEvent += data =>
            {
                if (data.Path == url) handler(data.Data);
            };

            if (connected)
            {
                handler(await getStringJsoned(url));
            }
            else
            {
                Action connectHandler = null;
                connectHandler = async () =>
                {
                    OnConnected -= connectHandler;
                    handler(await getStringJsoned(url));
                };

                OnConnected += connectHandler;
            }
        }

        public void ClearAllListeners()
        {
            OnWebsocketEvent = null;
        }

        private async void TryConnect()
        {
            try
            {
                if (connected) return;

                var status = LeagueUtils.GetLeagueStatus();
                if (status == null) return;

                var byteArray = Encoding.ASCII.GetBytes("riot:" + status.Item1);
                HTTP_CLIENT.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                try
                {
                    var testEndpoint = "lol-summoner/v1/current-summoner";
                    var test = await HTTP_CLIENT.GetAsync("wss://127.0.0.1:" + status.Item2 + "/" + testEndpoint);
                    Console.WriteLine(await test.Content.ReadAsStringAsync());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }


                processInfo = status;
                connected = true;

                OnConnected?.Invoke();
            }
            catch (Exception e)
            {
                processInfo = null;
                connected = false;
                Debug.WriteLine($"Exception occurred trying to connect to League of Legends: {e.ToString()}");
            }
        }

        public void Subscribe(string URI, Action<OnWebsocketEventArgs> args)
        {
            if (!Subscribers.ContainsKey(URI))
            {
                Subscribers.Add(URI, new List<Action<OnWebsocketEventArgs>>() {args});
            }
            else
            {
                Subscribers[URI].Add(args);
            }
        }

        public void Unsubscribe(string URI, Action<OnWebsocketEventArgs> action)
        {
            if (Subscribers.ContainsKey(URI))
            {
                if (Subscribers[URI].Count == 1)
                {
                    Subscribers.Remove(URI);
                }
                else if (Subscribers[URI].Count > 1)
                {
                    foreach (var item in Subscribers[URI].ToArray())
                    {
                        if (item == action)
                        {
                            var index = Subscribers[URI].IndexOf(action);
                            Subscribers[URI].RemoveAt(index);
                        }
                    }
                }
                else
                {
                    return;
                }
            }
        }


        private void TryConnectOrRetry()
        {
            if (connected) return;
            TryConnect();

            Task.Delay(2000).ContinueWith(a => TryConnectOrRetry());
        }

        private void HandleDisconnect(object sender, CloseEventArgs args)
        {
            processInfo = null;
            connected = false;

            OnDisconnected?.Invoke();

            TryConnectOrRetry();
        }

        private void HandleMessage(object sender, MessageEventArgs args)
        {
            if (!args.IsText) return;
            var payload = SimpleJson.DeserializeObject<JsonArray>(args.Data);

            if (payload.Count != 3) return;
            if ((long) payload[0] != 8 || !((string) payload[1]).Equals("OnJsonApiEvent")) return;

            var ev = (dynamic) payload[2];
            OnWebsocketEvent?.Invoke(new OnWebsocketEventArgs()
            {
                Path = ev["uri"],
                Type = ev["eventType"],
                Data = ev["eventType"] == "Delete" ? null : ev["data"]
            });
            if (Subscribers.ContainsKey((string) ev["uri"]))
            {
                foreach (var item in Subscribers[(string) ev["uri"]])
                {
                    item(new OnWebsocketEventArgs()
                    {
                        Path = ev["uri"],
                        Type = ev["eventType"],
                        Data = ev["eventType"] == "Delete" ? null : ev["data"]
                    });
                }
            }
        }

        public async Task<byte[]> GetAsset(string url)
        {
            if (!connected) throw new InvalidOperationException("Not connected to LCU");

            var res = await HTTP_CLIENT.GetAsync("https://127.0.0.1:" + processInfo.Item2 + url);
            return await res.Content.ReadAsByteArrayAsync();
        }

        static class LeagueUtils
        {
            private static Regex AUTH_TOKEN_REGEX = new Regex("\"--remoting-auth-token=(.+?)\"");
            private static Regex PORT_REGEX = new Regex("\"--app-port=(\\d+?)\"");

            public static Tuple<string, string> GetLeagueStatus()
            {
                foreach (var p in Process.GetProcessesByName("LeagueClientUx"))
                {
                    var osVersion = Environment.OSVersion;
                    if (
                        osVersion.Platform ==
                        PlatformID
                            .Win32NT) // Win32NT is Windows NT or later, all other windows values are not in use (link: https://docs.microsoft.com/en-us/dotnet/api/system.platformid?view=net-5.0)
                    {
                        using (var mos =
                            new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " +
                                                         p.Id.ToString()))
                        using (var moc = mos.Get())
                        {
                            var commandLine = (string) moc.OfType<ManagementObject>().First()["CommandLine"];

                            try
                            {
                                var authToken = AUTH_TOKEN_REGEX.Match(commandLine).Groups[1].Value;
                                var port = PORT_REGEX.Match(commandLine).Groups[1].Value;
                                return new Tuple<string, string>
                                (
                                    authToken,
                                    port
                                );
                            }
                            catch (Exception e)
                            {
                                DebugLogger.Global.WriteError(
                                    $"Error while trying to get the status for LeagueClientUx: {e.ToString()}\n\n(CommandLine = {commandLine})");
                            }
                        }
                    }
                    else if (osVersion.Platform == PlatformID.Unix || osVersion.Platform == PlatformID.MacOSX)
                    {
                        string command = "ps -A | grep LeagueClientUx";
                        Process proc = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "/bin/bash",
                                Arguments = "-c \"" + command + "\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                CreateNoWindow = true
                            }
                        };

                        proc.Start();
                        proc.WaitForExit();

                        string result = proc.StandardOutput.ReadToEnd();
                        var portRegex = Regex.Match(result, "--app-port=([0-9]*)");
                        var tokenRegex = Regex.Match(result, "--remoting-auth-token=([\\w-]*)");
                        string port = portRegex.Value.Split('=')[1];
                        string token = tokenRegex.Value.Split('=')[1];
                        return new Tuple<string, string>(token, port);
                    }
                }

                return null;
            }
        }
    }

    public class DebugLogger
    {
        public static DebugLogger Global = new DebugLogger("global.txt");

        private StreamWriter writer;

        public DebugLogger(string fileName)
        {
            writer = new StreamWriter(
                Path.Combine(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "testCopyPasta"),
                    fileName), true);
            writer.AutoFlush = true;
            writer.WriteLine($"\n\n\n --- {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")} --- ");
            writer.WriteLine($"Started logging to {fileName}...");
        }

        public void WriteError(string error)
        {
            writer.WriteLine($"[ERROR {DateTime.Now.ToString("HH:mm:ss")}] {error}");
        }

        public void WriteMessage(string message)
        {
            writer.WriteLine($"[MSG {DateTime.Now.ToString("HH:mm:ss")}] {message}");
        }

        public void WriteWarning(string warning)
        {
            writer.WriteLine($"[WRN {DateTime.Now.ToString("HH:mm:ss")}] {warning}");
        }
    }
}