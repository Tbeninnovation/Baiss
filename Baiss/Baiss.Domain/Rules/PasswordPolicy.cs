// namespace Baiss.Domain.Rules;

// /// <summary>
// /// Password policy rules and validation
// /// </summary>
// public class PasswordPolicy
// {
//     public int MinimumLength { get; }
//     public bool RequireUppercase { get; }
//     public bool RequireLowercase { get; }
//     public bool RequireDigits { get; }
//     public bool RequireSpecialCharacters { get; }
//     public int MaximumLength { get; }

//     public PasswordPolicy(
//         int minimumLength = 8,
//         bool requireUppercase = true,
//         bool requireLowercase = true,
//         bool requireDigits = true,
//         bool requireSpecialCharacters = true,
//         int maximumLength = 128)
//     {
//         MinimumLength = minimumLength;
//         RequireUppercase = requireUppercase;
//         RequireLowercase = requireLowercase;
//         RequireDigits = requireDigits;
//         RequireSpecialCharacters = requireSpecialCharacters;
//         MaximumLength = maximumLength;
//     }

//     /// <summary>
//     /// Validates a password against the policy rules
//     /// </summary>
//     /// <param name="password">The password to validate</param>
//     /// <returns>Validation result with success status and error messages</returns>
//     public PasswordValidationResult Validate(string password)
//     {
//         var errors = new List<string>();

//         if (string.IsNullOrEmpty(password))
//         {
//             errors.Add("Password cannot be empty");
//             return new PasswordValidationResult(false, errors);
//         }

//         // Check minimum length
//         if (password.Length < MinimumLength)
//         {
//             errors.Add($"Password must be at least {MinimumLength} characters long");
//         }

//         // Check maximum length
//         if (password.Length > MaximumLength)
//         {
//             errors.Add($"Password cannot be longer than {MaximumLength} characters");
//         }

//         // Check uppercase requirement
//         if (RequireUppercase && !password.Any(char.IsUpper))
//         {
//             errors.Add("Password must contain at least one uppercase letter");
//         }

//         // Check lowercase requirement
//         if (RequireLowercase && !password.Any(char.IsLower))
//         {
//             errors.Add("Password must contain at least one lowercase letter");
//         }

//         // Check digits requirement
//         if (RequireDigits && !password.Any(char.IsDigit))
//         {
//             errors.Add("Password must contain at least one digit");
//         }

//         // Check special characters requirement
//         if (RequireSpecialCharacters && !password.Any(IsSpecialCharacter))
//         {
//             errors.Add("Password must contain at least one special character (!@#$%^&*()_+-=[]{}|;:,.<>?)");
//         }

//         return new PasswordValidationResult(errors.Count == 0, errors);
//     }

//     /// <summary>
//     /// Checks if a character is considered a special character
//     /// </summary>
//     private static bool IsSpecialCharacter(char c)
//     {
//         return "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c);
//     }

//     /// <summary>
//     /// Gets password strength score (0-100)
//     /// </summary>
//     /// <param name="password">The password to evaluate</param>
//     /// <returns>Password strength score</returns>
//     public int GetPasswordStrength(string password)
//     {
//         if (string.IsNullOrEmpty(password))
//             return 0;

//         int score = 0;

//         // Length contribution (max 40 points)
//         score += Math.Min(password.Length * 2, 40);

//         // Character variety contribution
//         if (password.Any(char.IsUpper)) score += 10;
//         if (password.Any(char.IsLower)) score += 10;
//         if (password.Any(char.IsDigit)) score += 10;
//         if (password.Any(IsSpecialCharacter)) score += 10;

//         // Bonus for good length
//         if (password.Length >= 12) score += 10;
//         if (password.Length >= 16) score += 10;

//         return Math.Min(score, 100);
//     }

//     /// <summary>
//     /// Gets a default password policy for the application
//     /// </summary>
//     public static PasswordPolicy Default => new PasswordPolicy();

//     /// <summary>
//     /// Gets a strict password policy
//     /// </summary>
//     public static PasswordPolicy Strict => new PasswordPolicy(
//         minimumLength: 12,
//         requireUppercase: true,
//         requireLowercase: true,
//         requireDigits: true,
//         requireSpecialCharacters: true,
//         maximumLength: 128);

//     /// <summary>
//     /// Gets a relaxed password policy
//     /// </summary>
//     public static PasswordPolicy Relaxed => new PasswordPolicy(
//         minimumLength: 6,
//         requireUppercase: false,
//         requireLowercase: true,
//         requireDigits: false,
//         requireSpecialCharacters: false,
//         maximumLength: 256);
// }

// /// <summary>
// /// Result of password validation
// /// </summary>
// public record PasswordValidationResult(bool IsValid, IReadOnlyList<string> Errors)
// {
//     public string GetErrorMessage()
//     {
//         return string.Join(Environment.NewLine, Errors);
//     }

//     public bool HasErrors => Errors.Count > 0;
// }
