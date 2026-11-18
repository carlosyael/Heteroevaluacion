namespace HeteroevaluacionApp.Models;

public record RubricLevel(int Score, string Label, string Description);

public record RubricCriterion(
    int Order,
    string Key,
    string ShortName,
    string FullTitle,
    string ChapterTag,
    double Weight,
    List<RubricLevel> Levels
);

public static class RubricDefinition
{
    public static readonly List<RubricCriterion> Criteria = new()
    {
        new(
            1, "planteamiento", "Planteamiento",
            "1. Planteamiento del problema", "(Cap. I)", 10.0,
            new()
            {
                new(4, "Excelente", "El problema de investigación está claramente delimitado, contextualizado y formulado con preguntas y objetivos coherentes y medibles."),
                new(3, "Bueno", "El problema está bien planteado y contextualizado, aunque con ligeras imprecisiones en la delimitación o en algún objetivo."),
                new(2, "Satisfactorio", "El problema es comprensible, pero su delimitación, preguntas u objetivos requieren mayor claridad y precisión."),
                new(1, "Deficiente", "El planteamiento es vago o desarticulado; las preguntas y objetivos no guardan clara relación entre sí."),
                new(0, "No aceptable", "No se presenta un planteamiento del problema coherente o carece de preguntas y objetivos.")
            }
        ),
        new(
            2, "justificacion", "Justificación",
            "2. Justificación y viabilidad", "(Cap. I)", 5.0,
            new()
            {
                new(4, "Excelente", "Se argumenta sólidamente la relevancia teórica, práctica, metodológica o social del estudio, demostrando su viabilidad."),
                new(3, "Bueno", "La justificación es clara y pertinente, aunque podría profundizar más en algún criterio de relevancia o viabilidad."),
                new(2, "Satisfactorio", "Presenta razones generales para realizar el estudio, pero con poca sustentación académica o práctica."),
                new(1, "Deficiente", "La justificación es superficial, repetitiva o no demuestra la pertinencia ni viabilidad del trabajo."),
                new(0, "No aceptable", "No incluye justificación o los argumentos carecen de relación con el tema de tesis.")
            }
        ),
        new(
            3, "literatura", "Literatura",
            "3. Revisión de literatura y marco teórico", "(Cap. II)", 15.0,
            new()
            {
                new(4, "Excelente", "Integra fuentes académicas actualizadas y pertinentes, con síntesis crítica y articulación directa con las variables del estudio."),
                new(3, "Bueno", "Utiliza fuentes adecuadas y relevantes, aunque la síntesis crítica o la conexión con el problema puede fortalecerse."),
                new(2, "Satisfactorio", "Presenta conceptos básicos del tema, pero con fuentes limitadas, desactualizadas o poco articuladas."),
                new(1, "Deficiente", "El marco teórico es un listado de definiciones sin hilo conductor ni sustento en literatura científica."),
                new(0, "No aceptable", "Carece de marco teórico o presenta contenido sin respaldo bibliográfico.")
            }
        ),
        new(
            4, "metodologia", "Metodología",
            "4. Metodología de la investigación", "(Cap. III)", 20.0,
            new()
            {
                new(4, "Excelente", "Se define con claridad el paradigma, enfoque, diseño y tipo de investigación, coherentes entre sí. La población, muestra y técnica de muestreo están bien justificadas. Los instrumentos son adecuados y su validez y confiabilidad están debidamente sustentadas (juicio de expertos, prueba piloto, Alfa de Cronbach, etc.)."),
                new(3, "Bueno", "El diseño metodológico es coherente y los instrumentos son adecuados, aunque algún aspecto de muestreo o validación requiere mayor detalle."),
                new(2, "Satisfactorio", "Describe el enfoque y los instrumentos de forma general, pero falta rigor en la justificación del diseño, muestra o validez."),
                new(1, "Deficiente", "La metodología presenta inconsistencias entre enfoque, diseño e instrumentos, o carece de criterios de validez."),
                new(0, "No aceptable", "No se define una metodología clara o es incompatible con los objetivos de la investigación.")
            }
        ),
        new(
            5, "resultados", "Resultados",
            "5. Resultados, análisis y discusión", "(Cap. IV)", 25.0,
            new()
            {
                new(4, "Excelente", "Los resultados se presentan de manera clara y sistemática (tablas/gráficos) en correspondencia con los objetivos, con análisis riguroso y discusión contrastada con la literatura."),
                new(3, "Bueno", "Los resultados están bien organizados y analizados, aunque la discusión con antecedentes teóricos puede ser más profunda."),
                new(2, "Satisfactorio", "Presenta resultados descriptivos básicos, pero con análisis limitado y escasa discusión frente al marco teórico."),
                new(1, "Deficiente", "Los resultados son confusos, incompletos o no responden a los objetivos planteados."),
                new(0, "No aceptable", "No presenta resultados ni análisis coherente con la investigación.")
            }
        ),
        new(
            6, "conclusiones", "Conclusiones",
            "6. Conclusiones y recomendaciones", "(Cap. V)", 10.0,
            new()
            {
                new(4, "Excelente", "Las conclusiones responden directamente a cada objetivo y pregunta de investigación, sustentadas en los hallazgos, con recomendaciones prácticas y pertinentes."),
                new(3, "Bueno", "Las conclusiones son coherentes con los resultados, aunque alguna recomendación es general o falta vincular un objetivo."),
                new(2, "Satisfactorio", "Presenta conclusiones generales que resumen el trabajo, pero sin suficiente sustento en los datos obtenidos."),
                new(1, "Deficiente", "Las conclusiones son ambiguas, repiten teoría o no se derivan de los resultados."),
                new(0, "No aceptable", "No presenta conclusiones ni recomendaciones.")
            }
        ),
        new(
            7, "apa7", "APA 7",
            "7. Citas y referencias (APA 7)", "(Normativa)", 5.0,
            new()
            {
                new(4, "Excelente", "Aplica rigurosamente la normativa APA 7ma edición tanto en citas dentro del texto como en la lista de referencias, con correspondencia exacta."),
                new(3, "Bueno", "Cumple con APA 7 en la mayoría del documento, presentando errores menores de puntuación o formato en algunas referencias."),
                new(2, "Satisfactorio", "Aplica parcialmente APA 7, con inconsistencias frecuentes entre citas y referencias."),
                new(1, "Deficiente", "Presenta múltiples errores de citación y referencias incompletas o fuera de norma."),
                new(0, "No aceptable", "No aplica normativa APA 7 ni presenta referencias verificables.")
            }
        ),
        new(
            8, "redaccion", "Redacción",
            "8. Redacción, ortografía y estilo académico", "(Estilo)", 5.0,
            new()
            {
                new(4, "Excelente", "Redacción clara, fluida, objetiva y con vocabulario académico preciso; sin errores ortográficos ni gramaticales."),
                new(3, "Bueno", "Redacción clara y formal, con errores ortográficos o de estilo mínimos que no afectan la comprensión."),
                new(2, "Satisfactorio", "El texto es comprensible, pero presenta fallos frecuentes de cohesión, ortografía o uso informal del lenguaje."),
                new(1, "Deficiente", "Redacción confusa, con numerosos errores ortográficos y gramaticales que dificultan la lectura."),
                new(0, "No aceptable", "El documento es ilegible o carece por completo de estilo académico.")
            }
        ),
        new(
            9, "formato", "Formato",
            "9. Formato y presentación general", "(Estructura)", 5.0,
            new()
            {
                new(4, "Excelente", "Presentación impecable: estructura completa, tipografía uniforme, márgenes, paginación, tablas y figuras correctamente tituladas y numeradas."),
                new(3, "Bueno", "Buen formato general, con detalles menores en numeración, tablas o uniformidad visual."),
                new(2, "Satisfactorio", "Cumple con la estructura básica, pero presenta descuidos visibles en márgenes, títulos o tablas."),
                new(1, "Deficiente", "Formato desordenado e inconsistente en gran parte del documento."),
                new(0, "No aceptable", "No respeta la estructura ni los lineamientos mínimos de presentación.")
            }
        )
    };

    public static string Classify(double totalScore)
    {
        if (totalScore >= 90) return "Excelente";
        if (totalScore >= 80) return "Bueno";
        if (totalScore >= 70) return "Satisfactorio";
        if (totalScore >= 50) return "Deficiente";
        return "No aceptable";
    }

    public static double CalculateWeighted(double weight, int score)
        => Math.Round((score / 4.0) * weight, 2);
}
