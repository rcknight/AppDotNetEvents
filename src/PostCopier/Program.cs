using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Log;

namespace PostCopier
{
    class Program
    {
        static void Main(string[] args)
        {

            IEventStoreConnection _sourceConnection = null;
            IEventStoreConnection _destConnection = null;

            var _logger = new ConsoleLogger();

            var _connectionSettings =
                       ConnectionSettings.Create()
                      .UseConsoleLogger()
                      .KeepReconnecting()
                      .KeepRetrying()
                      .OnConnected(_ => _logger.Info("Event Store Connected"))
                      .OnDisconnected(_ => _logger.Error("Event Store Disconnected"))
                      .OnReconnecting(_ => _logger.Info("Event Store Reconnecting"))
                      .OnErrorOccurred((c, e) => _logger.Error(e, "Event Store Error :("));

            _sourceConnection = EventStoreConnection.Create(_connectionSettings, new IPEndPoint(IPAddress.Loopback, 1113));
            _sourceConnection.Connect();

            _destConnection = EventStoreConnection.Create(_connectionSettings, new IPEndPoint(IPAddress.Loopback, 1114));
            _destConnection.Connect();

            

            string sourceStream = "PostsToKeep";
            string destStream = "AppDotNetPosts";

            int lastPosition = 0;

            bool stop = false;

            Task lastWrite = null;

            while (true)
            {
                var slice = _sourceConnection.ReadStreamEventsForward(sourceStream, lastPosition,1024,true);

                if (slice.Events.Count() != 0)
                {
                    if(lastWrite != null)
                        lastWrite.Wait();
                    //wait for last write to complete
                    lastWrite = _destConnection.AppendToStreamAsync(destStream, ExpectedVersion.Any, slice.Events.Select(MakeEventData));
                }
                    

                lastPosition = slice.NextEventNumber;

                if (slice.IsEndOfStream)
                    break;

                Console.WriteLine(lastPosition);
            }

        }

        static EventData MakeEventData(ResolvedEvent e)
        {
            return new EventData(e.Event.EventId,"AppDotNetPost",true,e.Event.Data,e.Event.Metadata);
        }


    }
}
