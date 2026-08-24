// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/ClaveNumericaGenerator.cs
// PROPÓSITO: Generación atómica de la Clave Numérica de 50 dígitos (Hacienda CR v4.4)
// DESCRIPCIÓN: Construye la clave siguiendo el formato oficial y obtiene el
//              consecutivo fiscal con bloqueo Serializable (mismo patrón de
//              concurrencia que ConsecutiveService) para evitar duplicados.
//
//   ESTRUCTURA CLAVE (50 díg.):
//     [3] país 506 + [2] día + [2] mes + [2] año + [12] cédula emisor +
//     [20] consecutivo + [1] situación + [8] código de seguridad
//
//   ESTRUCTURA CONSECUTIVO (20 díg.):
//     [3] sucursal + [5] terminal + [2] tipo doc + [10] secuencia
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Data;
using System.Security.Cryptography;
using CMS.Entities.Operational;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services.EInvoice
{
    /// <inheritdoc cref="IClaveNumericaGenerator"/>
    public class ClaveNumericaGenerator : IClaveNumericaGenerator
    {
        private const string CountryCode = "506";

        private readonly ICompanyDbContextFactory _companyDbContextFactory;
        private readonly ILogger<ClaveNumericaGenerator> _logger;

        public ClaveNumericaGenerator(
            ICompanyDbContextFactory companyDbContextFactory,
            ILogger<ClaveNumericaGenerator> logger)
        {
            _companyDbContextFactory = companyDbContextFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<ClaveNumericaResult> GenerateAsync(
            int companyId,
            int issuerId,
            string issuerIdentification,
            string documentType,
            string branch,
            string terminal,
            string situation,
            DateTime issueDate,
            int userId,
            int? consecutiveId = null)
        {
            await using var db = await _companyDbContextFactory.CreateDbContextAsync(companyId);

            // Bloqueo Serializable para garantizar unicidad del consecutivo en concurrencia.
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                ElectronicDocumentConsecutive? consec = null;

                // 0) Si el usuario seleccionó un consecutivo específico en la pantalla de emisión,
                //    usar exactamente ese registro (validando emisor, tipo y que esté activo).
                if (consecutiveId.HasValue && consecutiveId.Value > 0)
                {
                    consec = await db.ElectronicDocumentConsecutives
                        .Where(c => c.Id == consecutiveId.Value &&
                                    c.IdBillingIssuer == issuerId &&
                                    c.DocumentType == documentType &&
                                    c.IsActive)
                        .FirstOrDefaultAsync();

                    if (consec == null)
                        throw new InvalidOperationException(
                            $"El consecutivo fiscal seleccionado (id {consecutiveId.Value}) no existe, no está activo " +
                            $"o no corresponde al emisor {issuerId} y tipo de documento '{documentType}'.");
                }

                // 1) Buscar el consecutivo exacto por emisor+sucursal+terminal+tipo (activo).
                consec ??= await db.ElectronicDocumentConsecutives
                    .Where(c => c.IdBillingIssuer == issuerId &&
                                c.Branch == branch &&
                                c.Terminal == terminal &&
                                c.DocumentType == documentType &&
                                c.IsActive)
                    .OrderByDescending(c => c.IsDefault)
                    .FirstOrDefaultAsync();

                // 2) Si no hay coincidencia exacta, usar el registro DEFAULT activo del tipo.
                consec ??= await db.ElectronicDocumentConsecutives
                    .Where(c => c.IdBillingIssuer == issuerId &&
                                c.DocumentType == documentType &&
                                c.IsActive &&
                                c.IsDefault)
                    .FirstOrDefaultAsync();

                if (consec == null)
                {
                    throw new InvalidOperationException(
                        $"No existe un consecutivo fiscal activo configurado para el emisor {issuerId}, " +
                        $"tipo de documento '{documentType}' (sucursal '{branch}', terminal '{terminal}'). " +
                        "Configúrelo en la pantalla de mantenimiento de Consecutivos de Documento Electrónico.");
                }

                var nextSequence = consec.Consecutive + 1;
                consec.Consecutive = nextSequence;
                consec.UpdatedBy = "ClaveNumericaGenerator";
                consec.RecordDate = DateTime.UtcNow;

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                // El consecutivo se arma con los datos reales del registro (por si se usó el default).
                var consecutive = BuildConsecutive(consec.Branch, consec.Terminal, documentType, nextSequence);
                var clave = BuildClave(issuerIdentification, consecutive, situation, issueDate);

                _logger.LogInformation(
                    "🔑 Clave Numérica generada: {Clave} (consecutivo {Consecutive})", clave, consecutive);

                return new ClaveNumericaResult(clave, consecutive, nextSequence);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error generando Clave Numérica para emisor {IssuerId}", issuerId);
                throw;
            }
        }

        /// <summary>Construye el consecutivo de 20 díg.: [3]sucursal+[5]terminal+[2]tipo+[10]secuencia.</summary>
        private static string BuildConsecutive(string branch, string terminal, string documentType, long sequence)
        {
            var b = branch.PadLeft(3, '0')[..3];
            var t = terminal.PadLeft(5, '0')[..5];
            var d = documentType.PadLeft(2, '0')[..2];
            var s = sequence.ToString().PadLeft(10, '0');
            if (s.Length > 10)
                throw new InvalidOperationException("El consecutivo fiscal excedió 10 dígitos (10^10).");
            return $"{b}{t}{d}{s}";
        }

        /// <summary>Construye la Clave Numérica de 50 díg.</summary>
        private static string BuildClave(string issuerIdentification, string consecutive, string situation, DateTime issueDate)
        {
            var day = issueDate.ToString("dd");
            var month = issueDate.ToString("MM");
            var year = issueDate.ToString("yy");
            var ident = new string(issuerIdentification.Where(char.IsDigit).ToArray()).PadLeft(12, '0');
            if (ident.Length > 12) ident = ident[^12..];
            var security = GenerateSecurityCode();
            var sit = situation.PadLeft(1, '0')[..1];

            var clave = $"{CountryCode}{day}{month}{year}{ident}{consecutive}{sit}{security}";
            if (clave.Length != 50)
                throw new InvalidOperationException($"Clave Numérica inválida ({clave.Length} díg.): {clave}");
            return clave;
        }

        /// <summary>Genera el código de seguridad de 8 dígitos aleatorios.</summary>
        private static string GenerateSecurityCode()
        {
            var value = RandomNumberGenerator.GetInt32(0, 100_000_000);
            return value.ToString().PadLeft(8, '0');
        }
    }
}
