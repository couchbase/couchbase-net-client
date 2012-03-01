using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Configuration;
using Couchbase;
using log4net;
using Enyim.Caching.Memcached;
using System.Diagnostics;
using System.IO;
using ServiceStack.Text;
using ServiceStack.Text.Json;

namespace CouchbaseSample
{

    public class Program
    {

        private static readonly ILog logger = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {

            log4net.Config.XmlConfigurator.Configure();

            //Manually configure CouchbaseClient
            //May also use app/web.config section
            var config = new CouchbaseClientConfiguration();
            config.Bucket = "default";
            config.BucketPassword = "";
            config.Urls.Add(new Uri("http://localhost:8091/pools/default"));
            config.DesignDocumentNameTransformer = new ProductionModeModeNameTransformer();
            config.HttpClientFactory = new HammockHttpClientFactory();

            //Quick test of Store/Get operations
            var client = new CouchbaseClient(config);
            var result = client.Store(StoreMode.Set, "foo", "bar");

            Debug.Assert(result, "Store failed");
            Console.WriteLine("Item saved successfully");

            var value = client.Get<string>("foo");
            Debug.Assert(value == "bar", "Get failed");
            Console.WriteLine("Item retrieved succesfully");

            processJson(client);

            Console.WriteLine("\r\n\r\n***  SAMPLE VIEWS MUST BE CREATED - SEE SampleViews.js in Data directory ***");

            Console.WriteLine("\r\n\r\nRequesting view all_breweries");
            var allBreweries = client.GetView<Brewery>("breweries", "all_breweries");
            foreach (var item in allBreweries)
            {
                Console.WriteLine(item.Name);
            }

            Console.WriteLine("\r\n\r\nRequesting view beers_by_name");
            var beersByName = client.GetView<Beer>("beers", "beers_by_name").StartKey("T");
            foreach (var item in beersByName)
            {
                Console.WriteLine(item.Name);
            }

            Console.WriteLine("\r\n\r\nRequesting view beers_by_name_and_abv");
            var beersByNameAndABV = client.GetView<Beer>("beers", "beers_by_name_and_abv")
                                       .StartKey(new object[] { "T", 6 });
            foreach (var item in beersByNameAndABV)
            {
                Console.WriteLine(item.Name);
            }

        }

        private static void processJson(CouchbaseClient client)
        {

            var dict = new Dictionary<string, string>();
            Action<string> process = s =>
            {
                using (StreamReader reader = new StreamReader(@"..\..\Data\" + s + ".json"))
                {

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {

                        var jsonLine = JsonSerializer.DeserializeFromString(line, typeof(JsonObject));
                        var jsonDict = jsonLine.ToStringDictionary();
                        var id = jsonDict["_id"];

                        //_id is reserved and can't be in JSON document
                        var idPropAndVal = "\"_id\" : \"" + id + "\", ";

                        //HACK://This is just data loading code, it's OK, right?
                        if (s == "breweries")
                        {
                            var key = id.Split('_')[0];
                            dict[key] = id;
                        }
                        else if (s == "beers")
                        {
                            var bid = jsonDict["breweryId"];

                            if (dict.ContainsKey(bid))
                            {
                                line = line.Replace(bid.ToString(), dict[bid]);
                                Console.WriteLine(line);
                            }
                            else
                            {
                                continue;
                            }
                        }

                        client.Store(StoreMode.Set, id, line.Replace(idPropAndVal, ""));
                    }
                }
            };

            process("breweries");
            process("beers");
            //using (StreamReader reader = new StreamReader(@"..\..\Data\SampleDocuments.json")) {

            //    string line;
            //    while ((line = reader.ReadLine()) != null) {

            //        var jsonLine = JsonSerializer.DeserializeFromString(line, typeof(JsonObject));
            //        var id = jsonLine.ToStringDictionary()["_id"];

            //        //_id is reserved and can't be in JSON document
            //        var idPropAndVal = "\"_id\" : \"" + id + "\", ";

            //        client.Remove(id);
            //        //client.Store(StoreMode.Set, id, line.Replace(idPropAndVal, ""));
            //    }

            //}
        }
    }
}
