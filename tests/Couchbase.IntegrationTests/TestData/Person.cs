using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Couchbase.IntegrationTests.Utils;
using Newtonsoft.Json;

namespace Couchbase.IntegrationTests.TestData
{
    
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

        public static Person Create()
        {
            return ResourceHelper.ReadResource<Person>(@"Data\person.json");
        }
    }
}
