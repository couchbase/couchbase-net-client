using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hammock;
using Newtonsoft.Json;
using Hammock.Serialization;
using System.IO;
using Couchbase;
using Enyim.Caching.Memcached;
using Couchbase.Configuration;
using System.Diagnostics;

namespace TestDataLoader
{
	class Program
	{
		private static void Dump(ArraySegment<byte> d)
		{
			var s = Encoding.UTF8.GetString(d.Array, d.Offset, d.Count);

			Console.WriteLine(s);
		}

		class Tmp
		{
			public string A { get; set; }
		}

		private static void Measure(string header, int iter, Action<int> action)
		{
			if (iter<= 0) return;

			Console.WriteLine(header);
			Console.WriteLine("--------");

			var sw = Stopwatch.StartNew();
			action(iter);
			sw.Stop();

			Console.WriteLine("{0} in {1} msec, {2:0.##} msec/item", iter, sw.ElapsedMilliseconds, (float)sw.ElapsedMilliseconds / iter);
			Console.WriteLine();
		}

		static void JsonNet(string input, int max)
		{
			var s = new JsonSerializer();

			for (var i = 0; i < max; i++)
			{
				var reader = new StringReader(input);
				var jsr = new JsonTextReader(reader);

				s.Deserialize(jsr);
			}
		}

		static void NetJavascript(string input, int max)
		{
			var s = new System.Web.Script.Serialization.JavaScriptSerializer();

			for (var i = 0; i < max; i++)
			{
				s.DeserializeObject(input);
			}
		}

		static void HammockSer(string input, int max)
		{
			for (var i = 0; i < max; i++)
			{
				Hammock.Serialization.JsonParser.FromJson(input);
			}
		}

		static void Main(string[] args)
		{
			AAAA();
		}

		static void BBBB()
		{
			var large = File.ReadAllText(@"D:\d\repo\Couchbase\CouchbaseDriver\LargeResponse.txt");
			var small = File.ReadAllText(@"D:\d\repo\Couchbase\CouchbaseDriver\MixedResponse.txt");

			JsonNet(large, 2);
			NetJavascript(large, 2);
			
			Console.WriteLine("Starting");

			const int LargeIter = 50;
			const int SmallIter = 1000;

			//Measure("Hammock Large", LargeIter, (max) => HammockSer(large, max));
			//Measure("Hammock Small", SmallIter, (max) => HammockSer(small, max));

			Measure("Json.Net Large", LargeIter, (max) => JsonNet(large, max));
			Measure("Json.Net Small", SmallIter, (max) => JsonNet(small, max));

			Measure(".Net Large", LargeIter, (max) => NetJavascript(large, max));
			Measure(".Net Small", SmallIter, (max) => NetJavascript(small, max));

			Console.ReadLine();
		}

		static void AAAA()
		{
			//ITranscoder tr = new JsonTranscoder();

			//Dump(tr.Serialize("a").Data);
			//Dump(tr.Serialize(null).Data);
			//Dump(tr.Serialize(1.0f).Data);
			//Dump(tr.Serialize(2.4d).Data);
			//Dump(tr.Serialize(08976543).Data);
			//Dump(tr.Serialize(new { A = "a", B = 2, C = true, D = new[] { 1, 2, 3, 4 } }).Data);

			//var o = tr.Deserialize(tr.Serialize(new Tmp { A = "a" }));

			//Console.WriteLine(tr.Deserialize(tr.Serialize((Single)1)).GetType());
			//Console.WriteLine(tr.Deserialize(tr.Serialize((Double)1)).GetType());


			var mbc = new CouchbaseClientConfiguration();
			mbc.Urls.Add(new Uri("http://192.168.47.128:8091/pools/default"));

			var c = new CouchbaseClient(mbc);

			//	for (var i = 0; i < 10; i++) c.Store(StoreMode.Set, "json_" + i, i + 100);
			//for (var i = 0; i < 10; i++) c.Store(StoreMode.Set, "binary_" + i, i + 100);

			//for (var i = 0; i < 1000; i++)
			//    c.Store(StoreMode.Set, "key_" + i, i);

			var r = c.GetView("test", "all").Limit(20);
			var tmp = c.Get(r);

			//Console.WriteLine(r.Count);
		}

		#region loader

		/*
		private static object CreateClient()
		{
			var settings = new JsonSerializerSettings
			{
				MissingMemberHandling = MissingMemberHandling.Ignore,
				NullValueHandling = NullValueHandling.Include,
				DefaultValueHandling = DefaultValueHandling.Include
			};

			var serializer = new HammockJsonDotNetSerializer(settings);
			var client = new RestClient
			{
				Authority = "http://ephubudw0310:5984",
				Serializer = serializer,
				Deserializer = serializer//,
				//Proxy = "http://localhost:8888"
			};

			client.AddHeader("Accept", "application/json");
			client.AddHeader("Content-Type", "application/json; charset=utf-8");

			return client;
		}

		#region create data
		static void CreateData()
		{
			var settings = new JsonSerializerSettings
			{
				MissingMemberHandling = MissingMemberHandling.Ignore,
				NullValueHandling = NullValueHandling.Include,
				DefaultValueHandling = DefaultValueHandling.Include
			};

			var serializer = new HammockJsonDotNetSerializer(settings);
			var client = new RestClient
			{
				Authority = "http://ephubudw0310:5984",
				Serializer = serializer,
				Deserializer = serializer//,
				//Proxy = "http://localhost:8888"
			};

			client.AddHeader("Accept", "application/json");
			client.AddHeader("Content-Type", "application/json; charset=utf-8");

			var tags = new[] { "taga", "tagb", "foo", "bar", "baz", "test", "demo", "helloworld" };
			var tagsOfTags = Enumerable.Range(0, 4).Select(i => tags.Skip(i).Take(4).ToArray()).ToArray();
			string ts = null;

			for (var i = 0; i < 10000; i++)
			{
				if (i % 10 == 0)
					ts = DateTime.Now.AddMinutes(i / 10).ToString("yyyy-MM-dd'T'HH:mm:ss.fffff");

				Console.WriteLine(i);
				var request = new RestRequest
				{
					Path = "dev4/" + i,
					Entity = new { first = "First" + i, last = "Last" + i, tags = tagsOfTags[i % 4], timestamp = ts },
					Method = Hammock.Web.WebMethod.Put
				};

				client.Request(request);
			}
		}

		#endregion
		 * */

		#endregion
	}
}
