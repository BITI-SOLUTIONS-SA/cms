// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/identification-validator.js
// PROPÓSITO: Validación en el cliente del número de identificación por tipo Hacienda CR.
// DESCRIPCIÓN: Refleja las reglas del validador de servidor
//              (CMS.Shared/Validation/IdentificationNumberValidator.cs):
//                01 Cédula física    -> 9 dígitos numéricos
//                02 Cédula jurídica  -> 10 dígitos numéricos
//                03 DIMEX            -> 11 o 12 dígitos numéricos
//                04 NITE             -> 10 dígitos numéricos
//                05 Extranjero       -> hasta 20 caracteres (libre)
//                06 No contribuyente -> hasta 20 caracteres (libre)
//              El "typeValue" recibido puede ser el código Hacienda ('01'..'06') o el
//              id del catálogo (1..6); se normaliza a código internamente.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

(function (global) {
    'use strict';

    // Mapa id de catálogo -> código Hacienda.
    var ID_TO_CODE = { '1': '01', '2': '02', '3': '03', '4': '04', '5': '05', '6': '06' };

    function normalizeCode(typeValue) {
        if (typeValue === null || typeValue === undefined) return '';
        var v = String(typeValue).trim();
        if (v === '') return '';
        // Si ya viene como código de 2 dígitos ('01'..'06'), se devuelve tal cual.
        if (/^0[1-6]$/.test(v)) return v;
        // Si viene como id de catálogo (1..6), se convierte al código.
        if (ID_TO_CODE.hasOwnProperty(v)) return ID_TO_CODE[v];
        return v;
    }

    function checkNumeric(value, min, max, label) {
        if (!/^[0-9]+$/.test(value)) {
            return label + ': el número de identificación debe contener solo dígitos.';
        }
        if (value.length < min || value.length > max) {
            return min === max
                ? label + ': el número de identificación debe tener ' + min + ' dígitos.'
                : label + ': el número de identificación debe tener entre ' + min + ' y ' + max + ' dígitos.';
        }
        return null;
    }

    /// Valida el número. Devuelve el mensaje de error o null si es válido.
    function validate(typeValue, identification) {
        var code = normalizeCode(typeValue);
        if (!code) {
            return 'Debe seleccionar el tipo de identificación.';
        }
        if (identification === null || identification === undefined || String(identification).trim() === '') {
            return 'El número de identificación es obligatorio.';
        }
        var value = String(identification).trim();

        switch (code) {
            case '01': return checkNumeric(value, 9, 9, 'Cédula física');
            case '02': return checkNumeric(value, 10, 10, 'Cédula jurídica');
            case '03': return checkNumeric(value, 11, 12, 'DIMEX');
            case '04': return checkNumeric(value, 10, 10, 'NITE');
            case '05':
            case '06':
                if (value.length > 20) {
                    return 'El número de identificación no puede exceder 20 caracteres.';
                }
                return null;
            default:
                return "Tipo de identificación '" + code + "' no reconocido.";
        }
    }

    /// True si es válido. Permite tratar tipo/identificación vacíos como válidos (opcional).
    function isValid(typeValue, identification, allowEmpty) {
        var code = normalizeCode(typeValue);
        var idEmpty = identification === null || identification === undefined || String(identification).trim() === '';
        if (allowEmpty && !code && idEmpty) return true;
        return validate(typeValue, identification) === null;
    }

    global.IdentificationValidator = {
        normalizeCode: normalizeCode,
        validate: validate,
        isValid: isValid
    };
})(window);
