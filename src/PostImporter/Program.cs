using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using ServiceStack.Text;
using Topshelf;
using Timer = System.Timers.Timer;

namespace PostImporter
{
    public class Program
    {
        static void Main(string[] args)
        {
            /*HostFactory.Run(x =>                                 //1
            {
                x.Service<PostImporter>(s =>                        //2
                {
                    s.ConstructUsing(name => new PostImporter());     //3
                    s.WhenStarted(tc => tc.Start());              //4
                    s.WhenStopped(tc => tc.Stop());               //5
                });
                x.RunAsLocalSystem();                            //6
                x.SetDescription("Sample Topshelf Host");        //7
                x.SetDisplayName("Stuff");                       //8
                x.SetServiceName("stuff");                       //9
            });*/
            new PostImporter().Start();
            new ManualResetEvent(false).WaitOne();
            Console.ReadLine();
        }
    }

    public class AppDotNetResponse
    {
        public ResponseMetaData meta { get; set; }
        public JsonObject[] data { get; set; }

    }

    public class ResponseMetaData
    {
        public int code { get; set; }
        public long max_id { get; set; }
        public long min_id { get; set; }
        public bool more { get; set; }
    }

    public class PostImporter
    {
        private const string APPDOTNET_STREAM_URL = "https://alpha-api.app.net/stream/0/posts/stream/global";
        private const int CONCURRENT_REQUESTS = 7;
        private const string EVENT_STORE_STREAM_NAME = "AppDotNetPosts";

        private FileCheckpoint _checkpoint;
        private readonly ILogger _logger;
        private IEventStoreConnection _connection;
        private bool _shuttingDown;

        //quick fix for https issue on mono
        public static bool Validator(object sender, X509Certificate certificate, X509Chain chain,
                                      SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public PostImporter()
        {
            _checkpoint = new FileCheckpoint("postsLoaded");

            _logger = new EventStore.ClientAPI.Common.Log.ConsoleLogger();

            var _connectionSettings =
                ConnectionSettings.Create()
                                  .UseConsoleLogger()
                                  .KeepReconnecting()
                                  .KeepRetrying()
                                  .OnConnected(_ => _logger.Info("Event Store Connected"))
                                  .OnDisconnected(_ => _logger.Error("Event Store Disconnected"))
                                  .OnReconnecting(_ => _logger.Info("Event Store Reconnecting"))
                                  .OnErrorOccurred((c, e) => _logger.Error(e, "Event Store Error :("));

            _connection = EventStoreConnection.Create(_connectionSettings, new IPEndPoint(IPAddress.Parse("192.81.222.61"), 1113));
            _connection.Connect();

            ThreadPool.SetMaxThreads(20, 20);
            ThreadPool.SetMinThreads(20, 20);
            //ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.ServerCertificateValidationCallback = Validator;
            //ServicePointManager.EnableDnsRoundRobin = false;
            //ServicePointManager.DnsRefreshTimeout = Int32.MaxValue;
        }

        public void LoadPosts()
        {
            int pauseMs = 0;
            while (true)
            {

                if (pauseMs > 500)
                    Thread.Sleep(pauseMs);

                try
                {

                    _logger.Info("Starting new request");

                    var requestPosition = _checkpoint.Read();

                    var taskList = new List<Task>();
                    for (int i = 0; i < CONCURRENT_REQUESTS; i++)
                    {
                        var request = BuildRequest(requestPosition);
                        var task = Task<WebResponse>.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null);
                        taskList.Add(task);
                        requestPosition += 200;
                    }
                    var responseTasks = taskList.ToArray();

                    _logger.Info("Waiting for response async");

                    Task.WaitAll(responseTasks);

                    var sw = Stopwatch.StartNew();

                    bool requestFault = false;

                    var faultedTasks = responseTasks.Where(t => t.IsFaulted).Select(t => t.Exception);

                    if (faultedTasks.Any())
                    {
                        _logger.Error(faultedTasks.First(), "Request task faulted :(");
                        pauseMs = 5000;
                        continue;
                    }

                    var responses =
                        responseTasks.AsParallel()
                                     .AsOrdered()
                                     .Select(t => ((Task<WebResponse>) t).Result as HttpWebResponse);
                    var parsedResponses = responses.AsParallel().AsOrdered().Select(ParseResponse).ToList();

                    foreach (var response in responses)
                        response.Close();

                    //write events

                    //filter out empty responses

                    foreach (var resp in parsedResponses)
                    {
                        //if (resp.meta.more == false)
                        //{
                        //    for (int i = 0; i < 10; i++)
                        //        Console.WriteLine("!!!!!!!!!!!!!!!!!CAUGHT UP!!!!!!!!!!!!!!!!");
                        //    
                        //}
                        var events = resp.data.Select(post => BuildEventData(post.SerializeToString())).Reverse();

                        if (events.Count() != 0)
                        {
                            _connection.AppendToStream(EVENT_STORE_STREAM_NAME, ExpectedVersion.Any, events);
                            Console.WriteLine(resp.meta.max_id);

                            _checkpoint.Write(resp.meta.max_id);
                            _checkpoint.Flush();
                            
                        }
                    }



                    //run again based on rate limit headers

                    sw.Stop();

                    decimal remaining = 0;
                    decimal resetSeconds = 0;

                    Decimal retryIn = 1;

                    var remainings =
                        responses.Select(r => decimal.Parse(r.Headers["X-RateLimit-Remaining"])).ToList();
                    remaining = Enumerable.Min(remainings) - 1;

                    var resettings =
                        responses.Select(r => decimal.Parse(r.Headers["X-RateLimit-Reset"])).ToList();
                    resetSeconds = Enumerable.Min(resettings);

                    _logger.Info("{0} requests remaining", remaining);
                    _logger.Info("{0} seconds until reset", resetSeconds);

                    if (remaining != 0 && remaining >= CONCURRENT_REQUESTS)
                    {
                        //retryIn = (resetSeconds/ (remaining / CONCURRENT_REQUESTS))*1000;
                    }
                    else
                    {
                        //we have hit rate limit somehow
                        if (resetSeconds != 0)
                            retryIn = (resetSeconds + 1)*1000;
                    }

                    retryIn = retryIn - sw.ElapsedMilliseconds;

                    if (retryIn <= 0)
                        retryIn = 1;

                    
                    _logger.Info("next load in {0:0.#} milliseconds", retryIn);
                    pauseMs = (int) retryIn;

                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected error, waiting 5s to retry");
                    pauseMs = 5000;
                }
            }
        }

