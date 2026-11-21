# Documentación de Funcionalidades — Sistema de Heteroevaluación de Tesis

## 1. Descripción General
Aplicación web desarrollada en **ASP.NET Blazor (.NET 9, InteractiveServer)** con **Entity Framework Core (Code First)** y **MySQL 8.x**, dockerizada y desplegada en **Azure Container Apps** conectada a **Azure Database for MySQL**.

Permite que los estudiantes evalúen las tesis de sus compañeros bajo la orquestación en tiempo real del docente, calculando automáticamente el promedio de cada estudiante y generando reportes descargables en Excel.

---

## 2. Arquitectura y Base de Datos (Code First)

### Entidades principales
1. **`Student`** (`Students`):
   - `Id` (PK, matrícula o código del estudiante)
   - `Nombre`, `Carrera`, `TemaTesis`, `CreatedAt`
2. **`Evaluation`** (`Evaluations`):
   - `Id` (PK), `EvaluatorId` (FK), `EvaluatedId` (FK), `TotalScore`, `Classification`, `Approved`, `CreatedAt`
   - Índice único `(EvaluatorId, EvaluatedId)` que impide a nivel de base de datos que un estudiante evalúe más de una vez al mismo compañero.
3. **`EvaluationDetail`** (`EvaluationDetails`):
   - `Id` (PK), `EvaluationId` (FK), `CriterionKey`, `Score` (0–4), `WeightedPoints`, `Evidence` (observaciones).
4. **`EvaluationState`** (`EvaluationStates`):
   - `Id = 1`, `ActiveStudentId`, `IsOpen`, `MinPassingScore` (por defecto `70.0`), `TeacherPin` (por defecto `profesor2026`), `UpdatedAt`.
5. **`CriterionConfig`** (`CriterionConfigs`):
   - `Key` (PK), `Order`, `ShortName`, `FullTitle`, `ChapterTag`, `Weight`, `IsActive`.

---

## 3. Portal de Estudiantes (`/estudiante`)
- **Acceso validado por ID**: El estudiante ingresa su `Id` (matrícula); el sistema valida que exista en el listado cargado por el maestro.
- **Orquestación estricta**:
  - Si no hay ninguna evaluación activa (`IsOpen == false`), muestra un aviso de espera con botón **Actualizar estado**.
  - Si el estudiante logueado es quien está siendo evaluado (`ActiveStudentId == CurrentStudent.Id`), bloquea la autoevaluación.
  - Si ya evaluó al compañero activo, muestra su calificación previa y deshabilita el botón de evaluar.
- **Rúbrica de Heteroevaluación de Tesis**:
  - Escala de 5 niveles por criterio: `4 Excelente`, `3 Bueno`, `2 Satisfactorio`, `1 Deficiente`, `0 No aceptable`.
  - Descripción dinámica del nivel seleccionado, campo **Evidencia u observación** y cálculo en tiempo real de **Puntos ponderados**.
  - Panel lateral **Resultado** con gráfico circular SVG, puntaje sobre 100, clasificación (`Excelente`, `Bueno`, `Satisfactorio`, `Deficiente`, `No aceptable`), estado (`Aprobado` / `Reprobado` según `MinPassingScore`) y barras de progreso por criterio.

---

## 4. Backoffice del Maestro (`/maestro`)
- **Autenticación por PIN**: Protegido mediante PIN almacenado en MySQL (`profesor2026` por defecto, modificable desde el panel).
- **Panel de Orquestación de Evaluación**:
  - Selector del estudiante a evaluar.
  - Botones **Iniciar Evaluación** y **Cerrar Evaluación** con indicador de estado en vivo (`ACTIVA` / `CERRADA`).
- **Pestaña 1: Estudiantes y Carga Masiva**:
  - Importación masiva desde **CSV** (`,` o `;`) o **Excel (`.xlsx` / `.xls`)** con columnas `Id`, `Nombre`, `Carrera`, `TemaTesis`.
  - Descarga de plantilla Excel (`Plantilla_Estudiantes.xlsx`).
  - Alta, actualización, búsqueda y eliminación individual de estudiantes.
- **Pestaña 2: Resultados y Promedios**:
  - Tabla resumen con evaluaciones recibidas, realizadas, **Promedio** automático, clasificación y estado.
  - **Refrescar**: Actualiza los resultados en tiempo real sin recargar la página.
  - **Ver Detalle**: Muestra todas las evaluaciones individuales recibidas por un estudiante, con puntaje, observaciones por criterio y botón **Eliminar** individual.
  - **Limpiar**: Elimina todas las evaluaciones recibidas por un estudiante específico.
  - **Eliminar todas las calificaciones**: Borra todas las evaluaciones manteniendo el listado de estudiantes.
  - **Exportar Resultados (.xlsx)**: Genera un Excel con dos hojas: `Resumen Promedios` y `Detalle Evaluaciones`.
- **Pestaña 3: Configuración y Reseteo**:
  - Ajuste del **Mínimo aprobatorio** (0–100).
  - Cambio de **contraseña / PIN** del docente.
  - Editor de **Criterios de Evaluación y Pesos (%)** (nombre corto, título, etiqueta, peso, activar/desactivar, validación de suma = 100% y restauración por defecto).
  - **Resetear Sistema desde 0**: Elimina estudiantes, evaluaciones y reinicia el estado con confirmación doble.

---

## 5. Instructivo de Uso (`/instructivo`)
Página integrada en la aplicación con la guía paso a paso para docentes y estudiantes, accesible desde el menú lateral.

---

## 6. Infraestructura y Despliegue
- **Docker local**: `Dockerfile` + `docker-compose.yml` (servicios `web` en puerto `8080` y `db` MySQL 8.0 en puerto `3306`).
- **Azure**: Desplegado en **Azure Container Apps** (`app-heteroevaluacion`) con imagen en **Azure Container Registry** (`ca3cf8422fd4acr.azurecr.io`) y base de datos en **Azure Database for MySQL Flexible Server** (`mysql-heteroevaluacion-yael.mysql.database.azure.com`).
