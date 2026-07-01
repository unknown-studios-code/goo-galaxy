using System;

namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// Type-safe readonly struct wrapping a string for card identification.
    /// Provides zero-allocation comparison by reference and ordinal string evaluation.
    /// </summary>
    public readonly struct CardId : IEquatable<CardId>
    {
        public static readonly CardId Empty = new(string.Empty);

        private readonly string _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="CardId"/> struct.
        /// </summary>
        /// <param name="value">The string value of the card identifier.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided value is null.</exception>
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

        /// <summary>
        /// Compares this instance with an object for equality.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the object is a CardId and represents the same identifier.</returns>
        public override bool Equals(object obj) => obj is CardId other && Equals(other);

        /// <summary>
        /// Returns the hash code for this card identifier.
        /// </summary>
        /// <returns>A hash code matching the wrapped string's hash code.</returns>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Converts the card identifier to its string representation.
        /// </summary>
        /// <returns>The wrapped string value.</returns>
        public override string ToString() => Value;

        /// <summary>
        /// Compares this instance with another <see cref="CardId"/> for equality.
        /// </summary>
        /// <param name="other">The other card identifier to compare.</param>
        /// <returns>True if they represent the same identifier, otherwise false.</returns>
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
