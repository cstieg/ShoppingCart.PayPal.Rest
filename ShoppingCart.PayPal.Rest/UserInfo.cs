using System.Runtime.Serialization;

namespace Cstieg.ShoppingCart.PayPal.Rest
{
    /// <summary>
    /// Class mapping json results returned from PayPal Identity UserInfo API call
    /// </summary>
    [DataContract]
    public class UserInfo
    {
        [DataMember(Name = "user_id")]
        public string UserId { get; set; }

        [DataMember(Name = "verified")]
        public string Verified { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "given_name")]
        public string GivenName { get; set; }

        [DataMember(Name = "family_name")]
        public string FamilyName { get; set; }

        [DataMember(Name = "language")]
        public string Language { get; set; }

        [DataMember(Name = "locale")]
        public string Locale { get; set; }

        [DataMember(Name = "zoneinfo")]
        public string ZoneInfo { get; set; }

        [DataMember(Name = "birthday")]
        public string Birthday { get; set; }

        [DataMember(Name = "email")]
        public string Email { get; set; }

        [DataMember(Name = "email_verified")]
        public string EmailVerified { get; set; }

        [DataMember(Name = "address")]
        public PayPalAddress Address { get; set; }
    }
}