using System;

namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// Stable, string-backed identifier for a card, used as the shared vocabulary across assemblies so nothing has
    /// to depend on <c>CardDataSO</c> to name a card.
    /// </summary>
    /// <remarks>
    /// A default-constructed <see cref="CardId" /> carries a null backing field; <see cref="Value" /> coalesces it to
    /// <see cref="string.Empty" /> so no caller observes a null string from a legally constructed value. That is also
    /// why <see cref="Equals(CardId)" /> compares through <see cref="Value" /> rather than the backing field: a
    /// default value and an explicitly empty one must compare equal.
    /// </remarks>
    public readonly struct CardId : IEquatable<CardId>
    {
        /// <summary>The empty card id, equal to a default-constructed value.</summary>
        public static readonly CardId Empty = new(string.Empty);

        private readonly string _value;

        /// <summary>Wraps a card id string.</summary>
        /// <param name="value">The id to wrap. Must not be null.</param>
        /// <exception cref="ArgumentNullException">The value is null.</exception>
        public CardId(string value)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value), "CardId cannot wrap a null string.");
        }

        /// <summary>The wrapped id, or <see cref="string.Empty" /> for a default-constructed value.</summary>
        public string Value => _value ?? string.Empty;

        public static bool operator ==(CardId left, CardId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CardId left, CardId right)
        {
            return !left.Equals(right);
        }

        public bool Equals(CardId other)
        {
            if (ReferenceEquals(_value, other._value))
            {
                return true;
            }

            return string.Equals(Value, other.Value, StringComparison.Ordinal);
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
    }
}
