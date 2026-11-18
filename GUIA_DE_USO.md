# Guía de Uso — Sistema de Heteroevaluación de Tesis

## 1. Acceso al Sistema
- **URL Principal**: `http://localhost:8080`
- **Portal de Estudiantes**: `http://localhost:8080/estudiante`
- **Backoffice del Maestro**: `http://localhost:8080/maestro` (PIN inicial: `profesor2026`)

---

## 2. Flujo del Maestro (`/maestro`)

### A. Carga y Gestión de Estudiantes (Pestaña *Estudiantes y Carga Masiva*)
1. **Carga masiva**: Sube un archivo `.csv` o `.xlsx` con las columnas `Id`, `Nombre`, `Carrera`, `TemaTesis`. Puedes descargar la plantilla desde **Descargar Plantilla Excel (.xlsx)**.
2. **Registro manual**: También puedes agregar, actualizar o eliminar estudiantes individualmente.

### B. Orquestación de Evaluación (Panel superior)
1. Selecciona en el menú desplegable al **estudiante que va a exponer / ser evaluado**.
2. Haz clic en **Iniciar Evaluación**. En ese momento, todos los demás estudiantes verán únicamente a ese compañero en su portal.
3. Cuando termine la ronda, haz clic en **Cerrar Evaluación**.

### C. Resultados y Exportación (Pestaña *Resultados y Promedios*)
- Visualiza el **Promedio** de cada estudiante (calculado automáticamente con todas las evaluaciones recibidas), cantidad de evaluaciones recibidas/realizadas, clasificación y estado (**Aprobado** / **Reprobado**).
- Haz clic en **Ver Detalle** para revisar las calificaciones y observaciones individuales de cada evaluador.
- Haz clic en **Exportar Resultados (.xlsx)** para descargar un archivo Excel con dos hojas:
  1. **Resumen Promedios**: Promedio final y estado de cada estudiante.
  2. **Detalle Evaluaciones**: Todas las calificaciones individuales por evaluador, criterio y observaciones.

### D. Configuración y Reseteo (Pestaña *Configuración y Reseteo*)
- **Mínimo aprobatorio**: Define el puntaje mínimo (0–100) para aprobar (por defecto `70`).
- **Cambiar contraseña / PIN**: Actualiza el PIN de acceso al Backoffice.
- **Resetear Sistema desde 0**: Elimina todo el listado de estudiantes y todas las evaluaciones para iniciar un nuevo grupo o semestre.

---

## 3. Flujo del Estudiante (`/estudiante`)
1. Ingresa con tu **ID (matrícula)** previamente cargado por el maestro.
2. Si el maestro aún no inicia una evaluación, verás un aviso de espera (puedes usar **Actualizar estado**).
3. Cuando la evaluación esté activa para un compañero, haz clic en **Evaluar Tesis**.
4. Califica los **9 criterios** (escala `4 Excelente` a `0 No aceptable`), agrega **Evidencia u observación** si lo deseas y verifica el puntaje en el panel **Resultado**.
5. Haz clic en **Enviar Evaluación**. Solo puedes calificar **una vez** a cada compañero y no puedes evaluarte a ti mismo.
