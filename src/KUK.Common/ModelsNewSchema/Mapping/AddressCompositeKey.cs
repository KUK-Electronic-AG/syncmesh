namespace KUK.Common.ModelsNewSchema.Mapping
{
    public struct AddressCompositeKey : IEquatable<AddressCompositeKey>
    {
        public string Street { get; }
        public string City { get; }
        public string State { get; }
        public string Country { get; }
        public string PostalCode { get; }

        public AddressCompositeKey(string street, string city, string state, string country, string postalCode)
        {
            Street = street;
            City = city;
            State = state;
            Country = country;
            PostalCode = postalCode;
        }

        public override bool Equals(object obj)
        {
            return obj is AddressCompositeKey key && Equals(key);
        }

        public bool Equals(AddressCompositeKey other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(Street ?? string.Empty, other.Street ?? string.Empty) &&
                   StringComparer.OrdinalIgnoreCase.Equals(City ?? string.Empty, other.City ?? string.Empty) &&
                   StringComparer.OrdinalIgnoreCase.Equals(State ?? string.Empty, other.State ?? string.Empty) &&
                   StringComparer.OrdinalIgnoreCase.Equals(Country ?? string.Empty, other.Country ?? string.Empty) &&
                   StringComparer.OrdinalIgnoreCase.Equals(PostalCode ?? string.Empty, other.PostalCode ?? string.Empty);
        }

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(Street ?? string.Empty);
                hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(City ?? string.Empty);
                hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(State ?? string.Empty);
                hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(Country ?? string.Empty);
                hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(PostalCode ?? string.Empty);
                return hash;
            }
        }

        public override string ToString()
        {
            return $"Street={Street}, City={City}, State={State}, Country={Country}, PostalCode={PostalCode}";
        }
    }
}