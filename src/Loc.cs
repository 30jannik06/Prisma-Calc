// Prisma - translations (English, Deutsch, Francais, Espanol). English text is the key. C# 5.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Prisma
{
    public static class Loc
    {
        public static readonly string[] Codes = { "en", "de", "fr", "es" };
        public static readonly string[] Native = { "English", "Deutsch", "Français", "Español" };

        public static string Lang = "en";
        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>();

        static void A(string en, string de, string fr, string es) { Table[en] = new[] { de, fr, es }; }

        public static CultureInfo Culture
        {
            get
            {
                try { return CultureInfo.GetCultureInfo(Lang == "de" ? "de-DE" : Lang == "fr" ? "fr-FR" : Lang == "es" ? "es-ES" : "en-US"); }
                catch (Exception) { return CultureInfo.InvariantCulture; }
            }
        }

        public static bool DecimalComma { get { return Lang != "en"; } }

        // Which of the supported languages a choice ("System", "de", "FR", "xx") means.
        public static string Resolve(string choice)
        {
            string c = choice;
            if (string.IsNullOrEmpty(c) || string.Equals(c, "System", StringComparison.OrdinalIgnoreCase))
            {
                try { c = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; } catch (Exception) { c = "en"; }
            }
            foreach (string code in Codes) if (string.Equals(code, c, StringComparison.OrdinalIgnoreCase)) return code;
            return "en";
        }

        public static void Apply(string choice) { Lang = Resolve(choice); }

        public static bool IsSupported(string code)
        {
            foreach (string c in Codes) if (string.Equals(c, code, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static int Index { get { return Lang == "de" ? 0 : Lang == "fr" ? 1 : Lang == "es" ? 2 : -1; } }

        public static string T(string en)
        {
            int i = Index;
            if (i < 0 || en == null) return en;
            string[] row;
            return Table.TryGetValue(en, out row) && !string.IsNullOrEmpty(row[i]) ? row[i] : en;
        }

        public static string F(string en, params object[] args) { return string.Format(Culture, T(en), args); }

        public static string Plural(int n, string one, string many) { return n + " " + T(n == 1 ? one : many); }

        // Decimal comma for display; the engines always work with a dot.
        public static string Num(string s)
        {
            return DecimalComma && s != null ? s.Replace('.', ',') : s;
        }

        public static string DateText(DateTime d)
        {
            switch (Lang)
            {
                case "de": return d.ToString("dddd, dd.MM.yyyy", Culture);
                case "fr": return d.ToString("dddd dd/MM/yyyy", Culture);
                case "es": return d.ToString("dddd, dd/MM/yyyy", Culture);
                default: return d.ToString("dddd, yyyy-MM-dd", Culture);
            }
        }

        public static IEnumerable<string> Keys { get { return Table.Keys; } }
        public static string[] Row(string en) { string[] r; return Table.TryGetValue(en, out r) ? r : null; }
        public static bool Has(string en) { return Table.ContainsKey(en); }

        static Loc()
        {
            // modes and sections
            A("Standard", "Standard", "Standard", "Estándar");
            A("Scientific", "Wissenschaftlich", "Scientifique", "Científica");
            A("Programmer", "Programmierer", "Programmeur", "Programador");
            A("Graphing", "Grafik", "Graphique", "Gráficos");
            A("Date calculation", "Datumsberechnung", "Calcul de dates", "Cálculo de fechas");
            A("Currency", "Währung", "Devise", "Moneda");
            A("Volume", "Volumen", "Volume", "Volumen");
            A("Length", "Länge", "Longueur", "Longitud");
            A("Weight and mass", "Gewicht und Masse", "Poids et masse", "Peso y masa");
            A("Temperature", "Temperatur", "Température", "Temperatura");
            A("Energy", "Energie", "Énergie", "Energía");
            A("Area", "Fläche", "Surface", "Área");
            A("Speed", "Geschwindigkeit", "Vitesse", "Velocidad");
            A("Time", "Zeit", "Temps", "Tiempo");
            A("Power", "Leistung", "Puissance", "Potencia");
            A("Data", "Daten", "Données", "Datos");
            A("Pressure", "Druck", "Pression", "Presión");
            A("Angle", "Winkel", "Angle", "Ángulo");
            A("Settings", "Einstellungen", "Paramètres", "Configuración");
            A("CALCULATOR", "RECHNER", "CALCULATRICE", "CALCULADORA");
            A("CONVERTER", "UMRECHNER", "CONVERTISSEUR", "CONVERSOR");
            A("History", "Verlauf", "Historique", "Historial");
            A("Memory", "Speicher", "Mémoire", "Memoria");
            A("Clear", "Löschen", "Effacer", "Borrar");
            A("There is no history yet.", "Noch kein Verlauf vorhanden.", "Aucun historique pour le moment.", "Todavía no hay historial.");
            A("Nothing is saved in memory.", "Im Speicher ist nichts abgelegt.", "Rien n'est enregistré en mémoire.", "No hay nada guardado en la memoria.");

            // date calculation
            A("Calculation", "Berechnung", "Calcul", "Cálculo");
            A("Difference between dates", "Differenz zwischen Daten", "Différence entre deux dates", "Diferencia entre fechas");
            A("Add or subtract", "Addieren oder subtrahieren", "Ajouter ou soustraire", "Sumar o restar");
            A("Dates as yyyy-mm-dd or dd.mm.yyyy", "Daten als JJJJ-MM-TT oder TT.MM.JJJJ", "Dates au format aaaa-mm-jj ou jj.mm.aaaa", "Fechas como aaaa-mm-dd o dd.mm.aaaa");
            A("From", "Von", "De", "Desde");
            A("To", "Bis", "À", "Hasta");
            A("Add", "Addieren", "Ajouter", "Sumar");
            A("Subtract", "Subtrahieren", "Soustraire", "Restar");
            A("Years", "Jahre", "Années", "Años");
            A("Months", "Monate", "Mois", "Meses");
            A("Days", "Tage", "Jours", "Días");
            A("Same date", "Gleiches Datum", "Même date", "Misma fecha");
            A("Enter two valid dates", "Zwei gültige Daten eingeben", "Saisissez deux dates valides", "Introduce dos fechas válidas");
            A("Enter a valid date and whole numbers", "Gültiges Datum und ganze Zahlen eingeben", "Saisissez une date valide et des nombres entiers", "Introduce una fecha válida y números enteros");
            A("Date out of range", "Datum außerhalb des Bereichs", "Date hors limites", "Fecha fuera de rango");
            A("year", "Jahr", "an", "año"); A("years", "Jahre", "ans", "años");
            A("month", "Monat", "mois", "mes"); A("months", "Monate", "mois", "meses");
            A("day", "Tag", "jour", "día"); A("days", "Tage", "jours", "días");
            A("week", "Woche", "semaine", "semana"); A("weeks", "Wochen", "semaines", "semanas");
            A("Total: {0}", "Gesamt: {0}", "Total : {0}", "Total: {0}");

            // converters
            A("Swap", "Tauschen", "Inverser", "Intercambiar");
            A("Example rates, not live. Edit {0} (CODE=rate per 1 EUR) to use your own.",
              "Beispielkurse, nicht live. Zum Verwenden eigener Kurse {0} bearbeiten (CODE=Kurs pro 1 EUR).",
              "Taux d'exemple, pas en direct. Modifiez {0} (CODE=taux pour 1 EUR) pour utiliser les vôtres.",
              "Tipos de ejemplo, no en vivo. Edita {0} (CÓDIGO=tipo por 1 EUR) para usar los tuyos.");

            // graphing
            A("Equations  y =", "Gleichungen  y =", "Équations  y =", "Ecuaciones  y =");
            A("Reset", "Zurücksetzen", "Réinitialiser", "Restablecer");
            A("Analyze first equation", "Erste Gleichung analysieren", "Analyser la première équation", "Analizar la primera ecuación");
            A("Drag to pan, wheel to zoom, hover to trace.", "Ziehen zum Verschieben, Mausrad zum Zoomen, Darüberfahren zum Verfolgen.",
              "Glisser pour déplacer, molette pour zoomer, survoler pour suivre.", "Arrastra para mover, rueda para ampliar, pasa el ratón para seguir.");
            A("Enter an equation first.", "Zuerst eine Gleichung eingeben.", "Saisissez d'abord une équation.", "Introduce primero una ecuación.");
            A("Zeros: {0}", "Nullstellen: {0}", "Zéros : {0}", "Ceros: {0}");
            A("Maxima: {0}", "Maxima: {0}", "Maxima : {0}", "Máximos: {0}");
            A("Minima: {0}", "Minima: {0}", "Minima : {0}", "Mínimos: {0}");
            A("none in view", "keine im Ausschnitt", "aucun dans la vue", "ninguno en la vista");

            // errors
            A("Division by zero", "Division durch null", "Division par zéro", "División entre cero");
            A("Result too large", "Ergebnis zu groß", "Résultat trop grand", "Resultado demasiado grande");
            A("Invalid input", "Ungültige Eingabe", "Entrée invalide", "Entrada no válida");
            A("Empty expression", "Leerer Ausdruck", "Expression vide", "Expresión vacía");
            A("Unbalanced parentheses", "Klammern nicht ausgeglichen", "Parenthèses non équilibrées", "Paréntesis desequilibrados");
            A("Unexpected '{0}'", "Unerwartetes Zeichen '{0}'", "'{0}' inattendu", "'{0}' inesperado");
            A("Incomplete expression", "Unvollständiger Ausdruck", "Expression incomplète", "Expresión incompleta");
            A("Missing )", "')' fehlt", "')' manquante", "Falta ')'");
            A("Unknown name '{0}'", "Unbekannter Name '{0}'", "Nom inconnu '{0}'", "Nombre desconocido '{0}'");
            A("Bad number '{0}'", "Ungültige Zahl '{0}'", "Nombre invalide '{0}'", "Número no válido '{0}'");
            A("Unknown function '{0}'", "Unbekannte Funktion '{0}'", "Fonction inconnue '{0}'", "Función desconocida '{0}'");
            A("Expression too long", "Ausdruck zu lang", "Expression trop longue", "Expresi\u00F3n demasiado larga");
            A("Expression too complex", "Ausdruck zu verschachtelt", "Expression trop complexe", "Expresi\u00F3n demasiado compleja");
            A("Undefined result", "Ergebnis nicht definiert", "Résultat indéfini", "Resultado indefinido");
            A("Result is not finite", "Ergebnis ist nicht endlich", "Le résultat n'est pas fini", "El resultado no es finito");

            // settings
            A("Appearance", "Erscheinungsbild", "Apparence", "Apariencia");
            A("System follows the Windows light/dark setting and changes with it.", "„System“ folgt der Hell/Dunkel-Einstellung von Windows und wechselt mit.",
              "« Système » suit le réglage clair/sombre de Windows et change avec lui.", "«Sistema» sigue el ajuste claro/oscuro de Windows y cambia con él.");
            A("System", "System", "Système", "Sistema");
            A("Light", "Hell", "Clair", "Claro");
            A("Dark", "Dunkel", "Sombre", "Oscuro");
            A("Theme style", "Theme-Stil", "Style du thème", "Estilo del tema");
            A("Aurora", "Aurora", "Aurore", "Aurora");
            A("Sunset", "Abendrot", "Crépuscule", "Atardecer");
            A("Ocean", "Ozean", "Océan", "Océano");
            A("Forest", "Wald", "Forêt", "Bosque");
            A("Rose", "Rose", "Rose", "Rosa");
            A("Graphite", "Graphit", "Graphite", "Grafito");
            A("Classic", "Klassisch", "Classique", "Clásico");
            A("Accent colour", "Akzentfarbe", "Couleur d'accent", "Color de énfasis");
            A("Colours the equals key, selections and highlights.", "Färbt die Gleichtaste, Auswahlen und Hervorhebungen.",
              "Colore la touche égal, les sélections et les surbrillances.", "Colorea la tecla igual, las selecciones y los resaltados.");
            A("Theme default", "Theme-Standard", "Défaut du thème", "Predeterminado del tema");
            A("Windows accent", "Windows-Akzent", "Accent Windows", "Énfasis de Windows");
            A("Effects", "Effekte", "Effets", "Efectos");
            A("Liquid glass surfaces", "Flüssige Glasflächen", "Surfaces en verre liquide", "Superficies de cristal líquido");
            A("Translucent panels and keys with a light edge.", "Durchscheinende Flächen und Tasten mit heller Kante.",
              "Panneaux et touches translucides avec un bord clair.", "Paneles y teclas translúcidos con borde claro.");
            A("Animated background", "Animierter Hintergrund", "Arrière-plan animé", "Fondo animado");
            A("Colour blobs drift slowly behind the glass. Turn off to save power.", "Farbflecken treiben langsam hinter dem Glas. Zum Energiesparen ausschalten.",
              "Des taches de couleur dérivent lentement derrière le verre. Désactiver pour économiser l'énergie.",
              "Manchas de color se desplazan lentamente tras el cristal. Desáctivalo para ahorrar energía.");
            A("On", "An", "Activé", "Activado");
            A("Off", "Aus", "Désactivé", "Desactivado");
            A("The animation is only visible while glass surfaces are on.", "Die Animation ist nur bei eingeschalteten Glasflächen sichtbar.",
              "L'animation n'est visible que lorsque les surfaces en verre sont activées.", "La animación solo se ve con las superficies de cristal activadas.");
            A("Restore default look", "Standardlook wiederherstellen", "Rétablir l'apparence par défaut", "Restaurar el aspecto predeterminado");
            A("Settings are stored in {0}.", "Einstellungen werden in {0} gespeichert.", "Les paramètres sont enregistrés dans {0}.", "La configuración se guarda en {0}.");
            A("Corners", "Ecken", "Coins", "Esquinas");
            A("Roundness of keys, panels and fields.", "Rundung von Tasten, Fl\u00E4chen und Feldern.", "Arrondi des touches, panneaux et champs.", "Redondez de teclas, paneles y campos.");
            A("Square", "Eckig", "Carr\u00E9", "Cuadradas");
            A("Soft", "Weich", "Doux", "Suaves");
            A("Round", "Rund", "Rond", "Redondas");
            A("About", "Info", "\u00C0 propos", "Acerca de");
            A("Version {0}", "Version {0}", "Version {0}", "Versi\u00F3n {0}");
            A("A calculator with liquid glass themes. Open source under the MIT licence.", "Ein Taschenrechner mit Liquid-Glass-Themes. Open Source unter der MIT-Lizenz.",
              "Une calculatrice avec des th\u00E8mes en verre liquide. Open source sous licence MIT.", "Una calculadora con temas de cristal l\u00EDquido. C\u00F3digo abierto bajo licencia MIT.");
            A("Language", "Sprache", "Langue", "Idioma");
            A("Changes the language of the app and the number format (decimal comma).", "Ändert die Sprache der App und das Zahlenformat (Dezimalkomma).",
              "Change la langue de l'application et le format des nombres (virgule décimale).", "Cambia el idioma de la aplicación y el formato numérico (coma decimal).");
            A("Automatic (Windows)", "Automatisch (Windows)", "Automatique (Windows)", "Automático (Windows)");

            // unit names (abbreviations are not translated; names equal in all languages are left out on purpose)
            A("Milliliter", "Milliliter", "Millilitre", "Mililitro"); A("Liter", "Liter", "Litre", "Litro");
            A("Cubic meter", "Kubikmeter", "Mètre cube", "Metro cúbico");
            A("Teaspoon (US)", "Teelöffel (US)", "Cuillère à café (US)", "Cucharadita (EE. UU.)");
            A("Tablespoon (US)", "Esslöffel (US)", "Cuillère à soupe (US)", "Cucharada (EE. UU.)");
            A("Fluid ounce (US)", "Flüssigunze (US)", "Once liquide (US)", "Onza líquida (EE. UU.)");
            A("Cup (US)", "Tasse (US)", "Tasse (US)", "Taza (EE. UU.)");
            A("Pint (US)", "Pint (US)", "Pinte (US)", "Pinta (EE. UU.)");
            A("Quart (US)", "Quart (US)", "Quart (US)", "Cuarto (EE. UU.)");
            A("Gallon (US)", "Gallone (US)", "Gallon (US)", "Galón (EE. UU.)");
            A("Cubic foot", "Kubikfuß", "Pied cube", "Pie cúbico"); A("Cubic inch", "Kubikzoll", "Pouce cube", "Pulgada cúbica");
            A("Nanometer", "Nanometer", "Nanomètre", "Nanómetro"); A("Micrometer", "Mikrometer", "Micromètre", "Micrómetro");
            A("Millimeter", "Millimeter", "Millimètre", "Milímetro"); A("Centimeter", "Zentimeter", "Centimètre", "Centímetro");
            A("Meter", "Meter", "Mètre", "Metro"); A("Kilometer", "Kilometer", "Kilomètre", "Kilómetro");
            A("Inch", "Zoll", "Pouce", "Pulgada"); A("Foot", "Fuß", "Pied", "Pie"); A("Yard", "Yard", "Yard", "Yarda");
            A("Mile", "Meile", "Mille", "Milla"); A("Nautical mile", "Seemeile", "Mille marin", "Milla náutica");
            A("Milligram", "Milligramm", "Milligramme", "Miligramo"); A("Gram", "Gramm", "Gramme", "Gramo");
            A("Kilogram", "Kilogramm", "Kilogramme", "Kilogramo"); A("Tonne", "Tonne", "Tonne", "Tonelada");
            A("Ounce", "Unze", "Once", "Onza"); A("Pound", "Pfund", "Livre", "Libra");
            A("Short ton (US)", "Short Ton (US)", "Tonne courte (US)", "Tonelada corta (EE. UU.)");
            A("Joule", "Joule", "Joule", "Julio"); A("Kilojoule", "Kilojoule", "Kilojoule", "Kilojulio");
            A("Calorie (thermal)", "Kalorie (thermochemisch)", "Calorie (thermochimique)", "Caloría (termoquímica)");
            A("Kilocalorie", "Kilokalorie", "Kilocalorie", "Kilocaloría");
            A("Watt-hour", "Wattstunde", "Wattheure", "Vatio-hora"); A("Kilowatt-hour", "Kilowattstunde", "Kilowattheure", "Kilovatio-hora");
            A("Electronvolt", "Elektronenvolt", "Électronvolt", "Electronvoltio");
            A("British thermal unit", "British Thermal Unit", "British thermal unit", "Unidad térmica británica");
            A("Foot-pound", "Fuß-Pfund", "Pied-livre", "Libra-pie");
            A("Square millimeter", "Quadratmillimeter", "Millimètre carré", "Milímetro cuadrado");
            A("Square centimeter", "Quadratzentimeter", "Centimètre carré", "Centímetro cuadrado");
            A("Square meter", "Quadratmeter", "Mètre carré", "Metro cuadrado"); A("Hectare", "Hektar", "Hectare", "Hectárea");
            A("Square kilometer", "Quadratkilometer", "Kilomètre carré", "Kilómetro cuadrado");
            A("Square inch", "Quadratzoll", "Pouce carré", "Pulgada cuadrada"); A("Square foot", "Quadratfuß", "Pied carré", "Pie cuadrado");
            A("Square yard", "Quadratyard", "Yard carré", "Yarda cuadrada"); A("Square mile", "Quadratmeile", "Mille carré", "Milla cuadrada");
            A("Meter per second", "Meter pro Sekunde", "Mètre par seconde", "Metro por segundo");
            A("Kilometer per hour", "Kilometer pro Stunde", "Kilomètre par heure", "Kilómetro por hora");
            A("Mile per hour", "Meile pro Stunde", "Mille par heure", "Milla por hora");
            A("Knot", "Knoten", "Nœud", "Nudo"); A("Foot per second", "Fuß pro Sekunde", "Pied par seconde", "Pie por segundo");
            A("Microsecond", "Mikrosekunde", "Microseconde", "Microsegundo"); A("Millisecond", "Millisekunde", "Milliseconde", "Milisegundo");
            A("Second", "Sekunde", "Seconde", "Segundo"); A("Minute", "Minute", "Minute", "Minuto");
            A("Hour", "Stunde", "Heure", "Hora"); A("Day", "Tag", "Jour", "Día"); A("Week", "Woche", "Semaine", "Semana");
            A("Year (365 days)", "Jahr (365 Tage)", "Année (365 jours)", "Año (365 días)");
            A("Watt", "Watt", "Watt", "Vatio"); A("Kilowatt", "Kilowatt", "Kilowatt", "Kilovatio");
            A("Horsepower (mechanical)", "Pferdestärke (mechanisch)", "Cheval-vapeur (mécanique)", "Caballo de fuerza (mecánico)");
            A("Foot-pound per minute", "Fuß-Pfund pro Minute", "Pied-livre par minute", "Libra-pie por minuto");
            A("BTU per minute", "BTU pro Minute", "BTU par minute", "BTU por minuto");
            A("Atmosphere", "Atmosphäre", "Atmosphère", "Atmósfera");
            A("Pound per square inch", "Pfund pro Quadratzoll", "Livre par pouce carré", "Libra por pulgada cuadrada");
            A("Degree", "Grad", "Degré", "Grado"); A("Radian", "Radiant", "Radian", "Radián"); A("Gradian", "Gon", "Grade", "Gradián");
        }
    }
}
