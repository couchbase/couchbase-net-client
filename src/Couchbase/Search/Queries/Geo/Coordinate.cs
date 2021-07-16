namespace Couchbase.Search.Queries.Geo
{
    /// <summary>
    ///  A coordinate is a tuple of a latitude and a longitude.
    /// </summary>
    /// <remarks>This class is Uncommitted and may change in future versions.</remarks>
    public class Coordinate
    {
        /// <summary>
        /// Constructs a coordinate given latitude and longitude.
        /// </summary>
        /// <param name="latitude">The latitude of the point as a <see cref="double"/>.</param>
        /// <param name="longitude">The longitude of the point as a <see cref="double"/>.</param>
        private Coordinate(double latitude, double longitude)
        {
            Lat = latitude;
            Lon = longitude;
        }

        /// <summary>
        /// Constructs a <see cref="Coordinate"/> object with latitude and longitude.
        /// </summary>
        /// <param name="latitude">The latitude of the point as a <see cref="double"/>.</param>
        /// <param name="longitude">The longitude of the point as a <see cref="double"/>.</param>
        /// <returns></returns>
        public static Coordinate OfLatLon(double latitude, double longitude)
        {
            return new Coordinate(latitude, longitude);
        }

        /// <summary>
        /// Returns the Latitude of this coordinate as a <see cref="double"/>.
        /// </summary>
        public double Lat { get; }

        /// <summary>
        /// Returns the Longitude of this coordinate as a <see cref="double"/>.
        /// </summary>
        public double Lon { get; }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
