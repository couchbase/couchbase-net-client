using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using SaslStart = Couchbase.Core.IO.Operations.Authentication.SaslStart;
using SequenceGenerator = Couchbase.Core.IO.Operations.SequenceGenerator;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    internal class PlainSaslMechanism : SaslMechanismBase
    {
        private readonly string _username;
        private readonly string _password;

        public PlainSaslMechanism(string username, string password, ILogger<PlainSaslMechanism> logger)
        {
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            Logger = logger;
            MechanismType = MechanismType.Plain;
        }

        /// <inheritdoc />
        public override async Task AuthenticateAsync(IConnection connection, CancellationToken cancellationToken = default)
        {
            using var op = new SaslStart
            {
                Key = MechanismType.GetDescription(),
                Content = GetAuthData(_username, _password),
                Opaque = SequenceGenerator.GetNext(),
                Transcoder = new LegacyTranscoder()
            };

            await SendAsync(op, connection, cancellationToken).ConfigureAwait(false);
        }

        static string GetAuthData(string userName, string passWord)
        {
            const string empty = "\0";
            var sb = new StringBuilder();
            sb.Append(userName);
            sb.Append(empty);
            sb.Append(userName);
            sb.Append(empty);
            sb.Append(passWord);
            return sb.ToString();
        }
    }
}
