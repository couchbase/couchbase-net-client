using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.Extensions
{
    internal sealed class InterfaceContractResolver : DefaultContractResolver
    {
        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            const string assemblyName = "System.Configuration.dll";
            var properties = base.GetSerializableMembers(objectType);
            var couchbaseProperties = properties.Where(x => x.Module.Name != assemblyName);
            return couchbaseProperties.ToList();
        }
    }
}
