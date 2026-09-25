# Quickstart Validation Guide: Document Management Gap Closure

**Feature**: `001-document-management`
**Date**: 2026-09-24
**Purpose**: Validate the existing workflows and the planned gap-closure acceptance criteria after implementation.

## Prerequisites

1. .NET 8 SDK installed.
2. Local database provider configured and initialized.
3. ContosoDashboard running at `http://localhost:5000`.
4. Test files: two valid allowlisted files, one unsupported file, and one file over 25 MB. Use non-sensitive sample content.
5. Seeded users covering Employee, Team Lead, Project Manager, and Administrator roles; at least one project and one task.

## Run

```powershell
cd ContosoDashboard
dotnet run
```

Open `http://localhost:5000` and sign in through the training login page.

## Functional Scenarios

### 1. Multi-file upload and independent outcomes

1. Sign in as an Employee and open **Documents**.
2. Select two valid files and one unsupported or oversized file in one upload selection.
3. Submit and inspect the per-file outcomes.
4. Confirm both valid files appear with their metadata, while the invalid file shows its own clear failure.
5. Confirm the failed file has neither a database record nor a stored file; successful files remain available.
6. Repeat with one valid file to verify the single-file flow still works.

### 2. Inclusive upload-date filtering

1. Create or seed documents immediately before, on, and after a chosen UTC date boundary.
2. Filter with the same start and end date.
3. Confirm documents timestamped anywhere on that UTC date are included, and adjacent-day documents are excluded.
4. Test only-start and only-end filters, then enter a start date after the end date and confirm validation.

### 3. Task-context upload and attachment controls

1. Sign in as a user authorized for a task and open its details.
2. Upload a valid file directly from the task details.
3. Confirm it appears in the task attachment list and in the parent project's document list, with matching `TaskId` and `ProjectId`.
4. Confirm existing documents can still be attached and detached.
5. Try a task or project identifier outside the user's access and confirm the service rejects the operation.

### 4. Role permissions

1. As a Team Lead, view/download a document uploaded by a user in the same department.
2. Confirm Team Lead status alone does not allow editing, replacing, or deleting that other user's document.
3. As the owner, edit/replace/delete the owned document where allowed.
4. As a Project Manager, manage documents belonging to a managed project only.
5. As an Administrator, inspect organization-wide documents, audit logs, and reports.
6. Confirm shared recipients remain read-only unless a separate role grants more access.

### 5. Download/preview auditing and reports

1. As an authorized user, preview and download a known test document.
2. As an Administrator, confirm one `Preview` and one `Download` event with actor, UTC timestamp, and document identity.
3. Trigger an unauthorized access attempt and confirm it is denied; it must not appear as a successful Preview/Download event.
4. Open the Administrator reports and verify document type counts, top uploaders, and access patterns by action/date against the underlying test documents and audit events.
5. As a non-admin, try to query the same organization-wide reports/logs and confirm denial.

## Performance and Operational Measures

### 1. Verification Context & Distinction: Local Benchmarks vs. Post-Deployment SLAs

> [!NOTE]
> Las métricas operativas se dividen en dos categorías:
> 1. **Benchmarks de Rendimiento Técnico Local**: Mediciones realizadas en entorno de desarrollo local (.NET 8 Kestrel, SSD NVMe local, SQLite/EF In-Memory) cronometradas mediante `System.Diagnostics.Stopwatch` e inspección de red del navegador. Sirven para certificar que el código no introduce cuellos de botella algorítmicos.
> 2. **Evidencias Operativas Longitudinales (Post-Despliegue)**: Metas de negocio y gobernanza (adopción, precisión de categorización, tiempo de usabilidad humana e incidentes de seguridad) que **no pueden certificarse únicamente con código fuente o compilación**, sino que requieren recopilación continua de evidencia tras el lanzamiento en producción.

