// ================================================================================
// ARCHIVO: CMS.API/Controllers/CustomerController.cs
// PROPÓSITO: API REST para gestión de clientes/emisores
// DESCRIPCIÓN: CRUD de customers con endpoints especializados para emisores,
//              búsquedas por identification, validaciones, etc.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Security.Claims;
using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _service;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(
            ICustomerService service,
            ILogger<CustomerController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int GetCompanyId()
        {
            var claim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;

            _logger.LogWarning("🔍 DEBUG GetCompanyId - Claim value: {Claim}", claim ?? "NULL");

            // Log all claims for debugging
            foreach (var c in User.Claims)
            {
                _logger.LogWarning("🔍 Claim: {Type} = {Value}", c.Type, c.Value);
            }

            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out var id))
            {
                _logger.LogError("❌ CompanyId no encontrado en el token o no es válido");
                throw new UnauthorizedAccessException("CompanyId no encontrado en el token");
            }

            _logger.LogWarning("🔍 CompanyId parseado: {CompanyId}", id);
            return id;
        }

        private string CurrentUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";

        /// <summary>Lista todos los customers.</summary>
        [HttpGet]
        public async Task<ActionResult<List<Customer>>> GetAll([FromQuery] bool includeInactive = false)
        {
            try
            {
                var companyId = GetCompanyId();
                var customers = await _service.GetAllAsync(companyId, includeInactive);
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener customers");
                return StatusCode(500, new { message = "Error al obtener customers", error = ex.Message });
            }
        }

        /// <summary>Lista solo los emisores - DEPRECATED: Usar CustomerBillingCredentialController</summary>
        [HttpGet("issuers")]
        [Obsolete("Use CustomerBillingCredentialController.GetIssuers() instead")]
        public ActionResult<List<Customer>> GetIssuers([FromQuery] bool includeInactive = false)
        {
            return BadRequest(new { message = "Este endpoint está deprecado. Usar /api/CustomerBillingCredential/issuers" });
        }

        /// <summary>Obtiene un customer por ID.</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Customer>> GetById(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var customer = await _service.GetByIdAsync(companyId, id);

                if (customer == null)
                    return NotFound(new { message = $"Customer ID {id} no encontrado" });

                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener customer {Id}", id);
                return StatusCode(500, new { message = "Error al obtener customer", error = ex.Message });
            }
        }

        /// <summary>Obtiene un customer por código.</summary>
        [HttpGet("by-code/{code}")]
        public async Task<ActionResult<Customer>> GetByCode(string code)
        {
            try
            {
                var companyId = GetCompanyId();
                var customer = await _service.GetByCodeAsync(companyId, code);

                if (customer == null)
                    return NotFound(new { message = $"Customer con código '{code}' no encontrado" });

                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener customer por código {Code}", code);
                return StatusCode(500, new { message = "Error al obtener customer", error = ex.Message });
            }
        }

        /// <summary>Obtiene un customer por identification (cédula/NIT).</summary>
        [HttpGet("by-identification/{identification}")]
        public async Task<ActionResult<Customer>> GetByIdentification(string identification)
        {
            try
            {
                var companyId = GetCompanyId();
                var customer = await _service.GetByIdentificationAsync(companyId, identification);

                if (customer == null)
                    return NotFound(new { message = $"Customer con identification '{identification}' no encontrado" });

                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener customer por identification {Identification}", identification);
                return StatusCode(500, new { message = "Error al obtener customer", error = ex.Message });
            }
        }

        /// <summary>Obtiene el customer marcado como company owner - DEPRECATED: Usar CustomerBillingCredentialController</summary>
        [HttpGet("company-owner")]
        [Obsolete("Use CustomerBillingCredentialController.GetCompanyOwner() instead")]
        public ActionResult<Customer> GetCompanyOwner()
        {
            return BadRequest(new { message = "Este endpoint está deprecado. Usar /api/CustomerBillingCredential/company-owner" });
        }

        /// <summary>Crea un nuevo customer.</summary>
        [HttpPost]
        public async Task<ActionResult<Customer>> Create([FromBody] Customer customer)
        {
            try
            {
                var companyId = GetCompanyId();
                var username = CurrentUser();

                var created = await _service.CreateAsync(companyId, customer, username);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validación fallida al crear customer");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear customer");
                return StatusCode(500, new { message = "Error al crear customer", error = ex.Message });
            }
        }

        /// <summary>Actualiza un customer existente.</summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Customer>> Update(int id, [FromBody] Customer customer)
        {
            if (id != customer.Id)
                return BadRequest(new { message = "El ID del URL no coincide con el ID del customer" });

            try
            {
                var companyId = GetCompanyId();
                var username = CurrentUser();

                var updated = await _service.UpdateAsync(companyId, customer, username);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validación fallida al actualizar customer {Id}", id);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar customer {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar customer", error = ex.Message });
            }
        }

        /// <summary>Elimina un customer (soft o hard delete según dependencias).</summary>
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                await _service.DeleteAsync(companyId, id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Customer {Id} no encontrado para eliminar", id);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar customer {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar customer", error = ex.Message });
            }
        }

        /// <summary>Verifica si existe un customer con ese código.</summary>
        [HttpGet("exists/{code}")]
        public async Task<ActionResult<bool>> Exists(string code, [FromQuery] int? excludeId = null)
        {
            try
            {
                var companyId = GetCompanyId();
                var exists = await _service.ExistsAsync(companyId, code, excludeId);
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar existencia de customer {Code}", code);
                return StatusCode(500, new { message = "Error al verificar existencia", error = ex.Message });
            }
        }
    }
}
