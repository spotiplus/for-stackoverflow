using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Collections.Concurrent;
using System.Threading;

namespace test
{
    public class clients
    {
        private readonly ConcurrentDictionary<string, HttpClient> Clients;
        public SemaphoreSlim Locker;
        private CancellationTokenSource TokenSource = new CancellationTokenSource();

        public HttpClientHandler[] handler { get; set; }
        public string[] address { get; set; }
        public string[] port { get; set; }
        public string[] username { get; set; }
        public string[] password { get; set; }
        public int MaxConcurrentDownloads { get; set; }

        private void initializeHandler(string address = "", string port = "", string user = "", string pass = "")
        {
            initializeHandler(new string[] { string.Concat(address, ":", port, ":", user, ":", pass) });
        }

        private void initializeHandler(string[] proxies_client)
        {
            if (proxies_client == null || proxies_client.Length == 0)
                return;

            this.address = new string[proxies_client.Length];
            this.port = new string[proxies_client.Length];
            this.username = new string[proxies_client.Length];
            this.password = new string[proxies_client.Length];
            for (int i = 0; i < proxies_client.Length; i++)
            {
                var split = proxies_client[i].Split(new char[] { ':' });

                this.address[i] = split[0] != "" ? split[0] : "";
                this.port[i] = split[1] != "" ? split[1] : "";
                this.username[i] = split[2] != "" ? split[2] : "";
                this.password[i] = split[3] != "" ? split[3] : "";
            }

            var proxies = new WebProxy[proxies_client.Length];
            NetworkCredential[] credential = new NetworkCredential[proxies_client.Length];
            for (int i = 0; i < proxies_client.Length; i++)
            {
                if (this.username[i] != "")
                    credential[i] = new NetworkCredential(this.username[i], this.password[i]);
                else
                    credential[i] = CredentialCache.DefaultNetworkCredentials;
            }

            const string protocol = "http://";
            for (int i = 0; i < proxies.Length; i++)
            {
                if (this.address[i] != "")
                {
                    var uri = proxies_client[i].Split(new char[] { ':' });
                    if (!uri[0].Contains(protocol))
                        uri[0] = string.Concat(protocol, uri[0]);
                    proxies[i] = new WebProxy()
                    {
                        Address = new Uri(string.Concat(uri[0], ":", uri[1])),
                        Credentials = credential[i],
                    };
                }
            };

            this.handler = new HttpClientHandler[proxies.Length];
            for (int i = 0; i < proxies.Length; i++)
            {
                if (proxies[i].Address.AbsoluteUri != "")
                    this.handler[i] = new HttpClientHandler() { Proxy = proxies[i] };
                else
                    this.handler[i] = new HttpClientHandler();
            }
        }

        public HttpClientHandler GethandlerIndexed(int index)
        {
            return (this.handler[index % this.handler.Length]);
        }

        public void SetConcurrentDownloads(int nb = 1)
        {
            Locker = new SemaphoreSlim(nb, nb);
        }
        public clients(string[] proxies = null)
        {
            Clients = new ConcurrentDictionary<string, HttpClient>();

            if (Locker is null)
                Locker = new SemaphoreSlim(1, 1);
            if (proxies != null)
                initializeHandler(proxies);
        }

        private async Task<HttpClient> CreateClient(string Name, bool persistent, CancellationToken token)
        {
            if (Clients.ContainsKey(Name))
                return Clients[Name];

            HttpClient newClient = new HttpClient();

            if (persistent)
            {
                while (Clients.TryAdd(Name, newClient) is false)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(1, token);
                }
            }

            return newClient;
        }

        public async Task<T> NewRequest<T>(Func<HttpClient, T> Expression, int? MaxTimeout = 2000, string Id = null)
        {
            await Locker.WaitAsync(MaxTimeout ?? 2000, TokenSource.Token);

            bool persistent = true;
            if (Id is null)
            {
                persistent = false;
                Id = string.Empty;
            }

            try
            {
                HttpClient client = await CreateClient(Id, persistent, TokenSource.Token);
                T result = await Task.Run<T>(() => Expression(client), TokenSource.Token);

                if (persistent is false)
                    client?.Dispose();

                return result;
            }
            finally
            {
                Locker.Release();
            }
        }

        public void AssignDefaultHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/65.0.3325.181 Safari/537.36");
            //client.Timeout = TimeSpan.FromSeconds(3);
        }

        public async Task Cancel(string Name)
        {
            if (Clients.ContainsKey(Name))
            {
                CancellationToken token = TokenSource.Token;
                HttpClient foundClient;

                while (Clients.TryGetValue(Name, out foundClient) is false)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(1, token);
                }

                if (foundClient != null)
                {
                    foundClient?.Dispose();
                }
            }
        }

        public void ForceCancelAll()
        {
            TokenSource?.Cancel();
            TokenSource?.Dispose();
            TokenSource = new CancellationTokenSource();

            foreach (var item in Clients)
            {
                item.Value?.Dispose();
            }

            Clients.Clear();
        }
    }
}
