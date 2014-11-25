using System.Collections.Generic;

namespace Couchbase.Tests.Documents
{
    /// <summary>
    /// 'Brewery' POCO for testing
    /// </summary>
    public class Brewery
    {
        public string Name { get; set; }

        public string City { get; set; }

        public string State { get; set; }

        public string Code { get; set; }

        public string Country { get; set; }

        public string Phone { get; set; }

        public string Website { get; set; }

        public string Type { get; set; }

        public string Updated { get; set; }

        public string Description { get; set; }

        public List<string> Address { get; set; }

        public Geo Geo { get; set; }
        /*
          "name": "21st Amendment Brewery Cafe",
          "city": "San Francisco",
          "state": "California",
          "code": "94107",
          "country": "United States",
          "phone": "1-415-369-0900",
          "website": "http://www.21st-amendment.com/",
          "type": "brewery",
          "updated": "2010-10-24 13:54:07",
          "description": "The 21st Amendment Brewery offers a variety of award winning house made brews and American grilled cuisine in a comfortable loft like setting. Join us before and after Giants baseball games in our outdoor beer garden. A great location for functions and parties in our semi-private Brewers Loft. See you soon at the 21A!",
          "address": [
            "563 Second Street"
          ],
          "geo": {
            "accuracy": "ROOFTOP",
            "lat": 37.7825,
            "lon": -122.393
         */
    }
}
