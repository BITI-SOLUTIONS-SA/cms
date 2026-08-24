// ================================================================================
// ARCHIVO: CMS.Data/Services/CustomerService.cs
// PROPÓSITO: Servicio de negocio para gestión de clientes/emisores
// DESCRIPCIÓN: Lógica de negocio para CRUD de customers, validaciones,
//              obtención de emisores, búsqueda por identification, etc.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.Operational;
using CMS.Shared.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services
{
    public interface ICustomerService
    {
        Task<List<Customer>> GetAllAsync(int companyId, bool includeInactive = false);
        Task<Customer?> GetByIdAsync(int companyId, int customerId);
        Task<Customer?> GetByCodeAsync(int companyId, string code);
        Task<Customer?> GetByIdentificationAsync(int companyId, string identification);
        Task<Customer> CreateAsync(int companyId, Customer customer, string username);
        Task<Customer> UpdateAsync(int companyId, Customer customer, string username);
        Task DeleteAsync(int companyId, int customerId);
        Task<bool> ExistsAsync(int companyId, string code, int? excludeId = null);
    }

    public class CustomerService : ICustomerService
    {
        private readonly ICompanyDbContextFactory _factory;
        private readonly AppDbContext _centralDb;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(
            ICompanyDbContextFactory factory,
            AppDbContext centralDb,
            ILogger<CustomerService> logger)
        {
            _factory = factory;
            _centralDb = centralDb;
            _logger = logger;
        }

        /// <summary>
        /// Valida el número de identificación del customer contra el tipo del catálogo central.
        /// Lanza InvalidOperationException si es inválido.
        /// </summary>
        private async Task ValidateIdentificationAsync(Customer customer)
        {
            if (!customer.IdElectronicDocumentIdentificationType.HasValue
                || string.IsNullOrWhiteSpace(customer.Identification))
                return;

            var code = await _centralDb.ElectronicDocumentIdentificationTypes
                .AsNoTracking()
                .Where(t => t.Id == customer.IdElectronicDocumentIdentificationType.Value)
                .Select(t => t.Code)
                .FirstOrDefaultAsync();

            if (!IdentificationNumberValidator.TryValidate(code, customer.Identification, out var error))
                throw new InvalidOperationException(error);
        }

        /// <summary>Obtiene todos los customers de una compañía.</summary>
        public async Task<List<Customer>> GetAllAsync(int companyId, bool includeInactive = false)
        {
            await using var db = await _factory.CreateDbContextAsync(companyId);
            var query = db.Customers.AsNoTracking();

            if (!includeInactive)
                query = query.Where(c => c.IsActive);

            return await query
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        /// <summary>Obtiene un customer por ID.</summary>
        public async Task<Customer?> GetByIdAsync(int companyId, int customerId)
        {
            await using var db = await _factory.CreateDbContextAsync(companyId);
            return await db.Customers
                .AsNoTracking()
                .Include(c => c.ParentCustomer)
                .Include(c => c.ChildCustomers)
                .FirstOrDefaultAsync(c => c.Id == customerId);
        }

        /// <summary>Obtiene un customer por código.</summary>
        public async Task<Customer?> GetByCodeAsync(int companyId, string code)
        {
            await using var db = await _factory.CreateDbContextAsync(companyId);
            return await db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code == code);
        }

        /// <summary>Obtiene un customer por identification (cédula/NIT).</summary>
        public async Task<Customer?> GetByIdentificationAsync(int companyId, string identification)
        {
            if (string.IsNullOrWhiteSpace(identification))
                return null;

            await using var db = await _factory.CreateDbContextAsync(companyId);
            return await db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Identification == identification);
        }

        /// <summary>Crea un nuevo customer.</summary>
        public async Task<Customer> CreateAsync(int companyId, Customer customer, string username)
        {
            await using var db = await _factory.CreateDbContextAsync(companyId);

            // Validar que no exista el código
            if (await db.Customers.AnyAsync(c => c.Code == customer.Code))
                throw new InvalidOperationException($"Ya existe un customer con código '{customer.Code}'");

            // Validar que no exista la identification (si no es vacía)
            if (!string.IsNullOrWhiteSpace(customer.Identification))
            {
                if (await db.Customers.AnyAsync(c => c.Identification == customer.Identification))
                    throw new InvalidOperationException($"Ya existe un customer con identification '{customer.Identification}'");
            }

            // Validar formato del número de identificación según su tipo (Hacienda CR).
            await ValidateIdentificationAsync(customer);

            // Auditoría
            customer.CreateDate = DateTime.UtcNow;
            customer.RecordDate = DateTime.UtcNow;
            customer.CreatedBy = username;
            customer.UpdatedBy = username;
            customer.RowPointer = Guid.NewGuid();

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            // Garantizar que TODO cliente tenga al menos una actividad económica (registro
            // predeterminado). Se toma el código por defecto del parámetro global
            // 'default_economic_activity' (id 6). Si no existe, se usa el fallback '0000.1'.
            var defaultActivityCode = await db.GlobalParameters
                .Where(p => p.Code == "default_economic_activity")
                .Select(p => p.ValueString)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(defaultActivityCode))
                defaultActivityCode = "0000.1";
            defaultActivityCode = defaultActivityCode.Trim();

            // Resolver el id del catálogo central (cross-DB) a partir del código.
            var defaultActivityId = await _centralDb.ElectronicDocumentEconomicActivities
                .Where(a => a.Code == defaultActivityCode)
                .Select(a => a.Id)
                .FirstOrDefaultAsync();
            // Fallback: si el código configurado no existe, tomar la primera actividad activa.
            if (defaultActivityId == 0)
            {
                defaultActivityId = await _centralDb.ElectronicDocumentEconomicActivities
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.Id)
                    .Select(a => a.Id)
                    .FirstOrDefaultAsync();
            }

            db.CustomerEconomicActivities.Add(new CustomerEconomicActivity
            {
                IdCustomer = customer.Id,
                IdElectronicDocumentEconomicActivity = defaultActivityId,
                IsDefault = true,
                IsActive = true,
                CreatedBy = username,
                UpdatedBy = username,
                RowPointer = Guid.NewGuid()
            });
            await db.SaveChangesAsync();

            _logger.LogInformation("Customer creado: {Code} - {Name} (ID: {Id})", 
                customer.Code, customer.Name, customer.Id);

            return customer;
        }

        /// <summary>Actualiza un customer existente.</summary>
        public async Task<Customer> UpdateAsync(int companyId, Customer customer, string username)
        {
            await using var db = await _factory.CreateDbContextAsync(companyId);

            var existing = await db.Customers.FindAsync(customer.Id);
            if (existing == null)
                throw new InvalidOperationException($"Customer ID {customer.Id} no encontrado");

            // Validar código único (excluyendo el actual)
            if (await db.Customers.AnyAsync(c => c.Code == customer.Code && c.Id != customer.Id))
                throw new InvalidOperationException($"Ya existe otro customer con código '{customer.Code}'");

            // Validar identification única (excluyendo el actual)
            if (!string.IsNullOrWhiteSpace(customer.Identification))
            {
                if (await db.Customers.AnyAsync(c => c.Identification == customer.Identification && c.Id != customer.Id))
                    throw new InvalidOperationException($"Ya existe otro customer con identification '{customer.Identification}'");
            }

            // Validar formato del número de identificación según su tipo (Hacienda CR).
            await ValidateIdentificationAsync(customer);

            // Copiar propiedades (excepto auditoría de creación)
            existing.Code = customer.Code;
            existing.Name = customer.Name;
            existing.CommercialName = customer.CommercialName;
            existing.IdCustomerType = customer.IdCustomerType;
            existing.IdElectronicDocumentIdentificationType = customer.IdElectronicDocumentIdentificationType;
            existing.Identification = customer.Identification;
            existing.ForeignIdentification = customer.ForeignIdentification;
            existing.CreditLimit = customer.CreditLimit;
            existing.CreditDays = customer.CreditDays;
            existing.PaymentTerms = customer.PaymentTerms;
            existing.DiscountPct = customer.DiscountPct;
            existing.PriceList = customer.PriceList;
            existing.IdAssignedSalesperson = customer.IdAssignedSalesperson;
            existing.IdParentCustomer = customer.IdParentCustomer;
            existing.Province = customer.Province;
            existing.Canton = customer.Canton;
            existing.District = customer.District;
            existing.OtherSigns = customer.OtherSigns;
            existing.GpsLatitude = customer.GpsLatitude;
            existing.GpsLongitude = customer.GpsLongitude;
            existing.PhoneCode = customer.PhoneCode;
            existing.Phone = customer.Phone;
            existing.Mobile = customer.Mobile;
            existing.Email = customer.Email;
            existing.Website = customer.Website;
            existing.ContactName = customer.ContactName;
            existing.ContactPosition = customer.ContactPosition;
            existing.Notes = customer.Notes;
            existing.InternalNotes = customer.InternalNotes;
            existing.IsActive = customer.IsActive;
            existing.BlockedReason = customer.BlockedReason;

            // Auditoría de actualización (el trigger de BD también lo hace)
            existing.RecordDate = DateTime.UtcNow;
            existing.UpdatedBy = username;

            await db.SaveChangesAsync();

            _logger.LogInformation("Customer actualizado: {Code} - {Name} (ID: {Id})", 
                existing.Code, existing.Name, existing.Id);

            return existing;
        }

        /// <summary>Elimina un customer (soft delete o hard delete según reglas).</summary>
        public async Task DeleteAsync(int companyId, int customerId)
        {
            await using var db = await _factory.CreateDbContextAsync(companyId);

            var customer = await db.Customers.FindAsync(customerId);
            if (customer == null)
                throw new InvalidOperationException($"Customer ID {customerId} no encontrado");

            // Verificar si tiene dependencias (documentos, credenciales, etc.)
            var hasCredentials = await db.CustomerBillingCredentials.AnyAsync(bc => bc.IdCustomer == customerId);
            var hasDocuments = await db.ElectronicDocuments.AnyAsync(ed => 
                ed.IdCustomerIssuer == customerId || ed.IdCustomerReceptor == customerId);

            if (hasCredentials || hasDocuments)
            {
                // Soft delete: solo marcar como inactivo
                customer.IsActive = false;
                customer.BlockedReason = "Eliminado (tiene dependencias)";
                await db.SaveChangesAsync();

                _logger.LogWarning("Customer {Code} marcado como inactivo (tiene dependencias)", customer.Code);
            }
            else
            {
                // Hard delete: eliminar físicamente
                db.Customers.Remove(customer);
                await db.SaveChangesAsync();

                _logger.LogInformation("Customer {Code} eliminado físicamente", customer.Code);
            }
        }

        /// <summary>Verifica si existe un customer con ese código.</summary>
        public async Task<bool> ExistsAsync(int companyId, string code, int? excludeId = null)
        {
            await using var db = await _factory.CreateDbContextAsync(companyId);
            var query = db.Customers.Where(c => c.Code == code);

            if (excludeId.HasValue)
                query = query.Where(c => c.Id != excludeId.Value);

            return await query.AnyAsync();
        }
    }
}
