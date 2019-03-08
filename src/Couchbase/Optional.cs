using System;
using System.Collections.Generic;

namespace Couchbase
{
    public struct Optional<T> : IEquatable<T>
    {
        private readonly T _value;

        public Optional(T value)
        {
            _value = value;
            HasValue = true;
        }

        public bool HasValue { get; }

        public T Value
        {
            get
            {
                if (HasValue) return _value;
                throw new InvalidOperationException();
            }
        }

        public static explicit operator T(Optional<T> optional)
        {
            return optional.Value;
        }

        public static implicit operator Optional<T>(T value)
        {
            return new Optional<T>(value);
        }

        public bool Equals(T other)
        {
            return EqualityComparer<T>.Default.Equals(_value, other);
        }

        public override bool Equals(object obj)
        {
            return Equals((Optional<T>)obj);
        }

      /*  protected bool Equals(Optional<T> other)
        {
            return EqualityComparer<T>.Default.Equals(_value, other._value) && HasValue == other.HasValue;
        }*/

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<T>.Default.GetHashCode(_value) * 397) ^ HasValue.GetHashCode();
            }
        }

        public static bool operator ==(Optional<T> left, Optional<T> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Optional<T> left, Optional<T> right)
        {
            return !Equals(left, right);
        }
    }
}
