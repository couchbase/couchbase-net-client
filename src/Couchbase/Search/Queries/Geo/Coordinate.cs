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
