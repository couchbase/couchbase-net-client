using System.ComponentModel;

namespace Couchbase.Core.IO.Authentication
{
     /// <summary>
    /// Supported SASL authentication types supported by Couchbase Server.
    /// </summary>
    public enum MechanismType
    {
        /// <summary>
        /// The username and password will be sent encrypted using salted Sha512 and will not be human-readable on the wire.
        /// </summary>
        [Description("SCRAM-SHA512")]
        ScramSha512,

        /// <summary>
        /// The username and password will be sent encrypted using salted Sha256 and will not be human-readable on the wire.
        /// </summary>
        [Description("SCRAM-SHA256")]
        ScramSha256,

        /// <summary>
        /// The username and password will be sent encrypted using salted Sha1 and will not be human-readable on the wire.
        /// </summary>
        [Description("SCRAM-SHA1")]
        ScramSha1,

        /// <summary>
        /// The username and password will be sent encrypted using CramMD5 and will not be human-readable on the wire.
        /// </summary>
        [Description("CRAM-MD5")]
        CramMd5,

        /// <summary>
        /// The username and password will be sent using human-readable plain text.
        /// </summary>
        [Description("PLAIN")]
        Plain
    }
}
