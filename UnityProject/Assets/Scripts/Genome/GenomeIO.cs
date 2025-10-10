using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace WormWorld.Genome
{
    /// <summary>
    /// IO helpers and schema validation hooks for genome interchange formats.
    /// </summary>
    public static class GenomeIO
    {
        private static readonly string[] CsvHeaders =
        {
            "version",
            "id",
            "name",
            "seed",
            "metadata_json",
            "body_json",
            "brain_json",
            "senses_json",
            "reproduction_json",
            "muscles_json",
            "pheromones_json",
            "nerves_json",
            "energy_json",
            "fitness_json"
        };

        private static readonly object SchemaLock = new object();
        private static readonly Dictionary<string, SimpleJsonSchemaValidator> ValidatorCache = new Dictionary<string, SimpleJsonSchemaValidator>(StringComparer.OrdinalIgnoreCase);
        private static string _schemaPathOverride;

        /// <summary>
        /// Gets or sets the filesystem path to the JSON schema used for validation.
        /// When not explicitly set, the accessor searches upward from the current working directory
        /// for <c>Data/schemas/genome.schema.json</c> or falls back to the <c>WORMWORLD_GENOME_SCHEMA</c> environment variable.
        /// </summary>
        public static string SchemaPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_schemaPathOverride))
                {
                    return _schemaPathOverride;
                }

                var environmentOverride = Environment.GetEnvironmentVariable("WORMWORLD_GENOME_SCHEMA");
                if (!string.IsNullOrEmpty(environmentOverride) && File.Exists(environmentOverride))
                {
                    _schemaPathOverride = environmentOverride;
                    return _schemaPathOverride;
                }

                var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (currentDirectory != null)
                {
                    var candidate = Path.Combine(currentDirectory.FullName, "Data", "schemas", "genome.schema.json");
                    if (File.Exists(candidate))
                    {
                        _schemaPathOverride = candidate;
                        return _schemaPathOverride;
                    }

                    currentDirectory = currentDirectory.Parent;
                }

                throw new FileNotFoundException("Unable to locate genome schema. Set GenomeIO.SchemaPath or WORMWORLD_GENOME_SCHEMA.");
            }
            set
            {
                lock (SchemaLock)
                {
                    _schemaPathOverride = value;
                }
            }
        }

        /// <summary>
        /// Reads genomes from the canonical CSV format into strongly typed objects.
        /// </summary>
        /// <param name="csvPath">Filesystem path to the CSV.</param>
        /// <returns>List of genomes described by the CSV rows.</returns>
        public static IList<Genome> ReadFromCsv(string csvPath)
        {
            if (csvPath == null)
            {
                throw new ArgumentNullException(nameof(csvPath));
            }

            var genomes = new List<Genome>();
            using (var reader = new StreamReader(File.OpenRead(csvPath)))
            {
                string? headerLine = reader.ReadLine();
                if (headerLine == null)
                {
                    return genomes;
                }

                var headers = ParseCsvLine(headerLine);
                if (headers.Count != CsvHeaders.Length || !headers.SequenceEqual(CsvHeaders))
                {
                    throw new InvalidDataException("CSV header does not match v0 genome contract.");
                }

                var rowIndex = 1; // header consumed
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    rowIndex++;

                    if (line == null || string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var fields = ParseCsvLine(line);
                        if (fields.Count != headers.Count)
                        {
                            throw new InvalidDataException($"Row {rowIndex} has {fields.Count} fields but expected {headers.Count}.");
                        }

                        var genome = CreateGenomeFromRow(headers, fields);
                        genomes.Add(genome);
                    }
                    catch (Exception ex) when (ex is InvalidDataException || ex is JsonException || ex is FormatException || ex is OverflowException)
                    {
                        throw new InvalidDataException($"Failed to parse genome CSV row {rowIndex}: {ex.Message}", ex);
                    }
                }
            }

            return genomes;
        }

        /// <summary>
        /// Writes genomes to a JSONL file in expanded form.
        /// </summary>
        /// <param name="jsonlPath">Filesystem path to create or overwrite.</param>
        /// <param name="genomes">Sequence of genomes to serialize.</param>
        public static void WriteToJsonl(string jsonlPath, IEnumerable<Genome> genomes)
        {
            if (jsonlPath == null)
            {
                throw new ArgumentNullException(nameof(jsonlPath));
            }

            if (genomes == null)
            {
                throw new ArgumentNullException(nameof(genomes));
            }

            var directory = Path.GetDirectoryName(jsonlPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(jsonlPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            foreach (var genome in genomes)
            {
                var canonical = ToCanonicalJson(genome);
                writer.WriteLine(canonical);
            }
        }

        /// <summary>
        /// Reads genomes from an expanded JSONL file.
        /// </summary>
        /// <param name="jsonlPath">Filesystem path to the JSONL payload.</param>
        /// <returns>Materialized genomes.</returns>
        public static IList<Genome> ReadFromJsonl(string jsonlPath)
        {
            if (jsonlPath == null)
            {
                throw new ArgumentNullException(nameof(jsonlPath));
            }

            var genomes = new List<Genome>();
            var lineNumber = 0;
            foreach (var rawLine in File.ReadLines(jsonlPath))
            {
                lineNumber++;
                var line = rawLine?.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var genome = new Genome
                    {
                        Version = root.TryGetProperty("version", out var versionElement) ? versionElement.GetString() ?? string.Empty : string.Empty,
                        Id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty,
                        Name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty,
                        Seed = root.TryGetProperty("seed", out var seedElement) ? seedElement.GetUInt64() : 0UL,
                        MetadataJson = Canonicalize(root.GetProperty("metadata")),
                        BodyJson = Canonicalize(root.GetProperty("body")),
                        BrainJson = Canonicalize(root.GetProperty("brain")),
                        SensesJson = Canonicalize(root.GetProperty("senses")),
                        ReproductionJson = Canonicalize(root.GetProperty("reproduction")),
                        MusclesJson = Canonicalize(root.GetProperty("muscles")),
                        PheromonesJson = Canonicalize(root.GetProperty("pheromone_pairs")),
                        NervesJson = Canonicalize(root.GetProperty("nerves")),
                        EnergyJson = Canonicalize(root.GetProperty("energy")),
                        FitnessJson = Canonicalize(root.GetProperty("fitness_weights"))
                    };

                    genomes.Add(genome);
                }
                catch (Exception ex) when (ex is JsonException || ex is InvalidDataException || ex is KeyNotFoundException)
                {
                    throw new InvalidDataException($"Failed to parse JSONL line {lineNumber}: {ex.Message}", ex);
                }
            }

            return genomes;
        }

        /// <summary>
        /// Writes genomes back to the canonical CSV representation.
        /// </summary>
        /// <param name="csvPath">Destination CSV path.</param>
        /// <param name="genomes">Genomes to serialize.</param>
        public static void WriteToCsv(string csvPath, IEnumerable<Genome> genomes)
        {
            if (csvPath == null)
            {
                throw new ArgumentNullException(nameof(csvPath));
            }

            if (genomes == null)
            {
                throw new ArgumentNullException(nameof(genomes));
            }

            var directory = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            writer.WriteLine(string.Join(",", CsvHeaders));

            foreach (var genome in genomes)
            {
                var metadata = CanonicalizeJsonString(genome.MetadataJson, "metadata_json");
                var body = CanonicalizeJsonString(genome.BodyJson, "body_json");
                var brain = CanonicalizeJsonString(genome.BrainJson, "brain_json");
                var senses = CanonicalizeJsonString(genome.SensesJson, "senses_json");
                var reproduction = CanonicalizeJsonString(genome.ReproductionJson, "reproduction_json");
                var muscles = CanonicalizeJsonString(genome.MusclesJson, "muscles_json");
                var pheromones = CanonicalizeJsonString(genome.PheromonesJson, "pheromones_json");
                var nerves = CanonicalizeJsonString(genome.NervesJson, "nerves_json");
                var energy = CanonicalizeJsonString(genome.EnergyJson, "energy_json");
                var fitness = CanonicalizeJsonString(genome.FitnessJson, "fitness_json");

                genome.MetadataJson = metadata;
                genome.BodyJson = body;
                genome.BrainJson = brain;
                genome.SensesJson = senses;
                genome.ReproductionJson = reproduction;
                genome.MusclesJson = muscles;
                genome.PheromonesJson = pheromones;
                genome.NervesJson = nerves;
                genome.EnergyJson = energy;
                genome.FitnessJson = fitness;

                var fields = new[]
                {
                    genome.Version ?? "v0",
                    genome.Id ?? string.Empty,
                    genome.Name ?? string.Empty,
                    genome.Seed.ToString(CultureInfo.InvariantCulture),
                    metadata,
                    body,
                    brain,
                    senses,
                    reproduction,
                    muscles,
                    pheromones,
                    nerves,
                    energy,
                    fitness
                };

                writer.WriteLine(string.Join(",", fields.Select(EscapeCsvField)));
            }
        }

        /// <summary>
        /// Validates a genome against the JSON Schema definition.
        /// </summary>
        /// <param name="genome">Genome to validate.</param>
        public static void ValidateWithSchema(Genome genome)
        {
            if (genome == null)
            {
                throw new ArgumentNullException(nameof(genome));
            }

            var schemaPath = SchemaPath;
            var validator = GetValidator(schemaPath);
            using var document = JsonDocument.Parse(ToCanonicalJson(genome));
            var errors = validator.Validate(document.RootElement);
            if (errors.Count > 0)
            {
                throw new SchemaValidationException(schemaPath, errors);
            }
        }

        /// <summary>
        /// Creates the canonical expanded JSON string for a genome.
        /// </summary>
        /// <param name="genome">Genome to export.</param>
        /// <returns>Canonical expanded JSON string.</returns>
        public static string ToCanonicalJson(Genome genome)
        {
            if (genome == null)
            {
                throw new ArgumentNullException(nameof(genome));
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteString("version", genome.Version ?? "v0");
                writer.WriteString("id", genome.Id ?? string.Empty);
                writer.WriteString("name", genome.Name ?? string.Empty);
                writer.WritePropertyName("seed");
                writer.WriteNumberValue(genome.Seed);

                WriteJsonProperty(writer, "metadata", genome.MetadataJson, "metadata_json");
                WriteJsonProperty(writer, "body", genome.BodyJson, "body_json");
                WriteJsonProperty(writer, "brain", genome.BrainJson, "brain_json");
                WriteJsonProperty(writer, "senses", genome.SensesJson, "senses_json");
                WriteJsonProperty(writer, "reproduction", genome.ReproductionJson, "reproduction_json");
                WriteJsonProperty(writer, "muscles", genome.MusclesJson, "muscles_json");
                WriteJsonProperty(writer, "pheromone_pairs", genome.PheromonesJson, "pheromones_json");
                WriteJsonProperty(writer, "nerves", genome.NervesJson, "nerves_json");
                WriteJsonProperty(writer, "energy", genome.EnergyJson, "energy_json");
                WriteJsonProperty(writer, "fitness_weights", genome.FitnessJson, "fitness_json");

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static Genome CreateGenomeFromRow(IReadOnlyList<string> headers, IReadOnlyList<string> fields)
        {
            var genome = new Genome();
            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                var value = fields[i];
                switch (header)
                {
                    case "version":
                        genome.Version = value;
                        break;
                    case "id":
                        genome.Id = value;
                        break;
                    case "name":
                        genome.Name = value;
                        break;
                    case "seed":
                        genome.Seed = string.IsNullOrWhiteSpace(value)
                            ? 0UL
                            : ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                        break;
                    case "metadata_json":
                        genome.MetadataJson = CanonicalizeJsonString(value, header);
                        break;
                    case "body_json":
                        genome.BodyJson = CanonicalizeJsonString(value, header);
                        break;
                    case "brain_json":
                        genome.BrainJson = CanonicalizeJsonString(value, header);
                        break;
                    case "senses_json":
                        genome.SensesJson = CanonicalizeJsonString(value, header);
                        break;
                    case "reproduction_json":
                        genome.ReproductionJson = CanonicalizeJsonString(value, header);
                        break;
                    case "muscles_json":
                        genome.MusclesJson = CanonicalizeJsonString(value, header);
                        break;
                    case "pheromones_json":
                        genome.PheromonesJson = CanonicalizeJsonString(value, header);
                        break;
                    case "nerves_json":
                        genome.NervesJson = CanonicalizeJsonString(value, header);
                        break;
                    case "energy_json":
                        genome.EnergyJson = CanonicalizeJsonString(value, header);
                        break;
                    case "fitness_json":
                        genome.FitnessJson = CanonicalizeJsonString(value, header);
                        break;
                }
            }

            return genome;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var builder = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var character = line[i];

                if (inQuotes)
                {
                    if (character == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            builder.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        builder.Append(character);
                    }
                }
                else
                {
                    if (character == ',')
                    {
                        fields.Add(builder.ToString());
                        builder.Clear();
                    }
                    else if (character == '"')
                    {
                        inQuotes = true;
                    }
                    else if (character != '\r' && character != '\n')
                    {
                        builder.Append(character);
                    }
                }
            }

            if (inQuotes)
            {
                throw new InvalidDataException("Unterminated quoted value in CSV line.");
            }

            fields.Add(builder.ToString());
            return fields;
        }

        private static string EscapeCsvField(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var requiresQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!requiresQuotes)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (var character in value)
            {
                if (character == '"')
                {
                    builder.Append('"');
                }

                builder.Append(character);
            }

            builder.Append('"');
            return builder.ToString();
        }

        private static void WriteJsonProperty(Utf8JsonWriter writer, string propertyName, string json, string columnName)
        {
            writer.WritePropertyName(propertyName);
            using var document = ParseJsonDocument(json, columnName);
            WriteCanonicalElement(document.RootElement, writer);
        }

        private static JsonDocument ParseJsonDocument(string json, string columnName)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException($"Column {columnName} must contain JSON payload.");
            }

            try
            {
                return JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Column {columnName} contains invalid JSON: {ex.Message}", ex);
            }
        }

        private static string CanonicalizeJsonString(string json, string columnName)
        {
            using var document = ParseJsonDocument(json, columnName);
            return Canonicalize(document.RootElement);
        }

        private static string Canonicalize(JsonElement element)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                WriteCanonicalElement(element, writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteCanonicalElement(JsonElement element, Utf8JsonWriter writer)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(property.Name);
                        WriteCanonicalElement(property.Value, writer);
                    }

                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                    {
                        WriteCanonicalElement(item, writer);
                    }

                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var intValue))
                    {
                        writer.WriteNumberValue(intValue);
                    }
                    else if (element.TryGetUInt64(out var unsignedValue))
                    {
                        writer.WriteNumberValue(unsignedValue);
                    }
                    else if (element.TryGetDecimal(out var decimalValue))
                    {
                        writer.WriteNumberValue(decimalValue);
                    }
                    else
                    {
                        writer.WriteNumberValue(element.GetDouble());
                    }

                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                    writer.WriteNullValue();
                    break;
                default:
                    throw new NotSupportedException($"Unsupported JSON value kind: {element.ValueKind}.");
            }
        }

        private static SimpleJsonSchemaValidator GetValidator(string schemaPath)
        {
            lock (SchemaLock)
            {
                if (ValidatorCache.TryGetValue(schemaPath, out var cached))
                {
                    return cached;
                }

                var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
                var validator = new SimpleJsonSchemaValidator(document);
                ValidatorCache[schemaPath] = validator;
                return validator;
            }
        }

        private sealed class SimpleJsonSchemaValidator
        {
            private readonly JsonDocument _schemaDocument;

            public SimpleJsonSchemaValidator(JsonDocument schemaDocument)
            {
                _schemaDocument = schemaDocument ?? throw new ArgumentNullException(nameof(schemaDocument));
            }

            public IReadOnlyList<string> Validate(JsonElement instance)
            {
                var errors = new List<string>();
                ValidateElement(_schemaDocument.RootElement, instance, "$", errors);
                return errors;
            }

            private void ValidateElement(JsonElement schema, JsonElement instance, string path, List<string> errors)
            {
                if (schema.TryGetProperty("$ref", out var refElement))
                {
                    var resolved = ResolveReference(refElement.GetString() ?? string.Empty);
                    ValidateElement(resolved, instance, path, errors);
                    return;
                }

                if (schema.TryGetProperty("type", out var typeElement))
                {
                    if (!IsTypeAllowed(typeElement, instance))
                    {
                        errors.Add($"{path}: expected type {DescribeType(typeElement)} but found {DescribeInstance(instance)}");
                        return;
                    }
                }

                if (schema.TryGetProperty("const", out var constElement))
                {
                    if (!JsonEquals(constElement, instance))
                    {
                        errors.Add($"{path}: expected constant {constElement.GetRawText()} but found {instance.GetRawText()}");
                    }
                }

                if (schema.TryGetProperty("enum", out var enumElement) && enumElement.ValueKind == JsonValueKind.Array)
                {
                    var isMatch = enumElement.EnumerateArray().Any(option => JsonEquals(option, instance));
                    if (!isMatch)
                    {
                        errors.Add($"{path}: value {instance.GetRawText()} is not permitted");
                    }
                }

                switch (instance.ValueKind)
                {
                    case JsonValueKind.Object:
                        ValidateObject(schema, instance, path, errors);
                        break;
                    case JsonValueKind.Array:
                        ValidateArray(schema, instance, path, errors);
                        break;
                    case JsonValueKind.String:
                        ValidateString(schema, instance, path, errors);
                        break;
                    case JsonValueKind.Number:
                        ValidateNumber(schema, instance, path, errors);
                        break;
                }
            }

            private void ValidateObject(JsonElement schema, JsonElement instance, string path, List<string> errors)
            {
                if (schema.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
                {
                    var required = requiredElement.EnumerateArray().Select(p => p.GetString()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                    foreach (var property in required)
                    {
                        if (!instance.TryGetProperty(property!, out _))
                        {
                            errors.Add($"{path}: required property '{property}' is missing");
                        }
                    }
                }

                Dictionary<string, JsonElement>? propertySchemas = null;
                if (schema.TryGetProperty("properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Object)
                {
                    propertySchemas = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (var property in propertiesElement.EnumerateObject())
                    {
                        propertySchemas[property.Name] = property.Value;
                    }
                }

                var hasAdditionalSchema = false;
                var additionalAllowed = true;
                JsonElement additionalSchema = default;
                if (schema.TryGetProperty("additionalProperties", out var additionalElement))
                {
                    if (additionalElement.ValueKind == JsonValueKind.False)
                    {
                        additionalAllowed = false;
                    }
                    else if (additionalElement.ValueKind == JsonValueKind.Object)
                    {
                        additionalAllowed = true;
                        hasAdditionalSchema = true;
                        additionalSchema = additionalElement;
                    }
                }

                foreach (var property in instance.EnumerateObject())
                {
                    var childPath = path == "$" ? "$" + "." + property.Name : path + "." + property.Name;
                    if (propertySchemas != null && propertySchemas.TryGetValue(property.Name, out var propertySchema))
                    {
                        ValidateElement(propertySchema, property.Value, childPath, errors);
                    }
                    else if (!additionalAllowed)
                    {
                        errors.Add($"{childPath}: additional properties are not allowed");
                    }
                    else if (hasAdditionalSchema)
                    {
                        ValidateElement(additionalSchema, property.Value, childPath, errors);
                    }
                }
            }

            private void ValidateArray(JsonElement schema, JsonElement instance, string path, List<string> errors)
            {
                if (schema.TryGetProperty("minItems", out var minItemsElement))
                {
                    var minItems = minItemsElement.GetInt32();
                    if (instance.GetArrayLength() < minItems)
                    {
                        errors.Add($"{path}: expected at least {minItems} items but found {instance.GetArrayLength()}");
                    }
                }

                if (schema.TryGetProperty("maxItems", out var maxItemsElement))
                {
                    var maxItems = maxItemsElement.GetInt32();
                    if (instance.GetArrayLength() > maxItems)
                    {
                        errors.Add($"{path}: expected at most {maxItems} items but found {instance.GetArrayLength()}");
                    }
                }

                if (schema.TryGetProperty("items", out var itemsSchema))
                {
                    var index = 0;
                    foreach (var item in instance.EnumerateArray())
                    {
                        ValidateElement(itemsSchema, item, $"{path}[{index}]", errors);
                        index++;
                    }
                }
            }

            private void ValidateString(JsonElement schema, JsonElement instance, string path, List<string> errors)
            {
                var value = instance.GetString() ?? string.Empty;
                if (schema.TryGetProperty("minLength", out var minLengthElement))
                {
                    var minLength = minLengthElement.GetInt32();
                    if (value.Length < minLength)
                    {
                        errors.Add($"{path}: string shorter than minimum length {minLength}");
                    }
                }

                if (schema.TryGetProperty("maxLength", out var maxLengthElement))
                {
                    var maxLength = maxLengthElement.GetInt32();
                    if (value.Length > maxLength)
                    {
                        errors.Add($"{path}: string longer than maximum length {maxLength}");
                    }
                }
            }

            private void ValidateNumber(JsonElement schema, JsonElement instance, string path, List<string> errors)
            {
                var value = GetDecimalValue(instance);
                if (schema.TryGetProperty("minimum", out var minimumElement))
                {
                    var minimum = GetDecimalValue(minimumElement);
                    if (value < minimum)
                    {
                        errors.Add($"{path}: value {value} is less than minimum {minimum}");
                    }
                }

                if (schema.TryGetProperty("maximum", out var maximumElement))
                {
                    var maximum = GetDecimalValue(maximumElement);
                    if (value > maximum)
                    {
                        errors.Add($"{path}: value {value} exceeds maximum {maximum}");
                    }
                }
            }

            private decimal GetDecimalValue(JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.Number)
                {
                    return 0m;
                }

                if (element.TryGetDecimal(out var dec))
                {
                    return dec;
                }

                if (element.TryGetInt64(out var longValue))
                {
                    return longValue;
                }

                if (element.TryGetUInt64(out var ulongValue))
                {
                    return ulongValue;
                }

                return Convert.ToDecimal(element.GetDouble(), CultureInfo.InvariantCulture);
            }

            private bool IsTypeAllowed(JsonElement typeDefinition, JsonElement instance)
            {
                if (typeDefinition.ValueKind == JsonValueKind.String)
                {
                    return MatchesType(typeDefinition.GetString(), instance);
                }

                if (typeDefinition.ValueKind == JsonValueKind.Array)
                {
                    foreach (var option in typeDefinition.EnumerateArray())
                    {
                        if (option.ValueKind == JsonValueKind.String && MatchesType(option.GetString(), instance))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                return true;
            }

            private bool MatchesType(string? type, JsonElement instance)
            {
                switch (type)
                {
                    case "object":
                        return instance.ValueKind == JsonValueKind.Object;
                    case "array":
                        return instance.ValueKind == JsonValueKind.Array;
                    case "string":
                        return instance.ValueKind == JsonValueKind.String;
                    case "number":
                        return instance.ValueKind == JsonValueKind.Number;
                    case "integer":
                        return instance.ValueKind == JsonValueKind.Number && IsInteger(instance);
                    case "boolean":
                        return instance.ValueKind == JsonValueKind.True || instance.ValueKind == JsonValueKind.False;
                    case "null":
                        return instance.ValueKind == JsonValueKind.Null;
                    default:
                        return true;
                }
            }

            private bool IsInteger(JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.Number)
                {
                    return false;
                }

                if (element.TryGetInt64(out _))
                {
                    return true;
                }

                if (element.TryGetUInt64(out _))
                {
                    return true;
                }

                if (element.TryGetDecimal(out var dec))
                {
                    return decimal.Truncate(dec) == dec;
                }

                return false;
            }

            private string DescribeType(JsonElement typeDefinition)
            {
                if (typeDefinition.ValueKind == JsonValueKind.String)
                {
                    return typeDefinition.GetString() ?? "unknown";
                }

                if (typeDefinition.ValueKind == JsonValueKind.Array)
                {
                    var values = typeDefinition.EnumerateArray()
                        .Where(element => element.ValueKind == JsonValueKind.String)
                        .Select(element => element.GetString())
                        .Where(value => !string.IsNullOrEmpty(value));
                    return string.Join(" | ", values);
                }

                return "unspecified";
            }

            private string DescribeInstance(JsonElement instance)
            {
                switch (instance.ValueKind)
                {
                    case JsonValueKind.Object:
                        return "object";
                    case JsonValueKind.Array:
                        return "array";
                    case JsonValueKind.String:
                        return "string";
                    case JsonValueKind.Number:
                        return "number";
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return "boolean";
                    case JsonValueKind.Null:
                        return "null";
                    default:
                        return instance.ValueKind.ToString();
                }
            }

            private bool JsonEquals(JsonElement left, JsonElement right)
            {
                return Canonicalize(left) == Canonicalize(right);
            }

            private JsonElement ResolveReference(string reference)
            {
                if (string.IsNullOrEmpty(reference) || reference == "#")
                {
                    return _schemaDocument.RootElement;
                }

                if (!reference.StartsWith("#", StringComparison.Ordinal))
                {
                    throw new NotSupportedException($"Only local schema references are supported. Encountered '{reference}'.");
                }

                var pointer = reference.Substring(1); // Trim leading '#'
                var segments = pointer.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var current = _schemaDocument.RootElement;
                foreach (var rawSegment in segments)
                {
                    var segment = rawSegment.Replace("~1", "/").Replace("~0", "~");
                    if (current.ValueKind == JsonValueKind.Object)
                    {
                        if (!current.TryGetProperty(segment, out current))
                        {
                            throw new InvalidDataException($"Schema reference '{reference}' could not be resolved.");
                        }
                    }
                    else if (current.ValueKind == JsonValueKind.Array)
                    {
                        if (!int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                        {
                            throw new InvalidDataException($"Schema reference '{reference}' targets non-object value.");
                        }

                        var array = current.EnumerateArray().ToList();
                        if (index < 0 || index >= array.Count)
                        {
                            throw new InvalidDataException($"Schema reference '{reference}' is out of bounds.");
                        }

                        current = array[index];
                    }
                    else
                    {
                        throw new InvalidDataException($"Schema reference '{reference}' targets non-container value.");
                    }
                }

                return current;
            }
        }

        /// <summary>
        /// Exception thrown when JSON schema validation fails.
        /// </summary>
        public sealed class SchemaValidationException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SchemaValidationException"/> class.
            /// </summary>
            /// <param name="schemaPath">Path to the schema used for validation.</param>
            /// <param name="errors">Collection of validation error messages.</param>
            public SchemaValidationException(string schemaPath, IReadOnlyList<string> errors)
                : base(CreateMessage(schemaPath, errors))
            {
                SchemaPath = schemaPath;
                Errors = new ReadOnlyCollection<string>(errors.ToArray());
            }

            /// <summary>
            /// Gets the schema path that triggered the failure.
            /// </summary>
            public string SchemaPath { get; }

            /// <summary>
            /// Gets the validation errors discovered during schema evaluation.
            /// </summary>
            public IReadOnlyList<string> Errors { get; }

            private static string CreateMessage(string schemaPath, IReadOnlyList<string> errors)
            {
                var builder = new StringBuilder();
                builder.Append("Schema validation failed for ");
                builder.Append(schemaPath);
                builder.Append(':');
                foreach (var error in errors)
                {
                    builder.Append(' ');
                    builder.Append(error);
                }

                return builder.ToString();
            }
        }
    }
}
