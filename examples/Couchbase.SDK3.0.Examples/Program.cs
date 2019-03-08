using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.SDK3._0.Examples
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Task.Run(async () =>
            {
                try
                {
                    var cluster = new Cluster();
                    var task = cluster.Initialize(
                        new Configuration()
                            .WithServers("couchbase://127.0.0.1")
                            .WithBucket("default")
                            .WithCredentials("Administrator", "password")
                    );
                    task.ConfigureAwait(false);
                    task.Wait();

                    var bucket = await cluster.Bucket("default");
                    var collection = await bucket.DefaultCollection;

                    await BasicCrud(collection);
                    await BasicProjection(collection);
                    await BasicDurability(collection);
                    await BasicQuery(cluster);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });

            Console.WriteLine("Hello World!");
            Console.Read();
        }

        private static async Task BasicQuery(ICluster cluster)
        {
            var statement = "SELECT * FROM `default` WHERE type=$type";

            var results = await cluster.Query<Person>(
                statement,
                parameters => parameters.Add("type", "person"),
                options => options.Timeout(new TimeSpan(0, 0, 0, 75)));

            foreach (var result in results) Console.WriteLine(result);
        }

        private static async Task BasicCrud(ICollection collection)
        {
            var id = "p01";

            //upsert a new person
            var result = await collection.Upsert(id, new Person
            {
                name = "Joan Deere",
                age = 28,
                animals = new List<string> {"kitty", "puppy"},
                attributes = new Attributes
                {
                    dimensions = new Dimensions
                    {
                        height = 65,
                        weight = 120
                    },
                    hair = "brown",
                    hobbies = new List<Hobby>
                    {
                        new Hobby
                        {
                            name = "Curling",
                            type = "winter",
                            details = new Details
                            {
                                location = new Location
                                {
                                    @long = -121.886330,
                                    lat = 37.338207
                                }
                            }
                        }
                    }
                }
            }, options => options.Expiration = new TimeSpan(1, 0, 0, 0));

            var get = await collection.Get(id,
                options => options.Timeout = new TimeSpan(0, 0, 0, 10));

            var person = get.ContentAs<Person>();
            Console.WriteLine(person.name);
        }

        private static async Task BasicDurability(ICollection collection)
        {
            var id = "p02";
            var result = await collection.Insert(id, new Person
            {
                name = "Jon Henry",
                age = 34
            }, options =>
            {
                options.DurabilityLevel = DurabilityLevel.MajorityAndPersistActive;
                options.Timeout = new TimeSpan(0, 0, 0, 30);
            });

            Console.WriteLine(result.Cas);
        }

        private static async Task BasicProjection(ICollection collection)
        {
            var id = "p01";

            var result = await collection.Get(id,
                options => options.Project("name", "age", "attributes.hair"));

            var person = result.ContentAs<Person>();
            Console.WriteLine("Age={person.age}, Name={person.name}, Hair{person.attributes.hair}");
        }

        public class Dimensions
        {
            public int height { get; set; }
            public int weight { get; set; }
        }

        public class Location
        {
            public double lat { get; set; }
            public double @long { get; set; }
        }

        public class Details
        {
            public Location location { get; set; }
        }

        public class Hobby
        {
            public string type { get; set; }
            public string name { get; set; }
            public Details details { get; set; }
        }

        public class Attributes
        {
            public string hair { get; set; }
            public Dimensions dimensions { get; set; }
            public List<Hobby> hobbies { get; set; }
        }

        public class Person
        {
            public string name { get; set; }
            public int age { get; set; }
            public List<string> animals { get; set; }
            public Attributes attributes { get; set; }
            public string type => "person";
        }
    }
}