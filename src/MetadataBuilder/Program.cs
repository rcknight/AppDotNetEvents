using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace MetadataBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            var meta = StreamMetadata.Create(1000, TimeSpan.FromHours(24));
            Console.WriteLine(meta.AsJsonString());
            Console.ReadLine();
        }
    }
}
