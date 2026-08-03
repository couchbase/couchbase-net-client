#if NET6_0_OR_GREATER

using System;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication.X509.Helpers;

/// <summary>
/// The handshake verdict plus what the validation callback actually did, so a test can tell a deliberate
/// rejection apart from a throw or from a failure that never reached the callback.
/// </summary>
internal sealed record HandshakeResult(
    bool Accepted,
    bool ValidatorInvoked,
    bool? ValidatorVerdict,
    Exception? ValidatorException);

#endif
