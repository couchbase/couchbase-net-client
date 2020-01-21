using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using SaslStart = Couchbase.Core.IO.Operations.Authentication.SaslStart;
using SequenceGenerator = Couchbase.Core.IO.Operations.SequenceGenerator;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    public class PlainSaslMechanism : ISaslMechanism
    {
        public PlainSaslMechanism(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; }
        public string Password { get; }
        public string MechanismType => "PLAIN";

        /// <inheritdoc />
        public async Task<bool> AuthenticateAsync(IConnection connection, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>();
            using var op = new SaslStart
            {
                Key = MechanismType,
                Content = GetAuthData(Username, Password),
                Opaque = SequenceGenerator.GetNext(),
                Transcoder = new DefaultTranscoder(),
                Completed = s =>
                {
                    //Status will be AuthenticationError if auth failed otherwise false
                    tcs.TrySetResult(s.Status == ResponseStatus.Success);
                    return tcs.Task;
                }
            };

            IDisposable? cancellationTokenRegistration = null;
            if (cancellationToken.CanBeCanceled)
            {
                // Not the default, so register the callback

                cancellationTokenRegistration = cancellationToken.Register(() =>
                {
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            try
            {
                await op.SendAsync(connection).ConfigureAwait(false);
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                cancellationTokenRegistration?.Dispose();
            }
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
