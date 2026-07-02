using System;

namespace GooGalaxy.Runtime.Shared.Types
{
    public readonly struct CardId : IEquatable<CardId>
    {
        public static readonly CardId Empty = new(string.Empty);

        private readonly string _value;

        public CardId(string value)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value), "CardId cannot wrap a null string.");
        }

        public string Value => _value ?? string.Empty;

        public static bool operator ==(CardId left, CardId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CardId left, CardId right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return obj is CardId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value;
        }

        public bool Equals(CardId other)
        {
            if (ReferenceEquals(_value, other._value))
            {
                return true;
            }

            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }
    }
}
