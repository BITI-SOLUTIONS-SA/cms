// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/EInvoiceRetryWorker.cs
// PROPÓSITO: BackgroundService que procesa la cola de reintentos de Hacienda
// DESCRIPCIÓN: Recorre las compañías activas, revisa su cola einvoice_retry_queue y
//              procesa los documentos cuyo next_attempt_at ya venció (backoff
//              exponencial). Garantiza que el sistema nunca pierda un comprobante
//              cuando Hacienda estuvo caído. Conserva la FechaEmision original.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.EInvoice;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services.EInvoice
{
    /// <summary>
    /// Worker de resiliencia para Facturación Electrónica CR v4.4.
    /// Procesa la cola de reintentos con backoff exponencial.
    /// </summary>
    public class EInvoiceRetryWorker : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EInvoiceRetryWorker> _logger;

        // Compañías sin BD operacional (o inaccesibles): se omiten para no ensuciar logs.
        private static readonly HashSet<int> _skipCompanies = new();

        public EInvoiceRetryWorker(IServiceScopeFactory scopeFactory, ILogger<EInvoiceRetryWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔄 EInvoiceRetryWorker iniciado.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAllCompaniesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el ciclo del EInvoiceRetryWorker.");
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        private async Task ProcessAllCompaniesAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var centralDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var factory = scope.ServiceProvider.GetRequiredService<ICompanyDbContextFactory>();
            var docService = scope.ServiceProvider.GetRequiredService<IElectronicDocumentService>();
            var authService = scope.ServiceProvider.GetRequiredService<IHaciendaAuthService>();
            var apiClient = scope.ServiceProvider.GetRequiredService<IHaciendaApiClient>();

            // Compañías con connection string operacional configurada.
            var companies = await centralDb.Companies
                .AsNoTracking()
                .Where(c => c.IsTenant &&
                            (c.CONNECTION_STRING_DEVELOPMENT != null || c.CONNECTION_STRING_PRODUCTION != null))
                .Select(c => c.ID)
                .ToListAsync(ct);

            foreach (var companyId in companies)
            {
                if (_skipCompanies.Contains(companyId)) continue;
                try
                {
                    await ProcessCompanyQueueAsync(companyId, factory, docService, authService, apiClient, ct);
                }
                catch (Npgsql.PostgresException pgx) when (pgx.SqlState is "3D000" or "42P01")
                {
                    // BD operacional inexistente (3D000) o sin tablas fiscales (42P01):
                    // marcar para no reintentar y no ensuciar logs.
                    _skipCompanies.Add(companyId);
                    _logger.LogInformation(
                        "Compañía {CompanyId} sin módulo de e-invoice (BD/tablas ausentes); se omite.", companyId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo procesar la cola de la compañía {CompanyId}.", companyId);
                }
            }
        }

        private async Task ProcessCompanyQueueAsync(
            int companyId,
            ICompanyDbContextFactory factory,
            IElectronicDocumentService docService,
            IHaciendaAuthService authService,
            IHaciendaApiClient apiClient,
            CancellationToken ct)
        {
            await using var db = await factory.CreateDbContextAsync(companyId);

            var now = DateTime.UtcNow;

            // 1) Reconciliación: rescatar documentos "huérfanos" que quedaron en un estado
            //    no terminal SIN un ítem activo en la cola (reinicio del servidor, fallo al
            //    encolar, o documentos previos a esta lógica). Sin esto nunca se reprocesan.
            await ReconcileOrphanDocumentsAsync(db, now, ct);

            // 2) Procesar la cola de reintentos cuyo next_attempt_at ya venció.
            var pending = await db.EInvoiceRetryQueue
                .Where(q => !q.IsDone && q.NextAttemptAt <= now)
                .OrderBy(q => q.NextAttemptAt)
                .Take(20)
                .ToListAsync(ct);

            if (pending.Count == 0) return;

            _logger.LogInformation("Procesando {Count} reintentos para compañía {CompanyId}.", pending.Count, companyId);

            foreach (var item in pending)
            {
                var document = await db.ElectronicDocuments.FirstOrDefaultAsync(d => d.Id == item.IdElectronicDocument, ct);
                if (document == null) { item.IsDone = true; continue; }

                // Obtener credential del emisor directamente
                var credential = await db.CustomerBillingCredentials
                    .FirstOrDefaultAsync(c => c.IdCustomer == document.IdCustomerIssuer && c.IsIssuer && c.IsActive, ct);
                if (credential == null) { item.IsDone = true; continue; }

                try
                {
                    if (item.Operation == EInvoiceRetryOperation.PollStatus)
                    {
                        var token = await authService.GetAccessTokenAsync(credential, ct);
                        var status = await apiClient.GetStatusAsync(credential, token, document.Clave!, ct);
                        if (status.Unauthorized)
                        {
                            token = await authService.ForceRefreshAsync(credential, ct);
                            status = await apiClient.GetStatusAsync(credential, token, document.Clave!, ct);
                        }

                        if (status.Status is "aceptado")
                        {
                            document.Status = EInvoiceStatus.Aceptado;
                            document.HaciendaStatus = status.Status;
                            document.HaciendaDetail = status.HaciendaDetail;
                            document.XmlResponse = status.HaciendaMessageXml ?? status.ResponseBody;
                            document.AcceptedAt = DateTime.UtcNow;
                            item.IsDone = true;
                        }
                        else if (status.Status is "rechazado")
                        {
                            document.Status = EInvoiceStatus.Rechazado;
                            document.HaciendaStatus = status.Status;
                            document.HaciendaDetail = status.HaciendaDetail;
                            document.XmlResponse = status.HaciendaMessageXml ?? status.ResponseBody;
                            item.IsDone = true;
                        }
                        else
                        {
                            ScheduleNextAttempt(item, status.RetryAfterSeconds);
                        }
                    }
                    else // send
                    {
                        await docService.ProcessPendingAsync(companyId, document.Id, ct);
                        // ProcessPendingAsync ya reencola PollStatus si corresponde.
                        item.IsDone = true;
                    }
                }
                catch (Exception ex)
                {
                    item.LastError = ex.Message;
                    ScheduleNextAttempt(item, null);
                    _logger.LogWarning(ex, "Reintento fallido documento {DocId}.", document.Id);
                }

                item.RecordDate = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Barredora de reconciliación. Busca documentos en un estado no terminal
        /// (Pendiente, Contingencia, Procesando, Enviado) que NO tengan un ítem activo
        /// en la cola de reintentos y los reencola para que el polling en segundo plano
        /// los rescate. Esto cubre reinicios del servidor, fallos al encolar y documentos
        /// creados antes de existir la cola. Solo se consideran documentos con más de
        /// 1 minuto de antigüedad para no competir con la emisión sincrónica en curso.
        /// </summary>
        private async Task ReconcileOrphanDocumentsAsync(CompanyDbContext db, DateTime now, CancellationToken ct)
        {
            var cutoff = now.AddMinutes(-1);

            var nonTerminal = new[]
            {
                EInvoiceStatus.Pendiente,
                EInvoiceStatus.Contingencia,
                EInvoiceStatus.Procesando,
                EInvoiceStatus.Enviado
            };

            // IDs de documentos que YA tienen un ítem activo en la cola.
            var queuedDocIds = await db.EInvoiceRetryQueue
                .Where(q => !q.IsDone)
                .Select(q => q.IdElectronicDocument)
                .Distinct()
                .ToListAsync(ct);

            var orphans = await db.ElectronicDocuments
                .Where(d => nonTerminal.Contains(d.Status)
                            && d.RecordDate <= cutoff
                            && !queuedDocIds.Contains(d.Id))
                .OrderBy(d => d.Id)
                .Take(50)
                .ToListAsync(ct);

            if (orphans.Count == 0) return;

            foreach (var doc in orphans)
            {
                // Si ya tiene clave y fue enviado a Hacienda -> consultar estado.
                // Si aún no ha sido enviado (Pendiente/Contingencia sin clave o sin envío) -> reintentar envío.
                var operation = (doc.Status == EInvoiceStatus.Procesando
                                 || doc.Status == EInvoiceStatus.Enviado
                                 || (doc.SubmittedAt != null && !string.IsNullOrEmpty(doc.Clave)))
                    ? EInvoiceRetryOperation.PollStatus
                    : EInvoiceRetryOperation.Send;

                db.EInvoiceRetryQueue.Add(new Entities.Operational.EInvoiceRetryQueue
                {
                    IdElectronicDocument = doc.Id,
                    Operation = operation,
                    AttemptCount = 0,
                    NextAttemptAt = now,
                    LastError = "Reencolado por reconciliación (documento huérfano).",
                    IsDone = false,
                    CreateDate = now,
                    RecordDate = now,
                    CreatedBy = "EInvoiceRetryWorker",
                    UpdatedBy = "EInvoiceRetryWorker"
                });

                _logger.LogInformation(
                    "🩹 Reconciliación: documento {DocId} (estado {Status}) reencolado como {Operation}.",
                    doc.Id, doc.Status, operation);
            }

            await db.SaveChangesAsync(ct);
        }

        private static void ScheduleNextAttempt(Entities.Operational.EInvoiceRetryQueue item, int? retryAfterSeconds)
        {
            const double baseSeconds = 30;
            const double capSeconds = 3600;
            item.AttemptCount++;
            var backoff = retryAfterSeconds ??
                (int)Math.Min(capSeconds, baseSeconds * Math.Pow(2, item.AttemptCount));
            item.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoff);
        }
    }
}
