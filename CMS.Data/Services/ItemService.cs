// ================================================================================
// ARCHIVO: CMS.Data/Services/ItemService.cs
// PROPÓSITO: Servicio para gestionar artículos en la BD de cada compañía
// DESCRIPCIÓN: Operaciones CRUD para la tabla item en las BD operacionales
//              - UnitOfMeasure se obtiene de la BD central (admin.unit_of_measure)
//              - Classification se obtiene de la BD de compañía con filtro por classification_group
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-19
// ================================================================================

using CMS.Entities.Operational;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services
{
    /// <summary>
    /// Servicio para gestionar artículos en la base de datos de cada compañía.
    /// </summary>
    public class ItemService : IItemService
    {
        private readonly ICompanyDbContextFactory _dbContextFactory;
        private readonly AppDbContext _centralDbContext;
        private readonly IAuditService _auditService;
        private readonly ILogger<ItemService> _logger;

        public ItemService(
            ICompanyDbContextFactory dbContextFactory,
            AppDbContext centralDbContext,
            IAuditService auditService,
            ILogger<ItemService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _centralDbContext = centralDbContext;
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todos los artículos de una compañía con paginación.
        /// </summary>
        public async Task<(List<Item> Items, int TotalCount)> GetItemsAsync(
            int companyId,
            string? search = null,
            int? classificationGroup = null,
            int? classificationId = null,
            bool? isActive = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int page = 1,
            int pageSize = 20)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);

            var query = dbContext.Items.AsQueryable();

            // Filtros
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(i => 
                    i.Code.ToLower().Contains(search) ||
                    i.Name.ToLower().Contains(search) ||
                    (i.Barcode != null && i.Barcode.ToLower().Contains(search)) ||
                    (i.Description != null && i.Description.ToLower().Contains(search)));
            }

            // Filtro por clasificación
            if (classificationGroup.HasValue && classificationId.HasValue)
            {
                query = classificationGroup.Value switch
                {
                    1 => query.Where(i => i.IdClassification1 == classificationId.Value),
                    2 => query.Where(i => i.IdClassification2 == classificationId.Value),
                    3 => query.Where(i => i.IdClassification3 == classificationId.Value),
                    4 => query.Where(i => i.IdClassification4 == classificationId.Value),
                    5 => query.Where(i => i.IdClassification5 == classificationId.Value),
                    6 => query.Where(i => i.IdClassification6 == classificationId.Value),
                    _ => query
                };
            }

            if (isActive.HasValue)
            {
                query = query.Where(i => i.IsActive == isActive.Value);
            }

            // Filtro por fechas (createdate o record_date)
            // IMPORTANTE: Convertir fechas a UTC para PostgreSQL timestamp with time zone
            // Lógica: mostrar items donde CreateDate O RecordDate esté dentro del rango
            if (dateFrom.HasValue || dateTo.HasValue)
            {
                var fromDate = dateFrom.HasValue 
                    ? DateTime.SpecifyKind(dateFrom.Value.Date, DateTimeKind.Utc) 
                    : DateTime.MinValue;
                var toDate = dateTo.HasValue 
                    ? DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc) // Incluir todo el día
                    : DateTime.MaxValue;

                query = query.Where(i => 
                    // CreateDate está dentro del rango
                    (i.CreateDate >= fromDate && i.CreateDate < toDate) ||
                    // RecordDate está dentro del rango
                    (i.RecordDate >= fromDate && i.RecordDate < toDate));
            }

            // Total count
            var totalCount = await query.CountAsync();

            // Paginación
            var items = await query
                .OrderBy(i => i.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        /// <summary>
        /// Obtiene un artículo por ID.
        /// </summary>
        public async Task<Item?> GetItemByIdAsync(int companyId, int itemId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await dbContext.Items.FindAsync(itemId);
        }

        /// <summary>
        /// Obtiene un artículo por código.
        /// </summary>
        public async Task<Item?> GetItemByCodeAsync(int companyId, string code)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await dbContext.Items
                .FirstOrDefaultAsync(i => i.Code.ToLower() == code.ToLower());
        }

        /// <summary>
        /// Crea un nuevo artículo.
        /// </summary>
        public async Task<Item> CreateItemAsync(int companyId, Item item, string? createdBy = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);

            item.CreateDate = DateTime.UtcNow;
            item.RecordDate = DateTime.UtcNow;
            item.CreatedBy = createdBy ?? "SYSTEM";
            item.UpdatedBy = createdBy ?? "SYSTEM";
            item.RowPointer = Guid.NewGuid();

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Artículo {Code} creado en compañía {CompanyId}", item.Code, companyId);

            return item;
        }

        /// <summary>
        /// Actualiza un artículo existente.
        /// </summary>
        public async Task<Item?> UpdateItemAsync(int companyId, Item item, string? updatedBy = null, int? updatedById = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);

            var existing = await dbContext.Items.FindAsync(item.Id);
            if (existing == null)
            {
                return null;
            }

            // ⭐ AUDITORÍA: Capturar valores originales antes de modificar
            var originalValues = AuditHelper.GetEntityValues(existing);

            // Actualizar propiedades
            existing.Code = item.Code;
            existing.Name = item.Name;
            existing.Description = item.Description;

            // Label Item fields
            existing.LabelItem = item.LabelItem;
            existing.LabelPrice = item.LabelPrice;
            existing.LabelItemBarcode = item.LabelItemBarcode;
            existing.IsLabelItem = item.IsLabelItem;

            existing.Barcode = item.Barcode;
            existing.IdClassification1 = item.IdClassification1;
            existing.IdClassification2 = item.IdClassification2;
            existing.IdClassification3 = item.IdClassification3;
            existing.IdClassification4 = item.IdClassification4;
            existing.IdClassification5 = item.IdClassification5;
            existing.IdClassification6 = item.IdClassification6;
            existing.Brand = item.Brand;
            existing.IdUnitOfMeasure = item.IdUnitOfMeasure;
            existing.CostPrice = item.CostPrice;
            existing.SalePrice = item.SalePrice;
            existing.TaxRate = item.TaxRate;
            existing.MinStock = item.MinStock;
            existing.MaxStock = item.MaxStock;
            existing.CurrentStock = item.CurrentStock;
            existing.ImageUrl = item.ImageUrl;
            existing.IsActive = item.IsActive;
            existing.IsSellable = item.IsSellable;
            existing.IsPurchasable = item.IsPurchasable;
            existing.TrackLots = item.TrackLots;
            existing.TrackSerialNumbers = item.TrackSerialNumbers;

            // Facturación electrónica (Fase B)
            existing.IdCustomer = item.IdCustomer;
            existing.TaxRateCode = item.TaxRateCode;
            existing.CabysCode = item.CabysCode;

            // Auditoría - El trigger de la BD actualiza record_date y updated_by
            existing.RecordDate = DateTime.UtcNow;
            existing.UpdatedBy = updatedBy ?? "SYSTEM";

            await dbContext.SaveChangesAsync();

            // ⭐ AUDITORÍA: Registrar los cambios
            var newValues = AuditHelper.GetEntityValues(existing);
            var changes = new Dictionary<string, (string? OldValue, string? NewValue)>();

            foreach (var kvp in originalValues)
            {
                if (newValues.TryGetValue(kvp.Key, out var newValue) && kvp.Value != newValue)
                {
                    changes[kvp.Key] = (kvp.Value, newValue);
                }
            }

            if (changes.Any() && updatedById.HasValue)
            {
                var companyInfo = await _dbContextFactory.GetCompanyInfoAsync(companyId);
                await _auditService.RecordUpdateAsync(
                    databaseName: companyInfo.Schema,  // La BD operacional tiene el mismo nombre que el schema
                    schemaName: companyInfo.Schema,
                    tableName: "item",
                    primaryKeyColumn: "id_item",
                    primaryKeyValue: existing.Id.ToString(),
                    changes: changes,
                    userName: updatedBy ?? "SYSTEM",
                    userId: updatedById.Value);
            }

            _logger.LogInformation("Artículo {Code} actualizado en compañía {CompanyId}", item.Code, companyId);

            return existing;
        }

        /// <summary>
        /// Elimina (desactiva) un artículo.
        /// </summary>
        public async Task<bool> DeleteItemAsync(int companyId, int itemId, string? deletedBy = null, int? deletedById = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);

            var item = await dbContext.Items.FindAsync(itemId);
            if (item == null)
            {
                return false;
            }

            // ⭐ AUDITORÍA: Capturar valores antes de eliminar
            var entityValues = AuditHelper.GetEntityValues(item);

            // Soft delete
            item.IsActive = false;
            item.RecordDate = DateTime.UtcNow;
            item.UpdatedBy = deletedBy ?? "SYSTEM";

            await dbContext.SaveChangesAsync();

            // ⭐ AUDITORÍA: Registrar el DELETE (soft delete cuenta como DELETE)
            if (deletedById.HasValue)
            {
                var companyInfo = await _dbContextFactory.GetCompanyInfoAsync(companyId);
                await _auditService.RecordDeleteAsync(
                    databaseName: companyInfo.Schema,
                    schemaName: companyInfo.Schema,
                    tableName: "item",
                    primaryKeyColumn: "id_item",
                    primaryKeyValue: item.Id.ToString(),
                    entityValues: entityValues,
                    userName: deletedBy ?? "SYSTEM",
                    userId: deletedById.Value);
            }

            _logger.LogInformation("Artículo {Code} desactivado en compañía {CompanyId}", item.Code, companyId);

            return true;
        }

        /// <summary>
        /// Obtiene artículos de etiqueta (is_label_item = true) con paginación.
        /// </summary>
        public async Task<(List<Item> Items, int TotalCount)> GetLabelItemsAsync(
            int companyId,
            string? search = null,
            int page = 1,
            int pageSize = 20,
            string? orderBy = "sale_price")
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);

            var query = dbContext.Items.Where(i => i.IsLabelItem && i.IsActive);

            // Filtro de búsqueda
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower().Trim();

                // Intentar parsear como número para búsqueda por precio
                decimal? searchPrice = null;
                if (decimal.TryParse(search, out var parsedPrice))
                {
                    searchPrice = parsedPrice;
                }

                query = query.Where(i =>
                    i.Code.ToLower().Contains(search) ||
                    i.Name.ToLower().Contains(search) ||
                    (i.LabelItem != null && i.LabelItem.ToLower().Contains(search)) ||
                    (i.Barcode != null && i.Barcode.ToLower().Contains(search)) ||
                    (searchPrice.HasValue && i.LabelPrice == searchPrice.Value) ||
                    (searchPrice.HasValue && i.SalePrice == searchPrice.Value));
            }

            // Total count
            var totalCount = await query.CountAsync();

            // Ordenamiento
            IOrderedQueryable<Item> orderedQuery = orderBy?.ToLower() switch
            {
                "name" => query.OrderBy(i => i.Name),
                "code" => query.OrderBy(i => i.Code),
                "label_price" => query.OrderBy(i => i.LabelPrice),
                "label_item" => query.OrderBy(i => i.LabelItem ?? i.Name),
                "sale_price" or _ => query.OrderBy(i => i.SalePrice)
            };

            // Paginación
            var items = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        /// <summary>
        /// Actualiza solo los campos de etiqueta de un artículo.
        /// </summary>
        public async Task<Item?> UpdateLabelInfoAsync(
            int companyId,
            int itemId,
            string? labelItem,
            decimal labelPrice,
            string? labelItemBarcode,
            bool printLabelName,
            bool printLabelPrice,
            bool printLabelBarcode,
            decimal labelWidthCm,
            decimal labelHeightCm,
            string labelOrientation,
            bool printLabelBorder,
            string labelBorderColor,
            string labelNameColor,
            string labelPriceColor,
            string labelBarcodeColor,
            decimal labelFontSize,
            string labelFontFamily,
            int labelPriceDecimals,
            string labelThousandSeparator,
            string labelCurrencySymbol,
            bool printCurrencySymbol,
            string? updatedBy = null,
            int? updatedById = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);

            var item = await dbContext.Items.FindAsync(itemId);
            if (item == null)
            {
                return null;
            }

            // ⭐ AUDITORÍA: Capturar valores originales antes de modificar
            var originalValues = AuditHelper.GetEntityValues(item);

            // Actualizar campos de etiqueta
            item.LabelItem = labelItem;
            item.LabelPrice = labelPrice;
            item.LabelItemBarcode = labelItemBarcode;
            item.PrintLabelName = printLabelName;
            item.PrintLabelPrice = printLabelPrice;
            item.PrintLabelBarcode = printLabelBarcode;

            // Actualizar campos de tamaño y formato
            item.LabelWidthCm = labelWidthCm;
            item.LabelHeightCm = labelHeightCm;
            item.LabelOrientation = labelOrientation;
            item.PrintLabelBorder = printLabelBorder;
            item.LabelBorderColor = labelBorderColor;
            item.LabelNameColor = labelNameColor;
            item.LabelPriceColor = labelPriceColor;
            item.LabelBarcodeColor = labelBarcodeColor;

            // Actualizar campos de fuente y formato de precio
            item.LabelFontSize = labelFontSize;
            item.LabelFontFamily = labelFontFamily;
            item.LabelPriceDecimals = labelPriceDecimals;
            item.LabelThousandSeparator = labelThousandSeparator;
            item.LabelCurrencySymbol = labelCurrencySymbol;
            item.PrintCurrencySymbol = printCurrencySymbol;

            item.RecordDate = DateTime.UtcNow;
            item.UpdatedBy = updatedBy ?? "SYSTEM";

            await dbContext.SaveChangesAsync();

            // ⭐ AUDITORÍA: Registrar los cambios
            var newValues = AuditHelper.GetEntityValues(item);
            var changes = new Dictionary<string, (string? OldValue, string? NewValue)>();

            foreach (var kvp in originalValues)
            {
                if (newValues.TryGetValue(kvp.Key, out var newValue) && kvp.Value != newValue)
                {
                    changes[kvp.Key] = (kvp.Value, newValue);
                }
            }

            if (changes.Any() && updatedById.HasValue)
            {
                var companyInfo = await _dbContextFactory.GetCompanyInfoAsync(companyId);
                await _auditService.RecordUpdateAsync(
                    databaseName: companyInfo.Schema,
                    schemaName: companyInfo.Schema,
                    tableName: "item",
                    primaryKeyColumn: "id_item",
                    primaryKeyValue: item.Id.ToString(),
                    changes: changes,
                    userName: updatedBy ?? "SYSTEM",
                    userId: updatedById.Value);
            }

            _logger.LogInformation("Etiqueta del artículo {Code} actualizada en compañía {CompanyId}", item.Code, companyId);

            return item;
        }

        /// <summary>
        /// Registra una impresión de etiqueta en el historial.
        /// </summary>
        public async Task<LabelPrintHistory> RecordLabelPrintAsync(
            int companyId,
            int itemId,
            string itemCode,
            string itemName,
            string? labelItem,
            decimal labelPrice,
            string? labelItemBarcode,
            bool printLabelName,
            bool printLabelPrice,
            bool printLabelBarcode,
            bool printLabelBorder,
            bool printCurrencySymbol,
            decimal labelWidthCm,
            decimal labelHeightCm,
            string labelOrientation,
            string labelBorderColor,
            string labelNameColor,
            string labelPriceColor,
            string labelBarcodeColor,
            decimal labelFontSize,
            string labelFontFamily,
            int labelPriceDecimals,
            string labelThousandSeparator,
            string labelCurrencySymbol,
            string? formattedPrice,
            int quantityPrinted,
            string? containerNumber,
            string? printedBy,
            string? printerName = null,
            string? printNotes = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);

            var printHistory = new LabelPrintHistory
            {
                IdItem = itemId,
                ItemCode = itemCode,
                ItemName = itemName,
                LabelItem = labelItem,
                LabelPrice = labelPrice,
                LabelItemBarcode = labelItemBarcode,
                PrintLabelName = printLabelName,
                PrintLabelPrice = printLabelPrice,
                PrintLabelBarcode = printLabelBarcode,
                PrintLabelBorder = printLabelBorder,
                PrintCurrencySymbol = printCurrencySymbol,
                LabelWidthCm = labelWidthCm,
                LabelHeightCm = labelHeightCm,
                LabelOrientation = labelOrientation,
                LabelBorderColor = labelBorderColor,
                LabelNameColor = labelNameColor,
                LabelPriceColor = labelPriceColor,
                LabelBarcodeColor = labelBarcodeColor,
                LabelFontSize = labelFontSize,
                LabelFontFamily = labelFontFamily,
                LabelPriceDecimals = labelPriceDecimals,
                LabelThousandSeparator = labelThousandSeparator,
                LabelCurrencySymbol = labelCurrencySymbol,
                FormattedPrice = formattedPrice,
                QuantityPrinted = quantityPrinted,
                ContainerNumber = containerNumber,
                PrintDate = DateTime.UtcNow,
                PrintedBy = printedBy,
                PrinterName = printerName,
                PrintNotes = printNotes,
                CreateDate = DateTime.UtcNow,
                RecordDate = DateTime.UtcNow,
                CreatedBy = printedBy ?? "SYSTEM"
            };

            dbContext.LabelPrintHistory.Add(printHistory);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Impresión de etiqueta registrada: Artículo {ItemCode}, Cantidad {Quantity}, Contenedor {Container}, Usuario {User}", 
                itemCode, quantityPrinted, containerNumber ?? "N/A", printedBy);

            return printHistory;
        }

        /// <summary>
        /// Obtiene el historial de impresiones de un artículo.
        /// </summary>
        public async Task<List<LabelPrintHistory>> GetLabelPrintHistoryAsync(
            int companyId,
            int? itemId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? printedBy = null,
            string? containerNumber = null,
            int page = 1,
            int pageSize = 50)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);

            var query = dbContext.LabelPrintHistory.AsQueryable();

            if (itemId.HasValue)
            {
                query = query.Where(h => h.IdItem == itemId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(h => h.PrintDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var toDateEndOfDay = toDate.Value.Date.AddDays(1);
                query = query.Where(h => h.PrintDate < toDateEndOfDay);
            }

            if (!string.IsNullOrEmpty(printedBy))
            {
                query = query.Where(h => h.PrintedBy != null && h.PrintedBy.ToLower().Contains(printedBy.ToLower()));
            }

            if (!string.IsNullOrEmpty(containerNumber))
            {
                query = query.Where(h => h.ContainerNumber != null && h.ContainerNumber.ToLower().Contains(containerNumber.ToLower()));
            }

            return await query
                .OrderByDescending(h => h.PrintDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene todas las unidades de medida desde la BD CENTRAL (admin.unit_of_measure).
        /// </summary>
        public async Task<List<UnitOfMeasureDto>> GetUnitsOfMeasureAsync(int companyId)
        {
            // UnitOfMeasure está en la BD central (admin.unit_of_measure)
            return await _centralDbContext.UnitsOfMeasure
                .Where(u => u.IsActive)
                .OrderBy(u => u.DisplayOrder)
                .ThenBy(u => u.Name)
                .Select(u => new UnitOfMeasureDto 
                { 
                    Id = u.Id, 
                    Code = u.Code, 
                    Name = u.Name, 
                    Symbol = u.Symbol, 
                    AllowsDecimals = u.AllowsDecimals, 
                    IsDefault = u.IsDefault 
                })
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene las clasificaciones por nivel (1-6) desde la BD de la compañía.
        /// Ahora usa una sola tabla 'classification' con el campo classification_group.
        /// </summary>
        public async Task<List<ClassificationDto>> GetClassificationsAsync(int companyId, int level)
        {
            if (level < 1 || level > 6)
            {
                return new List<ClassificationDto>();
            }

            using var dbContext = await _dbContextFactory.CreateDbContextAsync(companyId);

            return await dbContext.Classifications
                .Where(c => c.ClassificationGroup == level && c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .Select(c => new ClassificationDto 
                { 
                    Id = c.Id, 
                    Code = c.Code, 
                    Name = c.Name, 
                    Description = c.Description 
                })
                .ToListAsync();
        }
    }

    /// <summary>
    /// DTO para unidad de medida (usado en el servicio)
    /// </summary>
    public class UnitOfMeasureDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public bool AllowsDecimals { get; set; }
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// DTO para clasificación (usado en el servicio)
    /// </summary>
    public class ClassificationDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Interface para el servicio de Items
    /// </summary>
    public interface IItemService
    {
        Task<(List<Item> Items, int TotalCount)> GetItemsAsync(
            int companyId,
            string? search = null,
            int? classificationGroup = null,
            int? classificationId = null,
            bool? isActive = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int page = 1, 
            int pageSize = 20);

        Task<(List<Item> Items, int TotalCount)> GetLabelItemsAsync(
            int companyId,
            string? search = null,
            int page = 1,
            int pageSize = 20,
            string? orderBy = "sale_price");

        Task<Item?> GetItemByIdAsync(int companyId, int itemId);
        Task<Item?> GetItemByCodeAsync(int companyId, string code);
        Task<Item> CreateItemAsync(int companyId, Item item, string? createdBy = null);
        Task<Item?> UpdateItemAsync(int companyId, Item item, string? updatedBy = null, int? updatedById = null);
        Task<Item?> UpdateLabelInfoAsync(int companyId, int itemId, string? labelItem, decimal labelPrice, string? labelItemBarcode, bool printLabelName, bool printLabelPrice, bool printLabelBarcode, decimal labelWidthCm, decimal labelHeightCm, string labelOrientation, bool printLabelBorder, string labelBorderColor, string labelNameColor, string labelPriceColor, string labelBarcodeColor, decimal labelFontSize, string labelFontFamily, int labelPriceDecimals, string labelThousandSeparator, string labelCurrencySymbol, bool printCurrencySymbol, string? updatedBy = null, int? updatedById = null);
        Task<bool> DeleteItemAsync(int companyId, int itemId, string? deletedBy = null, int? deletedById = null);

        /// <summary>
        /// Obtiene las unidades de medida desde la BD CENTRAL (admin.unit_of_measure)
        /// </summary>
        Task<List<UnitOfMeasureDto>> GetUnitsOfMeasureAsync(int companyId);

        /// <summary>
        /// Obtiene las clasificaciones por nivel (1-6) desde la BD de la compañía
        /// </summary>
        Task<List<ClassificationDto>> GetClassificationsAsync(int companyId, int level);

        // Historial de impresiones
        Task<LabelPrintHistory> RecordLabelPrintAsync(
            int companyId, int itemId, string itemCode, string itemName,
            string? labelItem, decimal labelPrice, string? labelItemBarcode,
            bool printLabelName, bool printLabelPrice, bool printLabelBarcode,
            bool printLabelBorder, bool printCurrencySymbol,
            decimal labelWidthCm, decimal labelHeightCm, string labelOrientation,
            string labelBorderColor, string labelNameColor, string labelPriceColor, string labelBarcodeColor,
            decimal labelFontSize, string labelFontFamily, int labelPriceDecimals,
            string labelThousandSeparator, string labelCurrencySymbol,
            string? formattedPrice, int quantityPrinted, string? containerNumber, string? printedBy,
            string? printerName = null, string? printNotes = null);

        Task<List<LabelPrintHistory>> GetLabelPrintHistoryAsync(
            int companyId, int? itemId = null, DateTime? fromDate = null, DateTime? toDate = null,
            string? printedBy = null, string? containerNumber = null, int page = 1, int pageSize = 50);
    }
}
