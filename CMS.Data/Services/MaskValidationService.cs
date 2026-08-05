// ================================================================================
// ARCHIVO: CMS.Data/Services/MaskValidationService.cs
// PROPÓSITO: Validación de máscaras de consecutivos según formato * y 9
// DESCRIPCIÓN: Valida que las máscaras cumplan con el formato correcto y que
//              initial_value, final_value y length sean consistentes con la mask
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2026-06-23
// ================================================================================

using System.Text.RegularExpressions;

namespace CMS.Data.Services
{
    /// <summary>
    /// Servicio de validación de máscaras de consecutivos
    /// REGLAS DE MÁSCARA:
    /// - * → Alfanumérico (A-Z, 0-9)
    /// - 9 → Dígito numérico (0-9)
    /// - Otros caracteres → Literales (-, /, etc.)
    /// </summary>
    public static class MaskValidationService
    {
        /// <summary>
        /// Valida que una máscara sea válida (solo *, 9, y literales)
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateMask(string mask)
        {
            if (string.IsNullOrWhiteSpace(mask))
                return (false, "La máscara no puede estar vacía.");

            // La máscara solo puede contener *, 9, y caracteres literales (-, /, espacios, etc.)
            var invalidChars = mask.Where(c => c != '*' && c != '9' && !IsLiteralChar(c)).ToList();

            if (invalidChars.Any())
            {
                return (false, $"La máscara contiene caracteres inválidos: {string.Join(", ", invalidChars.Distinct())}. " +
                               $"Solo se permiten: * (alfanumérico), 9 (dígito), y caracteres literales (-, /, etc.).");
            }

            // Debe tener al menos un * o un 9
            if (!mask.Contains('*') && !mask.Contains('9'))
            {
                return (false, "La máscara debe contener al menos un * (alfanumérico) o un 9 (dígito).");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Valida que un valor coincida con la máscara
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateValueAgainstMask(string mask, string value, string fieldName)
        {
            if (value.Length != mask.Length)
            {
                return (false, $"{fieldName} debe tener {mask.Length} caracteres según la máscara (actualmente tiene {value.Length}).");
            }

            for (int i = 0; i < mask.Length; i++)
            {
                char maskChar = mask[i];
                char valueChar = value[i];

                if (maskChar == '*') // Alfanumérico
                {
                    if (!char.IsLetterOrDigit(valueChar))
                    {
                        return (false, $"{fieldName}: El carácter en posición {i + 1} debe ser alfanumérico (A-Z o 0-9), pero es '{valueChar}'.");
                    }
                }
                else if (maskChar == '9') // Dígito
                {
                    if (!char.IsDigit(valueChar))
                    {
                        return (false, $"{fieldName}: El carácter en posición {i + 1} debe ser un dígito (0-9), pero es '{valueChar}'.");
                    }
                }
                else // Literal
                {
                    if (valueChar != maskChar)
                    {
                        return (false, $"{fieldName}: El carácter en posición {i + 1} debe ser '{maskChar}', pero es '{valueChar}'.");
                    }
                }
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Valida que el length coincida con la máscara
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateLengthAgainstMask(string mask, int length)
        {
            int maskLength = mask.Length;

            if (length != maskLength)
            {
                return (false, $"El campo 'length' debe ser {maskLength} (longitud de la máscara), pero es {length}.");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Valida todos los campos de un consecutivo
        /// </summary>
        public static (bool IsValid, List<string> Errors) ValidateConsecutive(
            string mask, 
            string initialValue, 
            string finalValue, 
            int length)
        {
            var errors = new List<string>();

            // 1. Validar máscara
            var maskValidation = ValidateMask(mask);
            if (!maskValidation.IsValid)
                errors.Add(maskValidation.ErrorMessage);

            // 2. Validar length contra máscara
            var lengthValidation = ValidateLengthAgainstMask(mask, length);
            if (!lengthValidation.IsValid)
                errors.Add(lengthValidation.ErrorMessage);

            // 3. Validar initial_value contra máscara
            var initialValidation = ValidateValueAgainstMask(mask, initialValue, "Initial Value");
            if (!initialValidation.IsValid)
                errors.Add(initialValidation.ErrorMessage);

            // 4. Validar final_value contra máscara
            var finalValidation = ValidateValueAgainstMask(mask, finalValue, "Final Value");
            if (!finalValidation.IsValid)
                errors.Add(finalValidation.ErrorMessage);

            // 5. Validar que initial_value < final_value
            if (string.Compare(initialValue, finalValue, StringComparison.Ordinal) >= 0)
            {
                errors.Add($"Initial Value ('{initialValue}') debe ser menor que Final Value ('{finalValue}').");
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// Determina si un carácter es literal (no * ni 9)
        /// </summary>
        private static bool IsLiteralChar(char c)
        {
            // Caracteres literales comunes: -, /, \, _, espacios, puntos, etc.
            return c == '-' || c == '/' || c == '\\' || c == '_' || c == ' ' || 
                   c == '.' || c == ',' || c == ':' || c == ';' ||
                   char.IsPunctuation(c) || char.IsWhiteSpace(c);
        }

        /// <summary>
        /// Obtiene la longitud esperada de una máscara
        /// </summary>
        public static int GetMaskLength(string mask)
        {
            return mask.Length;
        }

        /// <summary>
        /// Genera un ejemplo de valor según la máscara
        /// </summary>
        public static string GenerateExample(string mask)
        {
            var example = new char[mask.Length];

            for (int i = 0; i < mask.Length; i++)
            {
                char maskChar = mask[i];

                if (maskChar == '*')
                    example[i] = 'A'; // Ejemplo: letra A
                else if (maskChar == '9')
                    example[i] = '0'; // Ejemplo: dígito 0
                else
                    example[i] = maskChar; // Literal
            }

            return new string(example);
        }
    }
}
