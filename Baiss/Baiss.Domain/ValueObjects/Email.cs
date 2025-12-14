// using System.Text.RegularExpressions;

// namespace Baiss.Domain.ValueObjects;

// /// <summary>
// /// Email value object ensuring valid email format
// /// </summary>
// public class Email : IEquatable<Email>
// {
//     private static readonly Regex EmailRegex = new Regex(
//         @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
//         RegexOptions.Compiled | RegexOptions.IgnoreCase);

//     public string Value { get; }

//     private Email(string value)
//     {
//         Value = value;
//     }

//     /// <summary>
//     /// Creates an Email value object with validation
//     /// </summary>
//     /// <param name="email">The email string to validate</param>
//     /// <returns>Email value object if valid, null otherwise</returns>
//     public static Email? Create(string email)
//     {
//         if (string.IsNullOrWhiteSpace(email))
//             return null;

//         var trimmedEmail = email.Trim().ToLowerInvariant();

//         if (!IsValidEmail(trimmedEmail))
//             return null;

//         return new Email(trimmedEmail);
//     }

//     /// <summary>
//     /// Creates an Email value object with validation, throws exception if invalid
//     /// </summary>
//     /// <param name="email">The email string to validate</param>
//     /// <returns>Email value object</returns>
//     /// <exception cref="ArgumentException">Thrown when email format is invalid</exception>
//     public static Email CreateRequired(string email)
//     {
//         var result = Create(email);
//         if (result == null)
//             throw new ArgumentException("Invalid email format", nameof(email));

//         return result;
//     }

//     /// <summary>
//     /// Validates email format using regex
//     /// </summary>
//     private static bool IsValidEmail(string email)
//     {
//         if (string.IsNullOrWhiteSpace(email))
//             return false;

//         if (email.Length > 254) // RFC 5321 limit
//             return false;

//         return EmailRegex.IsMatch(email);
//     }

//     /// <summary>
//     /// Gets the domain part of the email
//     /// </summary>
//     public string GetDomain()
//     {
//         var atIndex = Value.LastIndexOf('@');
//         return atIndex > 0 ? Value.Substring(atIndex + 1) : string.Empty;
//     }

//     /// <summary>
//     /// Gets the local part of the email (before @)
//     /// </summary>
//     public string GetLocalPart()
//     {
//         var atIndex = Value.LastIndexOf('@');
//         return atIndex > 0 ? Value.Substring(0, atIndex) : Value;
//     }

//     public bool Equals(Email? other)
//     {
//         if (other is null) return false;
//         return Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);
//     }

//     public override bool Equals(object? obj)
//     {
//         return Equals(obj as Email);
//     }

//     public override int GetHashCode()
//     {
//         return Value.GetHashCode(StringComparison.OrdinalIgnoreCase);
//     }

//     public override string ToString()
//     {
//         return Value;
//     }

//     public static bool operator ==(Email? left, Email? right)
//     {
//         return Equals(left, right);
//     }

//     public static bool operator !=(Email? left, Email? right)
//     {
//         return !Equals(left, right);
//     }

//     public static implicit operator string(Email email)
//     {
//         return email.Value;
//     }
// }