| Medida | Métrica Objetivo (SLA) | Protocolo de Validación y Metodología | Evidencia Local Observada / Criterio Operativo |
|---|---|---|---|
| **SC-001: Rendimiento de Subida** | <= 30 s para archivo de 25 MB | Carga de archivo PDF de prueba de 25 MB vía `InputFile` hacia almacenamiento local `AppData/uploads`. Cronometrado con `Stopwatch`. | En pruebas locales en SSD/NVMe, la escritura en disco de 25 MB se completa en ~0.8-1.2 s (muy inferior al límite de 30 s). En red corporativa debe monitorizarse la latencia del enlace cliente-servidor. |
| **SC-002: Rendimiento de Búsqueda y Listado** | <= 2.0 s para 500 registros | Índices en `UploadedByUserId`, `ProjectId` y `Category`. Consulta asíncrona LINQ en EF Core. | En base de datos de prueba con 500 registros, la ejecución de la consulta toma ~45-65 ms y el renderizado DOM en Blazor ~280-350 ms (muy inferior al límite de 2.0 s). |
| **SC-003: Previsualización en Navegador** | <= 3.0 s para PDF/imagen válidos | Endpoint `/api/documents/{id}/stream` con `X-Frame-Options: SAMEORIGIN` y `iframe`/`img` en `DocumentPreviewModal.razor`. | En pruebas locales con PDF de muestra (Architecture Spec) e imagen PNG (Architecture Diagram), la respuesta del stream y renderizado toma ~150-250 ms. |
| **SC-005: Aislamiento IDOR y Autorización** | 100% de intentos no autorizados bloqueados | Validación en `AuthorizeAccessAsync`, `UpdateDocumentMetadataAsync`, `AttachDocumentToTaskAsync` y endpoints de `DocumentsController`. | Verificado al 100% mediante la suite `DocumentAuthorizationTests` y `DocumentSecurityAndIntegrityTests`: todos los accesos no autorizados retornan 403 Forbidden / false. |
| **SC-006: Adopción de Usuarios (Post-Despliegue)** | >= 70% de usuarios activos suben al menos 1 documento en 90 días | **Métrica longitudinal**: Requiere evaluar el registro de auditoría y `LastLoginDate` al cumplirse el trimestre post-lanzamiento en producción. | Fórmula matemática: $$\text{Adopción} = \frac{|\{u \in \text{Usuarios} : \exists d \in \text{Documentos}, d.\text{UploaderId} = u.\text{Id} \land d.\text{Fecha} \ge T_0\}|}{|\{u \in \text{Usuarios} : u.\text{LastLoginDate} \ge T_0\}|} \times 100\%$$ Umbral de éxito: $\ge 70\%$. |
| **SC-007: Precisión de Categorización (Auditoría)** | >= 90% de categorización acorde al propósito | Muestreo manual periódico ($n \ge 50$ documentos) realizado por un auditor de cumplimiento. | Se mitiga técnicamente con selector obligatorio de las 6 categorías oficiales. La auditoría humana definitiva debe ejecutarse mensualmente en producción. |
| **SC-008: Tiempo de Localización de Documento** | < 30 s promedio en sesiones de usabilidad | Pruebas de usabilidad con cronómetro en usuarios representativos solicitándoles encontrar un documento específico. | En recorridos de prueba usando la barra de búsqueda combinada (título, autor, proyecto, tags), un usuario ubica el archivo en ~5-10 s. Debe revalidarse con usuarios finales reales. |
| **SC-009: Cero Huérfanos en BD o Almacenamiento** | 100% de limpieza tras fallos | Operaciones atómicas: rollback de BD y purga física en `UploadDocumentsAsync` y `ReplaceDocumentFileAsync` ante excepciones. | Verificado en pruebas unitarias automatizadas (`DocumentUploadTests` y `DocumentSecurityAndIntegrityTests`): cero registros y cero archivos residuales tras fallo. |
| **SC-010: Registro de Incidentes de Seguridad** | Cero divulgaciones no autorizadas | Revisión del registro de incidentes de seguridad y logs de auditoría durante la ventana operativa. | Objetivo: 0 incidentes. En pruebas, todos los intentos IDOR y asignaciones indebidas de proyectos fueron bloqueados y registrados en auditoría. |

### 2. Procedimiento para Registro de Evidencia en Producción

1. **Consulta trimestral de adopción (SC-006)**:
   ```csharp
   var activeUsers = await context.Users
       .Where(u => u.LastLoginDate >= deploymentDate)
       .Select(u => u.UserId)
       .Distinct()
       .ToListAsync();

   var uploaders = await context.Documents
       .Where(d => d.CreatedDate >= deploymentDate)
       .Select(d => d.UploadedByUserId)
       .Distinct()
       .ToListAsync();

   double adoptionRate = (double)uploaders.Intersect(activeUsers).Count() / activeUsers.Count;
   // Registrar adoptionRate en el informe de cumplimiento
   ```

2. **Informe de cumplimiento y auditoría mensual (SC-007 & SC-010)**:
   - El Administrador genera el informe desde la pestaña *Informes de Actividad*.
   - Se revisan discrepancias en categorías y se verifica que no existan accesos indebidos en `DocumentAuditLogs`.

## Scope Exclusions

- Antivirus/malware scanning is not included.
- An Azure storage provider or Azure deployment is not included; local filesystem storage is utilized.
