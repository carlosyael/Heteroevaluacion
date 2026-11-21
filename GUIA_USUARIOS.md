# Guía de Usuario — Sistema de Heteroevaluación de Tesis

Bienvenido al **Sistema de Heteroevaluación de Tesis**, la plataforma para evaluar exposiciones de tesis entre compañeros de forma organizada y transparente.

- **Enlace principal**: https://app-heteroevaluacion.gentlegrass-7729f379.westus2.azurecontainerapps.io/
- **Portal de Estudiantes**: https://app-heteroevaluacion.gentlegrass-7729f379.westus2.azurecontainerapps.io/estudiante
- **Backoffice del Maestro**: https://app-heteroevaluacion.gentlegrass-7729f379.westus2.azurecontainerapps.io/maestro
- **Instructivo dentro de la app**: https://app-heteroevaluacion.gentlegrass-7729f379.westus2.azurecontainerapps.io/instructivo

---

## Guía para Estudiantes

### 1. ¿Cómo ingresar?
1. Entra al **Portal de Estudiantes** (`/estudiante`).
2. Escribe tu **ID de estudiante (matrícula)** tal como lo registró tu maestro y haz clic en **Acceder**.

### 2. ¿Qué verás al entrar?
- Si el maestro **aún no inicia** la evaluación de ningún compañero, verás un mensaje de espera. La pantalla se **actualiza automáticamente cada 5 segundos** (también puedes usar el botón **Actualizar estado**).
- Cuando la evaluación esté activa, aparecerá **únicamente el compañero que está exponiendo**.
- Si tú eres quien está exponiendo en ese momento, el sistema te mostrará un aviso y no te permitirá evaluarte a ti mismo.

### 3. ¿Cómo calificar?
1. Haz clic en **Evaluar Tesis**.
2. Selecciona explícitamente una calificación del **4 al 0** para cada criterio de la rúbrica:
   - **4 — Excelente**
   - **3 — Bueno**
   - **2 — Satisfactorio**
   - **1 — Deficiente**
   - **0 — No aceptable**
3. Si lo deseas, escribe comentarios en **Evidencia u observación**.
4. Verifica el puntaje total sobre 100 en el panel **Resultado** y confirma el envío definitivo presionando **Enviar Evaluación** y luego **Confirmar envío**.

> **Importante:** Todos los criterios son obligatorios, solo puedes calificar **una vez** a cada compañero y no puedes modificar tu evaluación una vez enviada.

---

## Guía para el Maestro

### 1. ¿Cómo ingresar?
1. Entra al **Backoffice del Maestro** (`/maestro`).
2. Ingresa tu **PIN de acceso** de docente.

### 2. Cargar el listado de estudiantes
En la pestaña **Estudiantes y Carga Masiva**:
- **Carga rápida**: Sube un archivo **CSV** o **Excel (`.xlsx`)** con las columnas `Id`, `Nombre`, `Carrera` y `TemaTesis`. Puedes descargar una plantilla de ejemplo haciendo clic en **Descargar Plantilla Excel (.xlsx)**.
- **Manual**: También puedes agregar, editar o eliminar estudiantes uno por uno (con confirmación antes de eliminar).

### 3. Controlar la evaluación en clase
En el panel superior **Orquestación de Evaluación**:
1. Selecciona en la lista al **estudiante que va a exponer**.
2. Haz clic en **Iniciar Evaluación**. Verás el progreso en vivo (`evaluaciones recibidas`), actualización automática y el listado de estudiantes pendientes.
3. Cuando termine la ronda, haz clic en **Cerrar Evaluación**.

### 4. Ver promedios y descargar resultados
En la pestaña **Resultados y Promedios** (con auto-refresco cada 5 segundos y botón **Refrescar**):
- Consulta el **Promedio** de cada estudiante, cuántas evaluaciones recibió, su clasificación y si está **Aprobado** o **Reprobado**.
- Haz clic en **Ver Detalle** para leer las calificaciones y observaciones de cada evaluador (con fecha y hora local).
- Usa **Rúbrica (.xlsx)** en cada estudiante o **Rúbricas (.zip)** para descargar la plantilla oficial de rúbrica autocompletada con sus resultados.
- Puedes **Eliminar** una calificación puntual, **Limpiar** las de un estudiante o **Eliminar todas las calificaciones** (con doble confirmación).
- Haz clic en **Exportar Resultados (.xlsx)** para descargar el reporte consolidado en Excel.

### 5. Personalizar reglas y reiniciar
En la pestaña **Configuración y Reseteo**:
- Edita el **Nombre del Profesor** y la **Carrera / Programa** del encabezado de la rúbrica.
- Cambia el **Mínimo aprobatorio** y tu **PIN de docente**.
- Ajusta los **Criterios y Pesos (%)** (la suma de criterios activos debe ser `100%`).
- Usa **Eliminar listado y resetear todo** si deseas iniciar un grupo nuevo desde cero.
