// ================================================================================
// ARCHIVO: CMS.Shared/Validation/IdentificationNumberValidator.cs
// PROPÓSITO: Validación del número de identificación según el tipo Hacienda CR v4.4.
// DESCRIPCIÓN: Reglas oficiales por código de tipo de identificación:
//                01 Cédula física       -> 9 dígitos numéricos
//                02 Cédula jurídica      -> 10 dígitos numéricos
//                03 DIMEX                -> 11 o 12 dígitos numéricos
//                04 NITE                 -> 10 dígitos numéricos
//                05 Extranjero no dom.   -> hasta 20 caracteres (formato libre)
//                06 No contribuyente     -> hasta 20 caracteres (formato libre)
//              Se usa tanto en la emisión de comprobantes como en los mantenimientos
//              de customer y vendor.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Text.RegularExpressions;

namespace CMS.Shared.Validation
{
    /// <summary>Validador del número de identificación por tipo Hacienda CR.</summary>
    public static class IdentificationNumberValidator
    {
        /// <summary>
        /// Valida el número de identificación según el código de tipo Hacienda ('01'..'06').
        /// </summary>
        /// <param name="typeCode">Código Hacienda del tipo de identificación (formato '00').</param>
        /// <param name="identification">Número de identificación a validar.</param>
        /// <param name="error">Mensaje de error si la validación falla; null si es válida.</param>
        /// <returns>True si el número es válido para el tipo indicado.</returns>
        public static bool TryValidate(string? typeCode, string? identification, out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(typeCode))
            {
                error = "Debe seleccionar el tipo de identificación.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(identification))
            {
                error = "El número de identificación es obligatorio.";
                return false;
            }

            var value = identification.Trim();

            switch (typeCode.Trim())
            {
                case "01": // Cédula física
                    return CheckNumeric(value, 9, 9, "Cédula física", out error);
                case "02": // Cédula jurídica
                    return CheckNumeric(value, 10, 10, "Cédula jurídica", out error);
                case "03": // DIMEX
                    return CheckNumeric(value, 11, 12, "DIMEX", out error);
                case "04": // NITE
                    return CheckNumeric(value, 10, 10, "NITE", out error);
                case "05": // Extranjero no domiciliado
                case "06": // No contribuyente
                    if (value.Length > 20)
                    {
                        error = "El número de identificación no puede exceder 20 caracteres.";
                        return false;
                    }
                    return true;
                default:
                    error = $"Tipo de identificación '{typeCode}' no reconocido.";
                    return false;
            }
        }

        private static bool CheckNumeric(string value, int min, int max, string label, out string? error)
        {
            error = null;

            if (!Regex.IsMatch(value, "^[0-9]+$"))
            {
                error = $"{label}: el número de identificación debe contener solo dígitos.";
                return false;
            }

            if (value.Length < min || value.Length > max)
            {
                error = min == max
                    ? $"{label}: el número de identificación debe tener {min} dígitos."
                    : $"{label}: el número de identificación debe tener entre {min} y {max} dígitos.";
                return false;
            }

            return true;
        }
    }
}
