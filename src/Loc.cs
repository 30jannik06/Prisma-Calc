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

            // network tools
            A("NETWORK", "NETZWERK", "R\u00C9SEAU", "RED");
            A("Subnet calculator", "Subnetz-Rechner", "Calculateur de sous-r\u00E9seaux", "Calculadora de subredes");
            A("Address converter", "Adress-Umrechner", "Convertisseur d'adresses", "Conversor de direcciones");
            A("Transfer time", "\u00DCbertragungszeit", "Temps de transfert", "Tiempo de transferencia");
            A("Enter an address", "Adresse eingeben", "Saisissez une adresse", "Introduce una direcci\u00F3n");
            A("Not a valid address", "Keine g\u00FCltige Adresse", "Adresse non valide", "Direcci\u00F3n no v\u00E1lida");
            A("Not a valid IPv6 address", "Keine g\u00FCltige IPv6-Adresse", "Adresse IPv6 non valide", "Direcci\u00F3n IPv6 no v\u00E1lida");
            A("Not a valid IPv4 address", "Keine g\u00FCltige IPv4-Adresse", "Adresse IPv4 non valide", "Direcci\u00F3n IPv4 no v\u00E1lida");
            A("The mask is not valid", "Die Maske ist ung\u00FCltig", "Le masque n'est pas valide", "La m\u00E1scara no es v\u00E1lida");
            A("The mask is not contiguous", "Die Maske ist nicht zusammenh\u00E4ngend", "Le masque n'est pas contigu", "La m\u00E1scara no es contigua");
            A("Prefix must be 0 to {0}", "Pr\u00E4fix muss 0 bis {0} sein", "Le pr\u00E9fixe doit \u00EAtre compris entre 0 et {0}", "El prefijo debe estar entre 0 y {0}");
            A("Limited broadcast", "Eingeschr\u00E4nkter Broadcast", "Diffusion limit\u00E9e", "Difusi\u00F3n limitada");
            A("This network", "Dieses Netzwerk", "Ce r\u00E9seau", "Esta red");
            A("Private (RFC 1918)", "Privat (RFC 1918)", "Priv\u00E9e (RFC 1918)", "Privada (RFC 1918)");
            A("Shared address space (CGNAT)", "Gemeinsamer Adressraum (CGNAT)", "Espace d'adresses partag\u00E9 (CGNAT)", "Espacio de direcciones compartido (CGNAT)");
            A("Loopback", "Loopback", "Boucle locale", "Loopback");
            A("Link-local", "Link-lokal", "Lien local", "Enlace local");
            A("IETF protocol assignments", "IETF-Protokollzuweisungen", "Attributions de protocole IETF", "Asignaciones de protocolo IETF");
            A("Documentation", "Dokumentation", "Documentation", "Documentaci\u00F3n");
            A("Benchmarking", "Benchmarking", "Tests de performance", "Pruebas de rendimiento");
            A("Multicast", "Multicast", "Multidiffusion", "Multidifusi\u00F3n");
            A("Reserved", "Reserviert", "R\u00E9serv\u00E9e", "Reservada");
            A("Public", "\u00D6ffentlich", "Publique", "P\u00FAblica");
            A("Class A", "Klasse A", "Classe A", "Clase A");
            A("Class B", "Klasse B", "Classe B", "Clase B");
            A("Class C", "Klasse C", "Classe C", "Clase C");
            A("Class D (multicast)", "Klasse D (Multicast)", "Classe D (multidiffusion)", "Clase D (multidifusi\u00F3n)");
            A("Class E (reserved)", "Klasse E (reserviert)", "Classe E (r\u00E9serv\u00E9e)", "Clase E (reservada)");
            A("Unspecified", "Nicht angegeben", "Non sp\u00E9cifi\u00E9e", "No especificada");
            A("IPv4-mapped", "IPv4-gemappt", "Mapp\u00E9e IPv4", "Asignada a IPv4");
            A("NAT64", "NAT64", "NAT64", "NAT64");
            A("Teredo", "Teredo", "Teredo", "Teredo");
            A("6to4", "6to4", "6to4", "6to4");
            A("Unique local", "Eindeutig lokal (ULA)", "Locale unique (ULA)", "Local \u00FAnica (ULA)");
            A("Global unicast", "Global Unicast", "Unicast global", "Unicast global");
            A("IP version", "IP-Version", "Version IP", "Versi\u00F3n de IP");
            A("Address", "Adresse", "Adresse", "Direcci\u00F3n");
            A("Subnet mask", "Subnetzmaske", "Masque de sous-r\u00E9seau", "M\u00E1scara de subred");
            A("Prefix length", "Pr\u00E4fixl\u00E4nge", "Longueur du pr\u00E9fixe", "Longitud del prefijo");
            A("Wildcard mask", "Wildcard-Maske", "Masque g\u00E9n\u00E9rique", "M\u00E1scara wildcard");
            A("Network address", "Netzwerkadresse", "Adresse r\u00E9seau", "Direcci\u00F3n de red");
            A("Broadcast address", "Broadcast-Adresse", "Adresse de diffusion", "Direcci\u00F3n de difusi\u00F3n");
            A("First host", "Erster Host", "Premier h\u00F4te", "Primer host");
            A("Last host", "Letzter Host", "Dernier h\u00F4te", "\u00DAltimo host");
            A("Total addresses", "Adressen gesamt", "Adresses au total", "Direcciones en total");
            A("Usable hosts", "Nutzbare Hosts", "H\u00F4tes utilisables", "Hosts utilizables");
            A("Address type", "Adresstyp", "Type d'adresse", "Tipo de direcci\u00F3n");
            A("Address class", "Adressklasse", "Classe d'adresse", "Clase de direcci\u00F3n");
            A("Binary address", "Bin\u00E4re Adresse", "Adresse binaire", "Direcci\u00F3n binaria");
            A("Binary mask", "Bin\u00E4re Maske", "Masque binaire", "M\u00E1scara binaria");
            A("First address", "Erste Adresse", "Premi\u00E8re adresse", "Primera direcci\u00F3n");
            A("Last address", "Letzte Adresse", "Derni\u00E8re adresse", "\u00DAltima direcci\u00F3n");
            A("Compressed form", "Kurzform", "Forme compress\u00E9e", "Forma comprimida");
            A("Expanded form", "Langform", "Forme d\u00E9velopp\u00E9e", "Forma expandida");
            A("IPv4-mapped address", "IPv4-gemappte Adresse", "Adresse mapp\u00E9e IPv4", "Direcci\u00F3n asignada a IPv4");
            A("Dotted decimal", "Punktdezimal", "Notation d\u00E9cimale point\u00E9e", "Decimal con puntos");
            A("Decimal number", "Dezimalzahl", "Nombre d\u00E9cimal", "N\u00FAmero decimal");
            A("Hexadecimal", "Hexadezimal", "Hexad\u00E9cimal", "Hexadecimal");
            A("Binary", "Bin\u00E4r", "Binaire", "Binario");
            A("IPv4-mapped IPv6", "IPv4-gemapptes IPv6", "IPv6 mapp\u00E9e IPv4", "IPv6 asignada a IPv4");
            A("Reverse DNS name", "Reverse-DNS-Name", "Nom DNS inverse", "Nombre DNS inverso");
            A("Click to copy", "Zum Kopieren klicken", "Cliquer pour copier", "Haz clic para copiar");
            A("Examples: 192.168.1.10/24, 10.0.0.5 255.255.255.0, 2001:db8::1/64", "Beispiele: 192.168.1.10/24, 10.0.0.5 255.255.255.0, 2001:db8::1/64", "Exemples : 192.168.1.10/24, 10.0.0.5 255.255.255.0, 2001:db8::1/64", "Ejemplos: 192.168.1.10/24, 10.0.0.5 255.255.255.0, 2001:db8::1/64");
            A("Address / prefix", "Adresse / Pr\u00E4fix", "Adresse / pr\u00E9fixe", "Direcci\u00F3n / prefijo");
            A("Check an address", "Adresse pr\u00FCfen", "V\u00E9rifier une adresse", "Comprobar una direcci\u00F3n");
            A("Split into subnets", "In Subnetze aufteilen", "Diviser en sous-r\u00E9seaux", "Dividir en subredes");
            A("New prefix length", "Neue Pr\u00E4fixl\u00E4nge", "Nouvelle longueur de pr\u00E9fixe", "Nueva longitud de prefijo");
            A("Hosts needed", "Ben\u00F6tigte Hosts", "H\u00F4tes n\u00E9cessaires", "Hosts necesarios");
            A("Different IP version", "Andere IP-Version", "Autre version d'IP", "Otra versi\u00F3n de IP");
            A("Inside this network", "Liegt in diesem Netz", "Dans ce r\u00E9seau", "Dentro de esta red");
            A("Outside this network", "Liegt nicht in diesem Netz", "Hors de ce r\u00E9seau", "Fuera de esta red");
            A("Enter a prefix between /{0} and /{1}", "Pr\u00E4fix zwischen /{0} und /{1} eingeben", "Saisissez un pr\u00E9fixe entre /{0} et /{1}", "Introduce un prefijo entre /{0} y /{1}");
            A("{0} subnets", "{0} Subnetze", "{0} sous-r\u00E9seaux", "{0} subredes");
            A("showing the first {0}", "die ersten {0} werden angezeigt", "affichage des {0} premiers", "se muestran los primeros {0}");
            A("Enter a number of hosts", "Anzahl der Hosts eingeben", "Saisissez un nombre d'h\u00F4tes", "Introduce el n\u00FAmero de hosts");
            A("Too many hosts", "Zu viele Hosts", "Trop d'h\u00F4tes", "Demasiados hosts");
            A("Smallest subnet: /{0} ({1} usable)", "Kleinstes Subnetz: /{0} ({1} nutzbar)", "Plus petit sous-r\u00E9seau : /{0} ({1} utilisables)", "Subred m\u00E1s peque\u00F1a: /{0} ({1} utilizables)");
            A("Examples: 192.168.1.1, 3232235777, 0xC0A80101, 2001:db8::1", "Beispiele: 192.168.1.1, 3232235777, 0xC0A80101, 2001:db8::1", "Exemples : 192.168.1.1, 3232235777, 0xC0A80101, 2001:db8::1", "Ejemplos: 192.168.1.1, 3232235777, 0xC0A80101, 2001:db8::1");
            A("Address or number", "Adresse oder Zahl", "Adresse ou nombre", "Direcci\u00F3n o n\u00FAmero");
            A("Solve for", "Berechnen", "Calculer", "Calcular");
            A("Size", "Gr\u00F6\u00DFe", "Taille", "Tama\u00F1o");
            A("Share of the line speed (%)", "Nutzbarer Anteil der Leitung (%)", "Part utilisable de la ligne (%)", "Parte utilizable de la l\u00EDnea (%)");
            A("Time examples: 90, 1:30:00, 2h 15m", "Zeitbeispiele: 90, 1:30:00, 2h 15m", "Exemples de dur\u00E9e : 90, 1:30:00, 2h 15m", "Ejemplos de tiempo: 90, 1:30:00, 2h 15m");
            A("Transfer", "\u00DCbertragung", "Transfert", "Transferencia");
            A("Share must be between 1 and 100", "Anteil muss zwischen 1 und 100 liegen", "La part doit \u00EAtre comprise entre 1 et 100", "La parte debe estar entre 1 y 100");
            A("Enter a size", "Gr\u00F6\u00DFe eingeben", "Saisissez une taille", "Introduce un tama\u00F1o");
            A("Enter a speed", "Geschwindigkeit eingeben", "Saisissez une vitesse", "Introduce una velocidad");
            A("Enter a duration like 90, 1:30:00 or 2h 15m", "Dauer wie 90, 1:30:00 oder 2h 15m eingeben", "Saisissez une dur\u00E9e comme 90, 1:30:00 ou 2h 15m", "Introduce una duraci\u00F3n como 90, 1:30:00 o 2h 15m");
            A("{0} seconds in total", "{0} Sekunden insgesamt", "{0} secondes au total", "{0} segundos en total");

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
