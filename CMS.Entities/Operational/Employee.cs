// ================================================================================
// ARCHIVO: CMS.Entities/Operational/Employee.cs
// PROPÓSITO: Entidad Empleado para el módulo Human Resources (por compañía)
// DESCRIPCIÓN: Almacena datos completos del empleado. Reside en la BD de la
//              compañía ({schema}.employee). Vinculado opcionalmente a un
//              usuario activo del sistema (FK lógica cross-DB a cms.admin.user).
//              Referencia el catálogo central admin.department via FK lógica.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-07-04
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    [Table("employee")]
    public class Employee
    {
        [Key]
        [Column("id_employee")]
        public int Id { get; set; }

        // ── Vínculo con usuario del sistema ──────────────────────
        /// <summary>ID del usuario del sistema (cms.admin.user) — FK lógica cross-DB</summary>
        [Column("id_system_user")]
        public int? IdSystemUser { get; set; }

        // ── Departamento (FK lógica a {schema}.department) ──
        [Column("id_department")]
        public int? IdDepartment { get; set; }

        // ── Puesto (FK lógica a {schema}.job_position) ──
        [Column("id_job_position")]
        public int? IdJobPosition { get; set; }

        // ── Ubicación/Dirección (FK lógica a {schema}.location) ──
        [Column("id_location")]
        public int? IdLocation { get; set; }

        // ── Identificación personal ───────────────────────────────

        [Required][MaxLength(30)][Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required][MaxLength(100)][Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)][Column("second_name")]
        public string? SecondName { get; set; }

        [Required][MaxLength(100)][Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Required][MaxLength(100)][Column("second_last_name")]
        public string SecondLastName { get; set; } = string.Empty;

        /// <summary>Nombre completo desnormalizado para búsquedas rápidas</summary>
        [MaxLength(300)][Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        /// <summary>Número de cédula / pasaporte / identificación</summary>
        [Required][MaxLength(30)][Column("id_number")]
        public string IdNumber { get; set; } = string.Empty;

        /// <summary>FK lógica cross-DB a admin.electronic_document_identification_type (BD central cms)</summary>
        [Column("id_electronic_document_identification_type")]
        public int? IdElectronicDocumentIdentificationType { get; set; }

        [Required]
        [Column("birth_date")]
        public DateOnly BirthDate { get; set; }

        /// <summary>Género (código de admin.gender)</summary>
        [Required][MaxLength(10)][Column("gender")]
        public string Gender { get; set; } = string.Empty;

        // ── Contacto ──────────────────────────────────────────────

        [MaxLength(30)][Column("phone")]
        public string? Phone { get; set; }

        [Required][MaxLength(30)][Column("mobile")]
        public string Mobile { get; set; } = string.Empty;

        [Required][MaxLength(150)][Column("email")]
        public string Email { get; set; } = string.Empty;

        // ── Datos laborales ───────────────────────────────────────

        [MaxLength(50)][Column("employment_type")]
        public string EmploymentType { get; set; } = "FULL_TIME";  // FULL_TIME, PART_TIME, CONTRACT, INTERN, OTHER

        [Required]
        [Column("hire_date")]
        public DateOnly HireDate { get; set; }

        [Column("termination_date")]
        public DateOnly? TerminationDate { get; set; }

        [MaxLength(500)][Column("termination_reason")]
        public string? TerminationReason { get; set; }

        // ── Compensación ──────────────────────────────────────────

        [Required]
        [Column("base_salary")]
        public decimal BaseSalary { get; set; }

        [Column("id_currency")]
        public int? IdCurrency { get; set; }

        [MaxLength(30)][Column("payment_frequency")]
        public string PaymentFrequency { get; set; } = "MONTHLY";  // MONTHLY, BIWEEKLY, WEEKLY

        // ── Emergencia ────────────────────────────────────────────

        [MaxLength(200)][Column("emergency_contact_name")]
        public string? EmergencyContactName { get; set; }

        [MaxLength(30)][Column("emergency_contact_phone")]
        public string? EmergencyContactPhone { get; set; }

        [MaxLength(100)][Column("emergency_contact_relation")]
        public string? EmergencyContactRelation { get; set; }

        // ── Estado ────────────────────────────────────────────────

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(2000)][Column("notes")]
        public string? Notes { get; set; }

        // ── Auditoría ─────────────────────────────────────────────

        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required][MaxLength(150)][Column("created_by")]
        public string CreatedBy { get; set; } = "SYSTEM";

        [Required][MaxLength(150)][Column("updated_by")]
        public string UpdatedBy { get; set; } = "SYSTEM";

        [Column("rowpointer")]
        public Guid Rowpointer { get; set; } = Guid.NewGuid();

        // ── Navegaciones [NotMapped] — cross-DB ───────────────────
        [NotMapped]
        public string? DepartmentName { get; set; }

        [NotMapped]
        public string? DepartmentIcon { get; set; }

        [NotMapped]
        public string? DepartmentColor { get; set; }

        [NotMapped]
        public string? JobPositionName { get; set; }

        [NotMapped]
        public string? LocationDisplay { get; set; }

        [NotMapped]
        public string? TypeIdDescription { get; set; }

        [NotMapped]
        public string? GenderDescription { get; set; }

        [NotMapped]
        public string? CurrencyCode { get; set; }

        [NotMapped]
        public string? CurrencySymbol { get; set; }
    }
}
