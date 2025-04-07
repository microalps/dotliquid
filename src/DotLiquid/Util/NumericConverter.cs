using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DotLiquid.Util
{
    internal class NumericConverter
    {
        private static readonly Regex IntegerRegex = R.C(R.Q(@"^([+-]?\d+)$"));

        /// <summary>
        /// Check if the object is a floating point (real) type.
        /// </summary>
        /// <param name="value">The value to check</param>
        /// <returns>True if the value is decimal, float or double; false otherwise.</returns>
        public static bool IsReal(object value) => value is double || value is float || value is decimal;

        /// <summary>
        /// Check if the object is an integer type.
        /// </summary>
        /// <param name="value">The value to check</param>
        /// <returns>True if the value is an integer; false otherwise.</returns>
        public static bool IsInteger(object value) =>
            value is int || value is uint || value is long || value is ulong ||
            value is short || value is ushort || value is byte || value is sbyte;

        /// <summary>
        /// Check if the object is a numeric type.
        /// </summary>
        /// <param name="value">The value to check</param>
        /// <returns>True if the value is an integer or floating point number; false otherwise.</returns>
        public static bool IsNumeric(object value) => IsReal(value) || IsInteger(value);

        /// <summary>
        /// Coerce an object into a numeric type.
        /// </summary>
        /// <param name="value">The string to coerce.</param>
        /// <param name="formatProvider">The format provider for converting floating point numbers.</param>
        /// <param name="defaultValue">The value to return if coercion fails.</param>
        /// <returns>The coerced value as int, long, double or decimal type, or <paramref name="defaultValue"/> if coercion fails.</returns>
        public static object CoerceToNumericType(object value, IFormatProvider formatProvider, object defaultValue)
        {
            if (TryCoerceToNumericType(value, formatProvider, out object convertedValue))
            {
                return convertedValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Try to coerce an object into a numeric type.
        /// </summary>
        /// <param name="value">The string to parse.</param>
        /// <param name="formatProvider">The format provider for converting floating point numbers.</param>
        /// <param name="convertedValue">The coerced value as int, long, double or decimal type, or null if parsing fails.</param>
        /// <returns>true if parsing was successful; Otherwise, false.</returns>
        public static bool TryCoerceToNumericType(object value, IFormatProvider formatProvider, out object convertedValue)
        {
            if (value != null)
            {
                if (IsNumeric(value))
                {
                    convertedValue = value;
                    return true;
                }
                else if (value is string stringValue)
                {
                    return TryParseToNumericType(stringValue, formatProvider, out convertedValue);
                }
            }

            convertedValue = null;
            return false;
        }

        /// <summary>
        /// Try to parse the string into a numeric type.
        /// </summary>
        /// <param name="value">The string to parse.</param>
        /// <param name="formatProvider">The format provider for converting floating point numbers.</param>
        /// <param name="convertedValue">The coerced value as int, long, double or decimal type, or null if parsing fails.</param>
        /// <returns>true if parsing was successful; Otherwise, false.</returns>
        public static bool TryParseToNumericType(string value, IFormatProvider formatProvider, out object convertedValue)
        {
            if (value != null)
            {
                // Integer.
                if (IntegerRegex.IsMatch(value))
                {
                    if (int.TryParse(value, NumberStyles.Integer | NumberStyles.AllowThousands, formatProvider, out int intValue))
                    {
                        convertedValue = intValue;
                        return true;
                    }
                    else if (long.TryParse(value, NumberStyles.Integer | NumberStyles.AllowThousands, formatProvider, out long longValue))
                    {
                        convertedValue = longValue;
                        return true;
                    }
                }

                // Floating point numbers.

                // For cultures with "," as the decimal separator, allow
                // both "," and "." to be used as the separator.
                // First try to parse using current culture.
                // If that fails, try to parse using invariant culture.
                // Also, first try higher precision decimal.
                // If that fails, try to parse as double (precision float).
                // Double is less precise but has a larger range.
                if (decimal.TryParse(value, NumberStyles.Number | NumberStyles.Float, formatProvider, out decimal parsedDecimalCurrentCulture))
                {
                    convertedValue = parsedDecimalCurrentCulture;
                    return true;
                }

                if (decimal.TryParse(value, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsedDecimalInvariantCulture))
                {
                    convertedValue = parsedDecimalInvariantCulture;
                    return true;
                }

                if (double.TryParse(value, NumberStyles.Number | NumberStyles.Float, formatProvider, out double parsedDoubleCurrentCulture))
                {
                    convertedValue = parsedDoubleCurrentCulture;
                    return true;
                }

                if (double.TryParse(value, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDoubleInvariantCulture))
                {
                    convertedValue = parsedDoubleInvariantCulture;
                    return true;
                }
            }

            convertedValue = null;
            return false;
        }
    }
}
