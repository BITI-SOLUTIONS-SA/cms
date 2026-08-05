// ================================================================================
// ARCHIVO: CMS.Data/Services/ConsecutiveService.cs
// PROPÓSITO: Servicio para generación automática de números consecutivos
// DESCRIPCIÓN: Implementa la lógica de búsqueda jerárquica de consecutivos
//              por menú padre/hijo y generación thread-safe de números
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-22
// ================================================================================

using CMS.Data;
using CMS.Data.Services.Interfaces;
using CMS.Entities;
using CMS.Entities.Operational;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CMS.Data.Services
{
    /// <summary>
    /// Servicio para generación automática de números consecutivos
    /// con búsqueda jerárquica por menú padre/hijo
    /// </summary>
    public class ConsecutiveService : IConsecutiveService
    {
        private readonly ICompanyDbContextFactory _companyDbContextFactory;
        private readonly AppDbContext _centralDb;
        private readonly ILogger<ConsecutiveService> _logger;

        public ConsecutiveService(
            ICompanyDbContextFactory companyDbContextFactory,
            AppDbContext centralDb,
            ILogger<ConsecutiveService> logger)
        {
            _companyDbContextFactory = companyDbContextFactory;
            _centralDb = centralDb;
            _logger = logger;
        }

        /// <summary>
        /// Genera el siguiente número consecutivo para un menú y tipo de documento
        /// Implementa búsqueda jerárquica: busca en el menú actual, si no encuentra
        /// busca en el padre, y así sucesivamente hasta encontrar o llegar a la raíz
        /// </summary>
        /// <param name="companyId">ID de la compañía</param>
        /// <param name="menuId">ID del menú donde se está creando el documento</param>
        /// <param name="entityDocumentId">ID del tipo de documento (ej: Journal Entry)</param>
        /// <param name="userId">ID del usuario que genera el consecutivo</param>
        /// <returns>Número consecutivo generado (ej: WAD000000000001)</returns>
        public async Task<string> GenerateNextNumberAsync(
            int companyId, 
            int menuId, 
            int entityDocumentId, 
            int userId)
        {
            _logger.LogInformation(
                "🔢 Generando consecutivo para Company={CompanyId}, Menu={MenuId}, EntityDocument={EntityDocumentId}",
                companyId, menuId, entityDocumentId);

            await using var companyDb = await _companyDbContextFactory.CreateDbContextAsync(companyId);

            // Usar transacción con nivel de aislamiento Serializable para evitar números duplicados
            using var transaction = await companyDb.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            try
            {
                // 1. Buscar consecutivo usando lógica jerárquica
                var consecutive = await FindConsecutiveHierarchicalAsync(
                    companyDb, menuId, entityDocumentId);

                if (consecutive == null)
                {
                    throw new InvalidOperationException(
                        $"No se encontró consecutivo para el menú {menuId} ni sus padres. " +
                        $"Debe configurar un consecutivo en Settings/Consecutives.");
                }

                _logger.LogInformation(
                    "✅ Consecutivo encontrado: Code={Code}, Mask={Mask}, Menu={MenuId}",
                    consecutive.CODE, consecutive.MASK, consecutive.ID_MENU);

                // 2. Validar integridad de la configuración
                var validation = MaskValidationService.ValidateConsecutive(
                    consecutive.MASK,
                    consecutive.INITIAL_VALUE,
                    consecutive.FINAL_VALUE,
                    consecutive.LENGTH);

                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(
                        $"Configuración de consecutivo '{consecutive.CODE}' inválida:\n" +
                        string.Join("\n", validation.Errors));
                }

                // 3. Calcular siguiente valor basado en máscara * y 9
                string currentValue = string.IsNullOrEmpty(consecutive.LAST_VALUE) 
                    ? consecutive.INITIAL_VALUE 
                    : consecutive.LAST_VALUE;

                string nextValue = IncrementMaskedValue(consecutive.MASK, currentValue);

                // 4. Validar que no exceda el límite
                ValidateFinalValue(consecutive, nextValue);

                _logger.LogInformation(
                    "🎯 Número generado: {Number}",
                    nextValue);

                // 5. Actualizar consecutivo en BD
                consecutive.LAST_VALUE = nextValue;
                consecutive.LAST_USER = userId;
                consecutive.LAST_DATE = DateTime.UtcNow;
                consecutive.UpdatedBy = "ConsecutiveService";
                consecutive.RecordDate = DateTime.UtcNow;

                await companyDb.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "💾 Consecutivo actualizado: LastValue={LastValue}, User={UserId}",
                    consecutive.LAST_VALUE, userId);

                return nextValue;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, 
                    "❌ Error generando consecutivo para Menu={MenuId}, EntityDocument={EntityDocumentId}",
                    menuId, entityDocumentId);
                throw;
            }
        }

        /// <summary>
        /// Busca un consecutivo usando lógica jerárquica:
        /// 1. Busca en el menú actual
        /// 2. Si no encuentra, busca en el menú padre
        /// 3. Repite hasta encontrar o llegar a la raíz (id_parent = 0)
        /// </summary>
        private async Task<Consecutive?> FindConsecutiveHierarchicalAsync(
            CompanyDbContext companyDb,
            int menuId,
            int entityDocumentId)
        {
            var currentMenuId = menuId;
            var visited = new HashSet<int>(); // Evitar ciclos infinitos

            while (currentMenuId > 0 && !visited.Contains(currentMenuId))
            {
                visited.Add(currentMenuId);

                _logger.LogDebug("🔍 Buscando consecutivo en menú {MenuId}", currentMenuId);

                // Buscar consecutivo en el menú actual
                var consecutive = await companyDb.Consecutives
                    .FirstOrDefaultAsync(c =>
                        c.ID_MENU == currentMenuId &&
                        c.ID_ENTITY_DOCUMENT == entityDocumentId &&
                        c.IS_ACTIVE);

                if (consecutive != null)
                {
                    _logger.LogInformation(
                        "✅ Consecutivo encontrado en menú {MenuId}: {Code}",
                        currentMenuId, consecutive.CODE);
                    return consecutive;
                }

                // No encontrado, buscar el menú padre en la BD central
                var menu = await _centralDb.Menus.FindAsync(currentMenuId);
                if (menu == null || menu.ID_PARENT == 0)
                {
                    _logger.LogWarning(
                        "⚠️ No se encontró consecutivo en menú {MenuId} ni sus padres",
                        menuId);
                    break;
                }

                _logger.LogDebug(
                    "⬆️ Menú {MenuId} no tiene consecutivo, buscando en padre {ParentId}",
                    currentMenuId, menu.ID_PARENT);

                currentMenuId = menu.ID_PARENT;
            }

            return null;
        }

        /// <summary>
        /// Incrementa un valor según la máscara de consecutivo
        /// 
        /// REGLAS DE MÁSCARA:
        /// - * → Alfanumérico (A-Z, 0-9) - se mantiene fijo, solo se incrementa al desbordar
        /// - 9 → Dígito numérico (0-9) - se incrementa como número
        /// - Otros caracteres → Literales (-, /, etc.)
        /// 
        /// EJEMPLOS:
        ///   Mask: ***999999999999, Valor: WAD000000000001 → WAD000000000002
        ///   Mask: **-9999-999, Valor: JE-0001-001 → JE-0001-002
        ///   Mask: **-9999-999, Valor: JE-0001-999 → JE-0002-000 (desborda)
        /// 
        /// DESBORDAMIENTO:
        ///   Cuando la parte numérica llega a su máximo (ej: 999 → 1000), se incrementa
        ///   la parte alfanumérica y se resetea la numérica.
        /// </summary>
        private string IncrementMaskedValue(string mask, string currentValue)
        {
            if (currentValue.Length != mask.Length)
            {
                throw new InvalidOperationException(
                    $"El valor actual '{currentValue}' ({currentValue.Length} caracteres) " +
                    $"no coincide con la longitud de la máscara '{mask}' ({mask.Length} caracteres).");
            }

            var result = currentValue.ToCharArray();
            bool carry = true;

            // Recorrer de derecha a izquierda (como suma con acarreo)
            for (int i = mask.Length - 1; i >= 0 && carry; i--)
            {
                char maskChar = mask[i];
                char valueChar = result[i];

                if (maskChar == '9') // Dígito numérico
                {
                    if (char.IsDigit(valueChar))
                    {
                        int digit = valueChar - '0';
                        digit++;

                        if (digit <= 9)
                        {
                            result[i] = (char)('0' + digit);
                            carry = false; // No más acarreo
                        }
                        else
                        {
                            result[i] = '0'; // Resetear a 0 y seguir con acarreo
                            carry = true;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"El carácter en posición {i} del valor '{currentValue}' " +
                            $"no es un dígito según la máscara '9'.");
                    }
                }
                else if (maskChar == '*') // Alfanumérico
                {
                    if (carry)
                    {
                        // Incrementar parte alfanumérica con desbordamiento
                        char nextChar = IncrementAlphanumeric(valueChar);

                        if (nextChar == '0') // Hubo desbordamiento (Z→0, 9→A)
                        {
                            result[i] = '0';
                            carry = true; // Continuar acarreo
                        }
                        else
                        {
                            result[i] = nextChar;
                            carry = false;
                        }
                    }
                }
                // Los caracteres literales (-, /, etc.) se mantienen sin cambios
            }

            // Si todavía hay acarreo al final, significa desbordamiento total
            if (carry)
            {
                // Agregar un dígito más a la derecha según lo solicitado
                return new string(result) + "0";
            }

            return new string(result);
        }

        /// <summary>
        /// Incrementa un carácter alfanumérico con lógica de desbordamiento
        /// 0→1, 1→2, ..., 9→A, A→B, ..., Z→0 (desborda)
        /// </summary>
        private char IncrementAlphanumeric(char c)
        {
            if (char.IsDigit(c))
            {
                if (c == '9')
                    return 'A'; // 9 → A
                else
                    return (char)(c + 1); // 0→1, 1→2, etc.
            }
            else if (char.IsUpper(c))
            {
                if (c == 'Z')
                    return '0'; // Z → 0 (desborda, continuar acarreo)
                else
                    return (char)(c + 1); // A→B, B→C, etc.
            }
            else if (char.IsLower(c))
            {
                // Convertir a mayúsculas y procesar
                return IncrementAlphanumeric(char.ToUpper(c));
            }

            throw new InvalidOperationException($"Carácter '{c}' no es alfanumérico válido.");
        }

        /// <summary>
        /// Valida que el siguiente valor no exceda el límite configurado
        /// </summary>
        private void ValidateFinalValue(Consecutive consecutive, string nextValue)
        {
            // Comparación simple: si nextValue supera final_value alfabéticamente, error
            if (string.Compare(nextValue, consecutive.FINAL_VALUE, StringComparison.Ordinal) > 0)
            {
                throw new InvalidOperationException(
                    $"Consecutivo agotado: El siguiente valor '{nextValue}' excede el límite '{consecutive.FINAL_VALUE}'. " +
                    $"Configure un nuevo rango en Settings/Consecutives.");
            }
        }

        /// <summary>
        /// Obtiene información del consecutivo que se usaría para un menú sin generarlo
        /// Útil para preview o validaciones
        /// </summary>
        public async Task<ConsecutiveInfo?> GetConsecutiveInfoAsync(
            int companyId,
            int menuId,
            int entityDocumentId)
        {
            await using var companyDb = await _companyDbContextFactory.CreateDbContextAsync(companyId);

            var consecutive = await FindConsecutiveHierarchicalAsync(
                companyDb, menuId, entityDocumentId);

            if (consecutive == null)
                return null;

            string currentValue = string.IsNullOrEmpty(consecutive.LAST_VALUE) 
                ? consecutive.INITIAL_VALUE 
                : consecutive.LAST_VALUE;

            string nextValue = IncrementMaskedValue(consecutive.MASK, currentValue);

            return new ConsecutiveInfo
            {
                Code = consecutive.CODE,
                Description = consecutive.DESCRIPTION,
                Mask = consecutive.MASK,
                MenuId = consecutive.ID_MENU,
                LastValue = consecutive.LAST_VALUE,
                NextValue = nextValue
            };
        }
    }

    /// <summary>
    /// Información de un consecutivo (para preview/validación)
    /// </summary>
    public class ConsecutiveInfo
    {
        public string Code { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string Mask { get; set; } = default!;
        public int MenuId { get; set; }
        public string? LastValue { get; set; }
        public string NextValue { get; set; } = default!;
    }
}
