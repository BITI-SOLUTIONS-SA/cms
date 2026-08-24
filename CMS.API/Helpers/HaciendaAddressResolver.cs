// ================================================================================
// ARCHIVO: CMS.API/Helpers/HaciendaAddressResolver.cs
// PROPÓSITO: Resolver códigos de ubicación Hacienda CR (provincia/cantón/distrito)
//            a nombres legibles usando el catálogo geográfico central
//            (admin.geographic_division1..3) y componer una dirección fiscal.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-04
// ================================================================================

using CMS.Data;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Helpers
{
    /// <summary>
    /// Utilidad para convertir los códigos de ubicación de Hacienda CR
    /// (provincia = 1 díg, cantón = 2 díg, distrito = 2 díg) en una dirección
    /// fiscal legible: "Provincia - Cantón - Distrito - OtrasSeñas".
    /// </summary>
    public static class HaciendaAddressResolver
    {
        /// <summary>
        /// Compone la dirección fiscal legible resolviendo los códigos contra el
        /// catálogo geográfico de Costa Rica. Si algún código no se puede resolver,
        /// se omite ese segmento. Siempre concatena "OtrasSeñas" al final si existe.
        /// </summary>
        public static async Task<string?> BuildAddressTextAsync(
            AppDbContext centralDb,
            string? provinceCode,
            string? cantonCode,
            string? districtCode,
            string? otherSigns)
        {
            var segments = new List<string>();

            // País Costa Rica (ISO2 = CR).
            var idCountryCr = await centralDb.Countries
                .Where(c => c.ISO2_CODE == "CR")
                .Select(c => (int?)c.ID_COUNTRY)
                .FirstOrDefaultAsync();

            if (idCountryCr.HasValue)
            {
                int? idDivision1 = null;
                int? idDivision2 = null;

                // Provincia (division1)
                if (!string.IsNullOrWhiteSpace(provinceCode))
                {
                    var prov = await centralDb.GeographicDivisions1
                        .Where(p => p.IdCountry == idCountryCr.Value && p.Code == provinceCode)
                        .Select(p => new { p.IdGeographicDivision1, p.Name })
                        .FirstOrDefaultAsync();
                    if (prov != null)
                    {
                        idDivision1 = prov.IdGeographicDivision1;
                        segments.Add(prov.Name);
                    }
                }

                // Cantón (division2) — dependiente de la provincia.
                if (idDivision1.HasValue && !string.IsNullOrWhiteSpace(cantonCode))
                {
                    var canton = await centralDb.GeographicDivisions2
                        .Where(c => c.IdGeographicDivision1 == idDivision1.Value && c.Code == cantonCode)
                        .Select(c => new { c.IdGeographicDivision2, c.Name })
                        .FirstOrDefaultAsync();
                    if (canton != null)
                    {
                        idDivision2 = canton.IdGeographicDivision2;
                        segments.Add(canton.Name);
                    }
                }

                // Distrito (division3) — dependiente del cantón.
                if (idDivision2.HasValue && !string.IsNullOrWhiteSpace(districtCode))
                {
                    var district = await centralDb.GeographicDivisions3
                        .Where(d => d.IdGeographicDivision2 == idDivision2.Value && d.Code == districtCode)
                        .Select(d => d.Name)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrWhiteSpace(district))
                        segments.Add(district);
                }
            }

            if (!string.IsNullOrWhiteSpace(otherSigns))
                segments.Add(otherSigns.Trim());

            return segments.Count > 0 ? string.Join(" - ", segments) : null;
        }
    }
}
