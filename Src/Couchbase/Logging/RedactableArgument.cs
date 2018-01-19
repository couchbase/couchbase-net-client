using System;

namespace Couchbase.Logging
{
    /// <summary>
    /// Represents a logging argument that is redactable.
    /// </summary>
    internal class RedactableArgument
    {
        private static readonly string _user = "ud";
        private static readonly string _meta = "md";
        private static readonly string _system = "sd";

        public string RedactionType { get; }
        public object Message { get; }

        private RedactableArgument(string redactionType, object message)
        {
            RedactionType = redactionType;
            Message = message;
        }

        public static RedactableArgument User(object message)
        {
            return new RedactableArgument(_user, message);
        }

        public static RedactableArgument Meta(object message)
        {
            return new RedactableArgument(_meta, message);
        }

        public static RedactableArgument System(object message)
        {
            return new RedactableArgument(_system, message);
        }

        public static Func<object, string> UserAction = o => User(o).ToString();

        public static Func<object, string> MetaAction = o => Meta(o).ToString();

        public static Func<object, string> SystemAction = o => System(o).ToString();

        public override string ToString()
        {
            var redact = false;
            var redactionLevel = LogManager.RedactionLevel;
            switch (redactionLevel)
            {
                case RedactionLevel.Full:
                    redact = true;
                    break;
                case RedactionLevel.None:
                    break;
                case RedactionLevel.Partial:
                    redact = (RedactionType == _user);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(Enum.GetName(typeof(RedactionLevel), redactionLevel),
                        "Unexpected redaction level: {redactionLevel}");
            }

            return redact
                ? string.Concat("<", RedactionType, ">", Message, "</", RedactionType, ">")
                : Message?.ToString();
        }
    }
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
#endregion
