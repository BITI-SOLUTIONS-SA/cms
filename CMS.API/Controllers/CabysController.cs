// ================================================================================
// ARCHIVO: CMS.API/Controllers/CabysController.cs
// PROPÓSITO: API REST para consultar el catálogo central CAByS (13 díg.)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Application.DTOs.EInvoice;
using CMS.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CabysController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<CabysController> _logger;

        public CabysController(AppDbContext db, ILogger<CabysController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>Busca códigos CAByS por código o descripción (máx. 50 resultados).</summary>
        [HttpGet("search")]
        public async Task<ActionResult<List<CabysDto>>> Search([FromQuery] string q, [FromQuery] int limit = 50)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 3)
                return Ok(new List<CabysDto>());

            limit = Math.Clamp(limit, 1, 100);
            var term = q.Trim();

            var results = await _db.CabysCodes
                .AsNoTracking()
                .Where(c => c.IS_ACTIVE && (c.CODE.StartsWith(term) || EF.Functions.ILike(c.DESCRIPTION, $"%{term}%")))
                .OrderBy(c => c.CODE)
                .Take(limit)
                .Select(c => new CabysDto
                {
                    Code = c.CODE,
                    Description = c.DESCRIPTION,
                    TaxRate = c.TAX_RATE,
                    TaxRateCode = c.TAX_RATE_CODE,
                    Category = c.CATEGORY
                })
                .ToListAsync();

            return Ok(results);
        }

        /// <summary>Obtiene un código CAByS exacto (13 díg.).</summary>
        [HttpGet("{code}")]
        public async Task<ActionResult<CabysDto>> GetByCode(string code)
        {
            var c = await _db.CabysCodes.AsNoTracking().FirstOrDefaultAsync(x => x.CODE == code);
            if (c == null) return NotFound(new { message = "Código CAByS no encontrado" });
            return Ok(new CabysDto
            {
                Code = c.CODE,
                Description = c.DESCRIPTION,
                TaxRate = c.TAX_RATE,
                TaxRateCode = c.TAX_RATE_CODE,
                Category = c.CATEGORY
            });
        }
    }
}
