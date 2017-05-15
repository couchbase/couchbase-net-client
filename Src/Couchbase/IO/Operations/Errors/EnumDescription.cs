using System;

namespace Couchbase.IO.Operations.Errors
{
    /// <summary>
    /// Attribute that provides a description to for an enum entry.
    /// </summary>
    /// <remarks>Custom implementation because "System.ComponentModel.DescriptionAttribute" not available in .NET Core.</remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EnumDescription : Attribute
    {
        private readonly string _description;

        public string Description { get { return _description; } }

        public EnumDescription(string description)
        {
            _description = description;
        }
    }
}