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
            "1. Planteamiento del problema, antecedentes y objetivos", "(Cap. I)", 10.0,
            new()
            {
                new(4, "Excelente", "El problema está claramente planteado, es relevante, tiene posibilidad de solución y se sustenta en antecedentes nacionales e internacionales pertinentes y actualizados. Los objetivos general y específicos son claros, precisos, medibles y coherentes con la formulación y sistematización del problema."),
                new(3, "Bueno", "El problema está bien planteado y es relevante, aunque los antecedentes presentan alguna limitación menor (poca actualidad o variedad). Los objetivos son claros pero con leve falta de coherencia entre el general y los específicos."),
                new(2, "Satisfactorio", "El problema es relevante pero su solución es predecible; los antecedentes son escasos o poco variados. Los objetivos presentan dificultades de redacción o de alineación con la sistematización del problema."),
                new(1, "Deficiente", "El problema tiene poca relevancia y limitada contribución al conocimiento; los antecedentes no guardan relación directa con el tema. Los objetivos son imprecisos."),
                new(0, "No aceptable", "No hay planteamiento del problema claro, ni antecedentes ni objetivos identificables, o carecen de coherencia interna.")
            }
        ),
        new(
            2, "justificacion", "Justificación",
            "2. Justificación e hipótesis", "(Cap. I)", 5.0,
            new()
            {
                new(4, "Excelente", "La justificación argumenta con solidez la relevancia social, académica y práctica de la investigación, vinculándola claramente con las líneas de investigación de la maestría. La hipótesis (si aplica) se formula correctamente y guarda relación directa con las variables del estudio."),
                new(3, "Bueno", "La justificación es adecuada y vincula la investigación con la maestría, aunque algún aspecto (social, académico o práctico) podría desarrollarse más. La hipótesis es correcta con alguna imprecisión menor."),
                new(2, "Satisfactorio", "La justificación es superficial o genérica, con vínculo débil con las líneas de la maestría. La hipótesis presenta dificultad de redacción o relación con las variables."),
                new(1, "Deficiente", "La justificación es débil o incompleta, sin vínculo claro con la maestría. La hipótesis (si aplica) no guarda relación con las variables."),
                new(0, "No aceptable", "No hay justificación identificable ni hipótesis pertinente (cuando la investigación lo requiere).")
            }
        ),
        new(
            3, "literatura", "Literatura",
            "3. Revisión de la literatura (marco teórico, conceptual y legal)", "(Cap. II)", 15.0,
            new()
            {
                new(4, "Excelente", "El marco teórico integra teorías, conceptos y antecedentes investigativos actualizados y pertinentes, con fuentes variadas, confiables y de bases de datos científicas. Se evidencia el estado del arte y una síntesis crítica que sustenta las variables del estudio."),
                new(3, "Bueno", "El marco teórico es sólido y con fuentes confiables, aunque la síntesis crítica o el estado del arte podrían profundizarse más."),
                new(2, "Satisfactorio", "El marco teórico cubre los conceptos básicos, pero las fuentes son limitadas o poco variadas, o falta relación directa con las variables del estudio."),
                new(1, "Deficiente", "El marco teórico es superficial, con fuentes escasas o poco confiables, y sin conexión clara con el problema planteado."),
                new(0, "No aceptable", "Carece de marco teórico pertinente o las fuentes utilizadas no corresponden al tema.")
            }
        ),
        new(
            4, "metodologia", "Metodología",
            "4. Marco metodológico", "(Cap. III)", 20.0,
            new()
            {
                new(4, "Excelente", "Se define con claridad el paradigma, enfoque, diseño y tipo de investigación, coherentes entre sí. La población, muestra y técnica de muestreo están bien justificadas. Los instrumentos son adecuados y su validez y confiabilidad están debidamente sustentadas (juicio de expertos, prueba piloto, Alfa de Cronbach, etc.)."),
                new(3, "Bueno", "La metodología es adecuada para resolver el problema, aunque la descripción de la muestra o la validación de instrumentos presenta alguna imprecisión."),
                new(2, "Satisfactorio", "Se identifican el enfoque y diseño, pero hay dificultad en la selección de la metodología, la muestra o los instrumentos utilizados."),
                new(1, "Deficiente", "El enfoque o diseño no es adecuado para resolver el problema. La población/muestra o los instrumentos carecen de justificación o validación."),
                new(0, "No aceptable", "No se establece metodología, o esta es totalmente inapropiada para la solución del problema.")
            }
        ),
        new(
            5, "resultados", "Resultados",
            "5. Resultados, análisis y discusión", "(Cap. IV)", 25.0,
            new()
            {
                new(4, "Excelente", "Los resultados se presentan de manera clara y sistemática (tablas/gráficos) en correspondencia con los objetivos. El análisis es objetivo, establece relaciones entre los datos, permite inferencias y se discute críticamente contrastando con el marco teórico."),
                new(3, "Bueno", "Los resultados están bien presentados y el análisis es objetivo, aunque la discusión con la literatura previa podría profundizarse."),
                new(2, "Satisfactorio", "Los resultados se presentan, pero el análisis tiene dificultad estableciendo relaciones entre variables o discutiendo los hallazgos frente al marco teórico."),
                new(1, "Deficiente", "La presentación de resultados es limitada y el análisis carece de objetividad o de relación con el marco teórico."),
                new(0, "No aceptable", "No se presentan resultados de forma organizada ni se realiza análisis o discusión.")
            }
        ),
        new(
            6, "conclusiones", "Conclusiones",
            "6. Conclusiones y recomendaciones", "(Cap. V)", 10.0,
            new()
            {
                new(4, "Excelente", "Las conclusiones responden directamente a cada objetivo específico, se sustentan en los datos y evitan generalizaciones infundadas. Las recomendaciones son pertinentes y se derivan claramente de los hallazgos."),
                new(3, "Bueno", "Las conclusiones responden a los objetivos y se sustentan en los datos, con leves generalizaciones. Las recomendaciones son adecuadas."),
                new(2, "Satisfactorio", "Las conclusiones responden parcialmente a los objetivos o su sustentación en los datos es débil."),
                new(1, "Deficiente", "Las conclusiones responden poco a los objetivos y las recomendaciones son genéricas o poco fundamentadas."),
                new(0, "No aceptable", "No hay correspondencia entre conclusiones y objetivos, ni recomendaciones pertinentes.")
            }
        ),
        new(
            7, "apa7", "APA 7",
            "7. Referencias y citación (APA 7)", "(Normativa)", 5.0,
            new()
            {
                new(4, "Excelente", "Todas las fuentes citadas en el texto están documentadas en la bibliografía y viceversa, siguiendo estrictamente el formato APA 7.ª edición, sin errores."),
                new(3, "Bueno", "Las fuentes están correctamente documentadas en APA 7, con errores mínimos y aislados."),
                new(2, "Satisfactorio", "Las fuentes están documentadas, pero con dificultad en la aplicación consistente del formato APA 7."),
                new(1, "Deficiente", "Existen fuentes citadas en el texto que no aparecen en la bibliografía (o viceversa), y el formato APA presenta múltiples errores."),
                new(0, "No aceptable", "No se documentan las fuentes o no se utiliza el formato APA establecido.")
            }
        ),
        new(
            8, "redaccion", "Redacción",
            "8. Redacción y estilo académico", "(Estilo)", 5.0,
            new()
            {
                new(4, "Excelente", "Redacción en tercera persona, vocabulario claro, extensión apropiada de las oraciones, sin errores ortográficos ni gramaticales."),
                new(3, "Bueno", "Redacción clara con menos de 3 errores ortográficos o gramaticales."),
                new(2, "Satisfactorio", "Redacción comprensible en general, aunque a veces se pierde la idea por la extensión de las oraciones; menos de 5 errores."),
                new(1, "Deficiente", "Dificultad para entender lo expuesto; entre 6 y 8 errores ortográficos o gramaticales."),
                new(0, "No aceptable", "Redacción difícil de entender, con gran número de errores.")
            }
        ),
        new(
            9, "formato", "Formato",
            "9. Cumplimiento de normas de formato (Guía UNAPEC)", "(Estructura)", 5.0,
            new()
            {
                new(4, "Excelente", "El documento cumple totalmente los requisitos formales de la guía: encuadernado, papel, tipo y tamaño de letra, márgenes, espaciado, sangrías, numeración de páginas y presentación de tablas y figuras."),
                new(3, "Bueno", "Cumple la mayoría de los requisitos formales, con desviaciones menores en uno o dos aspectos (p. ej. numeración o espaciado)."),
                new(2, "Satisfactorio", "Cumple parcialmente los requisitos formales; se identifican varias desviaciones respecto a la guía."),
                new(1, "Deficiente", "El documento presenta múltiples incumplimientos del formato establecido en la guía."),
                new(0, "No aceptable", "El documento no sigue el formato institucional establecido en la guía.")
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
