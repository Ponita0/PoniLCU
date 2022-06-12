using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Management;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Text;
using WebSocketSharp;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PoniLCU
{
    public class OnWebsocketEventArgs : EventArgs
    {   // URI    
        public string Path { get; set; }

        // Update create delete     
        public string Type { get; set; }

        // data :D
        public dynamic Data { get; set; }
    }

    public class LeagueClient
    {
        #region some_ENUMS
        public enum credentials
        {
            lockfile,
            cmd,
            uxLogs
        }
        public enum requestMethod
        {
            GET, POST, PATCH, DELETE, PUT
        }
        #endregion
        #region important_variabls
        private static HttpClient client;

        private Dictionary<string, List<Action<OnWebsocketEventArgs>>> Subscribers = new Dictionary<string, List<Action<OnWebsocketEventArgs>>>();

        private WebSocket socketConnection;

        private Tuple<Process, string, string> processInfo;

        private bool connected;

        public event Action OnConnected;

        public event Action OnDisconnected;

        public event Action<OnWebsocketEventArgs> OnWebsocketEvent;

        public bool IsConnected => connected;

        static credentials _Method;
        static string _ClientRoot;  // Path of "League of Ledgends.exe" or "英雄联盟.exe"

        private static Regex AUTH_TOKEN_REGEX = new Regex("\"--remoting-auth-token=(.+?)\"");

        private static Regex PORT_REGEX = new Regex("\"--app-port=(\\d+?)\"");

        private static Regex LOGS_REGEX = new Regex("--remoting-auth-token=([a-zA-Z0-9_]*)\\r\\n\\t--app-port=([0-9]*)");
        #endregion

        public LeagueClient(credentials method, string clientRoot = "")
        {
            _Method = method;
            _ClientRoot = clientRoot;
            //we initialize the http client
            try
            {
                client = new HttpClient(new HttpClientHandler()
                {
                    SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls,
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true
                });
            }
            catch
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                client = new HttpClient(new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true
                });
            }
            //wait before we start initializing the connections 
            Task.Delay(2000).ContinueWith(e => TryConnectOrRetry());
            var trytimes = 0;
            while (!IsConnected)
            {
                if (trytimes != 2)
                {
                    trytimes++;
                    TryConnectOrRetry();

                }
                else
                {
                    Debug.WriteLine("Connection timed out ");
                    break;
                }
                Task.Delay(1000).GetAwaiter().GetResult();
            }
        }
        //the method to do requests based on parameters
        public Task<string> Request(requestMethod method, string url, object body = null)
        {
            if (!connected) throw new InvalidOperationException("Not connected to LCU");
            string RequestMethod;

            switch (method)
            {
                case requestMethod.GET:
                    RequestMethod = "GET";
                    body = null;
                    break;
                case requestMethod.POST:
                    RequestMethod = "POST";
                    break;
                case requestMethod.PATCH:
                    RequestMethod = "PATCH";
                    break;
                case requestMethod.DELETE:
                    RequestMethod = "DELETE";
                    break;
                case requestMethod.PUT:
                    RequestMethod = "PUT";
                    break;
                default:
                    RequestMethod = "post";
                    break;
            }
            // to give the user the ability to write the uri with or without the '/' in start
            if (url[0] != '/')
            {
                url = "/" + url;
            }

            return client.SendAsync(new HttpRequestMessage(new HttpMethod(RequestMethod), "https://127.0.0.1:" + processInfo.Item3 + url)
            {
                Content = body == null ? null : new StringContent(body.ToString(), Encoding.UTF8, "application/json")
            }).Result.Content.ReadAsStringAsync();
        }

        public async Task<dynamic> getStringJsoned(string url)
        {
            if (!connected) throw new InvalidOperationException("Not connected to LCU");

            var res = await client.GetAsync("https://127.0.0.1:" + processInfo.Item3 + url);
            var stringContent = await res.Content.ReadAsStringAsync();

            if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return JsonConvert.DeserializeObject(stringContent);
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

        public KeyValuePair<string, string>
            CreateAuthorizationHeader(ICredentials credentials)
        {
            NetworkCredential networkCredential =
                credentials.GetCredential(null, null);

            string userName = networkCredential.UserName;
            string userPassword = networkCredential.Password;

            string authInfo = userName + ":" + userPassword;
            authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));

            return new KeyValuePair<string, string>("Authorization", "Basic " + authInfo);
        }
        private void TryConnect()
        {
            try
            {
                if (connected) return;

                var status = GetLeagueStatus();
                if (status == null) return;

                processInfo = status;
                var byteArray = Encoding.ASCII.GetBytes("riot:" + status.Item2);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                socketConnection = new WebSocket("wss://127.0.0.1:" + status.Item3 + "/", "wamp");
                socketConnection.SetCredentials("riot", status.Item2, true);

                socketConnection.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
                socketConnection.SslConfiguration.ServerCertificateValidationCallback = (a, b, c, d) => true;
                socketConnection.OnMessage += HandleMessage;
                socketConnection.OnClose += HandleDisconnect;
                socketConnection.Connect();
                socketConnection.Send($"[5, \"OnJsonApiEvent\"]");

                processInfo = status;
                connected = true;

                OnConnected?.Invoke();


            }
            catch (Exception e)
            {
                processInfo = null;
                connected = false;
                if (!connected) throw new InvalidOperationException($"Exception occurred trying to connect to League of Legends: {e.ToString()}");
            }
        }


        private void HandleDisconnect(object sender, CloseEventArgs args)
        {
            processInfo = null;
            connected = false;
            socketConnection = null;

            OnDisconnected?.Invoke();

            TryConnectOrRetry();
        }

        private void HandleMessage(object sender, MessageEventArgs args)
        {
            if (!args.IsText) return;
            var payload = JsonConvert.DeserializeObject<JArray>(args.Data);

            if (payload.Count != 3) return;
            if ((long)payload[0] != 8 || !((string)payload[1]).Equals("OnJsonApiEvent")) return;

            var ev = (dynamic)payload[2];
            OnWebsocketEvent?.Invoke(new OnWebsocketEventArgs()
            {
                Path = ev["uri"],
                Type = ev["eventType"],
                Data = ev["eventType"] == "Delete" ? null : ev["data"]
            });
            if (Subscribers.ContainsKey((string)ev["uri"]))
            {
                foreach (var item in Subscribers[(string)ev["uri"]])
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
        public void Subscribe(string URI, Action<OnWebsocketEventArgs> args)
        {
            if (!Subscribers.ContainsKey(URI))
            {
                Subscribers.Add(URI, new List<Action<OnWebsocketEventArgs>>() { args });
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

        private Tuple<Process, string, string> GetLeagueStatus()
        {
            if (_Method == credentials.uxLogs)
            {
                DirectoryInfo dirInfo;
                FileInfo[] fileInfo;
                FileInfo latestUxLog;
                string uxLogsContent;

                dirInfo = new DirectoryInfo(_ClientRoot);
                fileInfo = dirInfo.GetFiles("LeagueClient.exe", SearchOption.TopDirectoryOnly);

                // 1st： Riot server, 2nd: Tencent server
                string uxLogsFolder = fileInfo.Length != 0 ? Path.Combine(_ClientRoot, "Logs\\LeagueClient Logs") : Path.Combine(_ClientRoot, "Game\\Logs\\LeagueClient Logs");

                dirInfo = new DirectoryInfo(uxLogsFolder);
                fileInfo = dirInfo.GetFiles("*LeagueClientUx.log", SearchOption.TopDirectoryOnly);
                latestUxLog = fileInfo.Last();

                using (var stream = File.Open(latestUxLog.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    uxLogsContent = reader.ReadToEnd();
                }

                var match = LOGS_REGEX.Match(uxLogsContent);
                return new Tuple<Process, string, string>
                    (
                        null,
                        match.Groups[1].Value,
                        match.Groups[2].Value
                    );
            }
            else
            {
                foreach (var p in Process.GetProcessesByName("LeagueClientUx"))
                {
                    if (LeagueClient._Method == LeagueClient.credentials.cmd)
                    {
                        using (var mos = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + p.Id.ToString()))
                        using (var moc = mos.Get())
                        {
                            var commandLine = (string)moc.OfType<ManagementObject>().First()["CommandLine"];

                            try
                            {
                                var authToken = AUTH_TOKEN_REGEX.Match(commandLine).Groups[1].Value;
                                var port = PORT_REGEX.Match(commandLine).Groups[1].Value;
                                return new Tuple<Process, string, string>
                                (
                                    p,
                                    authToken,
                                    port
                                );
                            }
                            catch (Exception e)
                            {
                                throw new InvalidOperationException($"Error while trying to get the status for LeagueClientUx: {e.ToString()}\n\n(CommandLine = {commandLine})");

                            }
                        }
                    }
                    else if (_Method == LeagueClient.credentials.lockfile)
                    {
                        try
                        {
                            if (p.MainModule == null)
                                throw new Exception("The LeagueClientUx process doesn't have any main module.");

                            var processDirectory = Path.GetDirectoryName(p.MainModule.FileName);

                            if (processDirectory == null)
                                throw new Exception("Unable to get the directory name for the LeagueClientUx process.");

                            string lockfilePath = Path.Combine(processDirectory, "lockfile");

                            string lockfile;
                            using (var stream = File.Open(lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var reader = new StreamReader(stream))
                            {
                                lockfile = reader.ReadToEnd();
                            }

                            var splitContent = lockfile.Split(':');
                            return new Tuple<Process, string, string>
                            (
                                p,
                                splitContent[3],
                                splitContent[2]
                            );
                        }
                        catch (Exception e)
                        {
                            throw new InvalidOperationException($"Error while trying to get the status for LeagueClientUx: {e.ToString()})");
                        }
                    }
                }
                return null;
            }
        }
    }
}