        private EventData BuildEventData(string post)
        {
            return new EventData(Guid.NewGuid(),"AppDotNetPost",true,Encoding.UTF8.GetBytes(post),new byte[]{});
        }

        public HttpWebRequest BuildRequest(long startingPost)
        {
            var url = APPDOTNET_STREAM_URL + "?since_id=" + startingPost + "&before_id=" + (startingPost + 201) + "&count=200";
            //load posts
            var request = (HttpWebRequest)WebRequest.Create(url);
            //request.Headers.Add("Accept-Encoding", "gzip,deflate");
            request.Method = "GET";
            request.Timeout = 5000;
            request.Headers.Add("X-ADN-Pretty-JSON","1");
            return request;
        }

        public AppDotNetResponse ParseResponse(HttpWebResponse response)
        {
            if (response.StatusCode != HttpStatusCode.OK || response.ContentLength == 0)
            {
                //check headers for retry
                var retryAfterHeader = response.Headers["Retry-After"];

                int retrySeconds = 10;
                if (retryAfterHeader != null)
                    int.TryParse(retryAfterHeader, out retrySeconds);
                throw new  PauseException("Bad Status code",retrySeconds * 1000);
            }

            //presumably success ... parse json
            var stream = response.GetResponseStream();

            if (stream == null)
            {
                _logger.Debug("Null response stream for ");
                throw new Exception("Bad Response stream");
            }

            var reader = new StreamReader(response.GetResponseStream());
            string result = reader.ReadToEnd();

            var parsed = (AppDotNetResponse)result.FromJson<AppDotNetResponse>();
            return parsed;
        }


        public void Start()
        {
            LoadPosts();
        }

        public void Stop()
        {
            _shuttingDown = true;
        }
    }
}
