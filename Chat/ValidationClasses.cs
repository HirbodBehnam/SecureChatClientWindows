using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Chat
{
    public static class Validation
    {
        /// <summary>
        /// Checks if the username is ok; Username must not be empty, it must be letter or digits or _; No white space is allowed
        /// </summary>
        /// <param name="value">String to test</param>
        /// <returns>True if it's valid; Otherwise false</returns>
        public static bool UsernameValidation(string value)
        {
            bool empty = string.IsNullOrWhiteSpace(value ?? "");
            bool charsOk = (value ?? "")
                .All(ch => (char.IsLetterOrDigit(ch) || ch == '_') && !char.IsWhiteSpace(ch));
            return !empty && charsOk;
        }
    }
    /// <summary>
    /// Checks if a field is empty or not
    /// </summary>
    public class NotEmptyValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            return string.IsNullOrWhiteSpace((value ?? "").ToString())
                ? new ValidationResult(false, "Field is required.")
                : ValidationResult.ValidResult;
        }
    }

    /// <summary>
    /// A validation rule to check the username. Username must not be empty, it must be letter or digits or _
    /// </summary>
    public class UsernameValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            return Validation.UsernameValidation((value ?? "").ToString())
                ? ValidationResult.ValidResult
                : new ValidationResult(false, "Invalid characters");
        }
    }
}
