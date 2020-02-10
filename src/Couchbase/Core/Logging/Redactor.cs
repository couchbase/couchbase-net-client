using System;

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Represents a logging argument that is redactable.
    /// </summary>
    internal class Redactor : IRedactor
    {
        private static readonly string _user = "ud";
        private static readonly string _meta = "md";
        private static readonly string _system = "sd";

        public Redactor(ClusterContext context)
        {
            RedactionLevel = context.ClusterOptions.RedactionLevel;
        }

        public RedactionLevel RedactionLevel { get; }

        public object UserData(object message)
        {
            return RedactMessage(message, _user);
        }

        public object MetaData(object message)
        {
            return RedactMessage(message, _meta);
        }

        public object SystemData(object message)
        {
            return RedactMessage(message, _system);
        }

        private object RedactMessage(object message, string redactionType)
        {
            var redact = false;
            var redactionLevel = RedactionLevel;
            switch (redactionLevel)
            {
                case RedactionLevel.Full:
                    redact = true;
                    break;
                case RedactionLevel.None:
                    break;
                case RedactionLevel.Partial:
                    redact = redactionType == _user;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(Enum.GetName(typeof(RedactionLevel), redactionLevel),
                        "Unexpected redaction level: {redactionLevel}");
            }

            return redact
                ? $"<{redactionType}>{message}</{redactionType}>"
                : message;
        }
    }
}
