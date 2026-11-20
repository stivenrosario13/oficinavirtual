using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace BloomCobrosAgent.Services;

public sealed class SpreadsheetImporter
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = ["id", "codigo", "cliente id", "id cliente"],
        ["contract"] = ["contrato", "contract", "referencia", "cuenta"],
        ["nationalId"] = ["cedula", "cédula", "documento", "identificacion", "identificación", "dni", "rnc"],
        ["firstName"] = ["nombre", "nombres", "first name"],
        ["lastName"] = ["apellido", "apellidos", "last name"],
        ["phone"] = ["telefono", "teléfono", "celular", "movil", "móvil", "whatsapp"],
        ["balance"] = ["balance", "saldo", "monto", "deuda"],
        ["monthlyFee"] = ["mensualidad", "cuota", "renta"],
        ["dueDate"] = ["fecha de pago", "fecha pago", "vencimiento", "fecha vencimiento"],
        ["status"] = ["estatus", "estado", "status", "estatus contrato"],
        ["sector"] = ["sector", "barrio"],
        ["location"] = ["localidad", "ciudad", "municipio"],
        ["consent"] = ["consentimiento whatsapp", "autorizado whatsapp", "opt in", "consentimiento"],
        ["doNotContact"] = ["no contactar", "excluir", "lista negra"]
    };

    public ImportResult Import(string path, ImportSnapshot? previous)
    {
        var rows = Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ReadCsv(path)
            : ReadXlsx(path);
        if (rows.Count < 2) throw new InvalidDataException("El archivo no contiene filas de clientes.");

        var headers = rows[0].Select((value, index) => new { Key = CollectionEngine.Normalize(value), index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First().index);
        int Col(string field)
        {
            foreach (var alias in Aliases[field])
                if (headers.TryGetValue(CollectionEngine.Normalize(alias), out var index)) return index;
            return -1;
        }
        string Cell(List<string> row, string field)
        {
            var index = Col(field);
            return index >= 0 && index < row.Count ? row[index].Trim() : "";
        }

        var warnings = new List<string>();
        if (Col("phone") < 0) warnings.Add("No se encontró una columna de teléfono.");
        if (Col("balance") < 0) warnings.Add("No se encontró una columna de balance/saldo.");
        if (Col("consent") < 0) warnings.Add("No existe una columna de consentimiento; todos quedan sin autorización.");

        var customers = new List<Customer>();
        foreach (var row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var customer = new Customer
            {
                Id = Cell(row, "id"), Contract = Cell(row, "contract"), NationalId = Cell(row, "nationalId"),
                FirstName = Cell(row, "firstName"), LastName = Cell(row, "lastName"),
                Phone = CollectionEngine.NormalizePhone(Cell(row, "phone")),
                Balance = ParseDecimal(Cell(row, "balance")), MonthlyFee = ParseDecimal(Cell(row, "monthlyFee")),
                DueDate = ParseDate(Cell(row, "dueDate")), SourceStatus = Cell(row, "status"),
                Sector = Cell(row, "sector"), Location = Cell(row, "location"),
                WhatsAppConsent = ParseBool(Cell(row, "consent")), DoNotContact = ParseBool(Cell(row, "doNotContact"))
            };
            CollectionEngine.Classify(customer, DateTime.Today);
            customers.Add(customer);
        }

        var previousByKey = (previous?.Customers ?? []).Where(c => !string.IsNullOrWhiteSpace(Key(c)))
            .GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var paid = customers.Count(c => c.Balance <= 0 && previousByKey.TryGetValue(Key(c), out var old) && old.Balance > 0);
        return new(customers, paid, warnings);
    }

    private static string Key(Customer c) => !string.IsNullOrWhiteSpace(c.Contract) ? "C:" + c.Contract : !string.IsNullOrWhiteSpace(c.Id) ? "I:" + c.Id : "P:" + c.Phone;

    private static decimal ParseDecimal(string value)
    {
        var cleaned = value.Replace("RD$", "", StringComparison.OrdinalIgnoreCase).Replace("$", "").Trim();
        foreach (var culture in new[] { CultureInfo.GetCultureInfo("es-DO"), CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("en-US") })
            if (decimal.TryParse(cleaned, NumberStyles.Any, culture, out var parsed)) return parsed;
        return 0;
    }

    private static DateTime? ParseDate(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("es-DO"), DateTimeStyles.None, out var date)) return date;
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial) && serial > 0) return DateTime.FromOADate(serial);
        return null;
    }

    private static bool ParseBool(string value)
    {
        var normalized = CollectionEngine.Normalize(value);
        return normalized is "si" or "sí" or "yes" or "true" or "1" or "autorizado";
    }

    private static List<List<string>> ReadCsv(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var separator = lines.FirstOrDefault()?.Count(c => c == ';') > lines.FirstOrDefault()?.Count(c => c == ',') ? ';' : ',';
        return lines.Select(line => ParseCsvLine(line, separator)).ToList();
    }

    private static List<string> ParseCsvLine(string line, char separator)
    {
        var result = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
            else if (c == '"') quoted = !quoted;
            else if (c == separator && !quoted) { result.Add(value.ToString()); value.Clear(); }
            else value.Append(c);
        }
        result.Add(value.ToString());
        return result;
    }

    private static List<List<string>> ReadXlsx(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var strings = new List<string>();
        var shared = archive.GetEntry("xl/sharedStrings.xml");
        if (shared != null)
        {
            using var stream = shared.Open();
            var document = XDocument.Load(stream);
            strings = document.Descendants().Where(x => x.Name.LocalName == "si")
                .Select(si => string.Concat(si.Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value))).ToList();
        }
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml") ?? throw new InvalidDataException("No se encontró la primera hoja del Excel.");
        using var sheetStream = sheet.Open();
        var sheetDocument = XDocument.Load(sheetStream);
        var output = new List<List<string>>();
        foreach (var row in sheetDocument.Descendants().Where(x => x.Name.LocalName == "row"))
        {
            var values = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements().Where(x => x.Name.LocalName == "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? "A1";
                var index = ColumnIndex(reference);
                var type = cell.Attribute("t")?.Value;
                var raw = cell.Elements().FirstOrDefault(x => x.Name.LocalName == "v")?.Value
                          ?? string.Concat(cell.Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value));
                if (type == "s" && int.TryParse(raw, out var sharedIndex) && sharedIndex < strings.Count) raw = strings[sharedIndex];
                values[index] = raw;
            }
            var max = values.Count == 0 ? -1 : values.Keys.Max();
            output.Add(Enumerable.Range(0, max + 1).Select(i => values.GetValueOrDefault(i, "")).ToList());
        }
        return output;
    }

    private static int ColumnIndex(string reference)
    {
        var result = 0;
        foreach (var c in reference.TakeWhile(char.IsLetter)) result = result * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        return Math.Max(0, result - 1);
    }
}
