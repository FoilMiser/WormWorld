using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            "fitness_json",
            "pre_eval_fitness"
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
                string headerLine = reader.ReadLine();
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
                    catch (Exception ex) when (ex is InvalidDataException || ex is JsonReaderException || ex is FormatException || ex is OverflowException)
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
                    var root = JObject.Parse(line);
                    var genome = new Genome
                    {
                        Version = root.Value<string>("version") ?? string.Empty,
                        Id = root.Value<string>("id") ?? string.Empty,
                        Name = root.Value<string>("name") ?? string.Empty,
                        Seed = root.Value<ulong?>("seed") ?? 0UL,
                        MetadataJson = CanonicalizeToken(root["metadata"], "metadata"),
                        BodyJson = CanonicalizeToken(root["body"], "body"),
                        BrainJson = CanonicalizeToken(root["brain"], "brain"),
                        SensesJson = CanonicalizeToken(root["senses"], "senses"),
                        ReproductionJson = CanonicalizeToken(root["reproduction"], "reproduction"),
                        MusclesJson = CanonicalizeToken(root["muscles"], "muscles"),
                        PheromonesJson = CanonicalizeToken(root["pheromone_pairs"], "pheromone_pairs"),
                        NervesJson = CanonicalizeToken(root["nerves"], "nerves"),
                        EnergyJson = CanonicalizeToken(root["energy"], "energy"),
                        FitnessJson = CanonicalizeToken(root["fitness_weights"], "fitness_weights"),
                        PreEvalFitness = root.TryGetValue("pre_eval_fitness", StringComparison.Ordinal, out var preEvalToken)
                            ? (preEvalToken.Type == JTokenType.Null ? (double?)null : preEvalToken.Value<double>())
                            : null
                    };

                    genomes.Add(genome);
                }
                catch (Exception ex) when (ex is JsonReaderException || ex is InvalidDataException || ex is KeyNotFoundException)
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
                    fitness,
                    genome.PreEvalFitness.HasValue
                        ? genome.PreEvalFitness.Value.ToString(CultureInfo.InvariantCulture)
                        : string.Empty
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
            var canonical = ToCanonicalJson(genome);
            var token = JToken.Parse(canonical);
            var errors = validator.Validate(token);
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

            var root = new JObject
            {
                ["version"] = genome.Version ?? "v0",
                ["id"] = genome.Id ?? string.Empty,
                ["name"] = genome.Name ?? string.Empty,
                ["seed"] = genome.Seed
            };

            root["metadata"] = ParseSection(genome.MetadataJson, "metadata_json");
            root["body"] = ParseSection(genome.BodyJson, "body_json");
            root["brain"] = ParseSection(genome.BrainJson, "brain_json");
            root["senses"] = ParseSection(genome.SensesJson, "senses_json");
            root["reproduction"] = ParseSection(genome.ReproductionJson, "reproduction_json");
            root["muscles"] = ParseSection(genome.MusclesJson, "muscles_json");
            root["pheromone_pairs"] = ParseSection(genome.PheromonesJson, "pheromones_json");
            root["nerves"] = ParseSection(genome.NervesJson, "nerves_json");
            root["energy"] = ParseSection(genome.EnergyJson, "energy_json");
            root["fitness_weights"] = ParseSection(genome.FitnessJson, "fitness_json");

            if (genome.PreEvalFitness.HasValue)
            {
                root["pre_eval_fitness"] = genome.PreEvalFitness.Value;
            }

            return JsonCompat.Normalize(root.ToString(Formatting.None));
        }

        /// <summary>
        /// Canonicalizes an inline JSON fragment using the genome canonical ordering rules.
        /// </summary>
        /// <param name="json">JSON text to normalize.</param>
        /// <returns>Canonical JSON string.</returns>
        public static string CanonicalizeSection(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            return CanonicalizeJsonString(json, "inline_fragment");
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
                    case "pre_eval_fitness":
                        genome.PreEvalFitness = string.IsNullOrWhiteSpace(value)
                            ? (double?)null
                            : double.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
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

        private static string CanonicalizeJsonString(string json, string columnName)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException($"Column {columnName} must contain JSON payload.");
            }

            try
            {
                return JsonCompat.Normalize(json);
            }
            catch (JsonReaderException ex)
            {
                throw new InvalidDataException($"Column {columnName} contains invalid JSON: {ex.Message}", ex);
            }
        }

        private static string CanonicalizeToken(JToken token, string propertyName)
        {
            if (token == null)
            {
                throw new KeyNotFoundException($"Property '{propertyName}' is missing.");
            }

            return JsonCompat.Normalize(token.ToString(Formatting.None));
        }

        private static JToken ParseSection(string json, string columnName)
        {
            var normalized = CanonicalizeJsonString(json, columnName);
            return JToken.Parse(normalized);
        }

        private static SimpleJsonSchemaValidator GetValidator(string schemaPath)
        {
            lock (SchemaLock)
            {
                if (ValidatorCache.TryGetValue(schemaPath, out var cached))
                {
                    return cached;
                }

                var document = JToken.Parse(File.ReadAllText(schemaPath));
                var validator = new SimpleJsonSchemaValidator(document);
                ValidatorCache[schemaPath] = validator;
                return validator;
            }
        }

        private sealed class SimpleJsonSchemaValidator
        {
            private readonly JToken _schemaDocument;

            public SimpleJsonSchemaValidator(JToken schemaDocument)
            {
                _schemaDocument = schemaDocument ?? throw new ArgumentNullException(nameof(schemaDocument));
            }

            public IReadOnlyList<string> Validate(JToken instance)
            {
                var errors = new List<string>();
                ValidateElement(_schemaDocument, instance, "$", errors);
                return errors;
            }

            private void ValidateElement(JToken schema, JToken instance, string path, List<string> errors)
            {
                if (schema is JObject schemaObject)
                {
                    if (schemaObject.TryGetValue("$ref", out var refToken))
                    {
                        var resolved = ResolveReference(refToken?.Value<string>() ?? string.Empty);
                        ValidateElement(resolved, instance, path, errors);
                        return;
                    }

                    if (schemaObject.TryGetValue("type", out var typeDefinition))
                    {
                        if (!IsTypeAllowed(typeDefinition, instance))
                        {
                            errors.Add($"{path}: expected type {DescribeType(typeDefinition)} but found {DescribeInstance(instance)}");
                            return;
                        }
                    }

                    if (schemaObject.TryGetValue("const", out var constToken))
                    {
                        if (!JsonEquals(constToken, instance))
                        {
                            errors.Add($"{path}: expected constant {constToken.ToString(Formatting.None)} but found {instance.ToString(Formatting.None)}");
                        }
                    }

                    if (schemaObject.TryGetValue("enum", out var enumToken) && enumToken is JArray enumArray)
                    {
                        var isMatch = enumArray.Any(option => JsonEquals(option, instance));
                        if (!isMatch)
                        {
                            errors.Add($"{path}: value {instance.ToString(Formatting.None)} is not permitted");
                        }
                    }

                    switch (instance.Type)
                    {
                        case JTokenType.Object:
                            ValidateObject(schemaObject, (JObject)instance, path, errors);
                            break;
                        case JTokenType.Array:
                            ValidateArray(schemaObject, (JArray)instance, path, errors);
                            break;
                        case JTokenType.String:
                            ValidateString(schemaObject, instance, path, errors);
                            break;
                        case JTokenType.Integer:
                        case JTokenType.Float:
                            ValidateNumber(schemaObject, instance, path, errors);
                            break;
                    }
                }
            }

            private void ValidateObject(JObject schema, JObject instance, string path, List<string> errors)
            {
                if (schema.TryGetValue("required", out var requiredToken) && requiredToken is JArray requiredArray)
                {
                    var required = requiredArray.Values<string?>().Where(v => !string.IsNullOrEmpty(v)).ToArray();
                    foreach (var property in required)
                    {
                        if (property != null && instance.Property(property) == null)
                        {
                            errors.Add($"{path}: required property '{property}' is missing");
                        }
                    }
                }

                Dictionary<string, JToken> propertySchemas = null;
                if (schema.TryGetValue("properties", out var propertiesToken) && propertiesToken is JObject propertiesObject)
                {
                    propertySchemas = new Dictionary<string, JToken>(StringComparer.Ordinal);
                    foreach (var property in propertiesObject.Properties())
                    {
                        propertySchemas[property.Name] = property.Value;
                    }
                }

                var additionalAllowed = true;
                JToken additionalSchema = null;
                if (schema.TryGetValue("additionalProperties", out var additionalToken))
                {
                    if (additionalToken.Type == JTokenType.Boolean)
                    {
                        additionalAllowed = additionalToken.Value<bool>();
                    }
                    else
                    {
                        additionalSchema = additionalToken;
                    }
                }

                foreach (var property in instance.Properties())
                {
                    var propertyPath = path + "." + property.Name;
                    if (propertySchemas != null && propertySchemas.TryGetValue(property.Name, out var propertySchema))
                    {
                        ValidateElement(propertySchema, property.Value, propertyPath, errors);
                    }
                    else if (additionalSchema != null)
                    {
                        ValidateElement(additionalSchema, property.Value, propertyPath, errors);
                    }
                    else if (!additionalAllowed)
                    {
                        errors.Add($"{propertyPath}: additional properties are not permitted");
                    }
                }
            }

            private void ValidateArray(JObject schema, JArray instance, string path, List<string> errors)
            {
                if (schema.TryGetValue("minItems", out var minItemsToken))
                {
                    var minItems = minItemsToken.Value<int>();
                    if (instance.Count < minItems)
                    {
                        errors.Add($"{path}: array has {instance.Count} items but minimum is {minItems}");
                    }
                }

                if (schema.TryGetValue("maxItems", out var maxItemsToken))
                {
                    var maxItems = maxItemsToken.Value<int>();
                    if (instance.Count > maxItems)
                    {
                        errors.Add($"{path}: array has {instance.Count} items but maximum is {maxItems}");
                    }
                }

                if (schema.TryGetValue("uniqueItems", out var uniqueItemsToken) && uniqueItemsToken.Type == JTokenType.Boolean && uniqueItemsToken.Value<bool>())
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (var i = 0; i < instance.Count; i++)
                    {
                        var key = JsonCompat.Normalize(instance[i].ToString(Formatting.None));
                        if (!seen.Add(key))
                        {
                            errors.Add($"{path}[{i}]: duplicate entry detected");
                            break;
                        }
                    }
                }

                if (!schema.TryGetValue("items", out var itemsToken))
                {
                    return;
                }

                if (itemsToken is JObject singleSchema)
                {
                    for (var i = 0; i < instance.Count; i++)
                    {
                        ValidateElement(singleSchema, instance[i], $"{path}[{i}]", errors);
                    }
                }
                else if (itemsToken is JArray tupleSchemas)
                {
                    for (var i = 0; i < tupleSchemas.Count && i < instance.Count; i++)
                    {
                        ValidateElement(tupleSchemas[i], instance[i], $"{path}[{i}]", errors);
                    }
                }
            }

            private void ValidateString(JObject schema, JToken instance, string path, List<string> errors)
            {
                var value = instance.Type == JTokenType.Null ? string.Empty : instance.Value<string>() ?? string.Empty;

                if (schema.TryGetValue("minLength", out var minLengthToken))
                {
                    var minLength = minLengthToken.Value<int>();
                    if (value.Length < minLength)
                    {
                        errors.Add($"{path}: string length {value.Length} is less than minimum {minLength}");
                    }
                }

                if (schema.TryGetValue("maxLength", out var maxLengthToken))
                {
                    var maxLength = maxLengthToken.Value<int>();
                    if (value.Length > maxLength)
                    {
                        errors.Add($"{path}: string length {value.Length} exceeds maximum {maxLength}");
                    }
                }

                if (schema.TryGetValue("pattern", out var patternToken))
                {
                    var pattern = patternToken.Value<string>();
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        if (!Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant))
                        {
                            errors.Add($"{path}: string '{value}' does not match pattern '{pattern}'");
                        }
                    }
                }
            }

            private void ValidateNumber(JObject schema, JToken instance, string path, List<string> errors)
            {
                var value = GetDecimalValue(instance);
                if (schema.TryGetValue("minimum", out var minimumToken))
                {
                    var minimum = GetDecimalValue(minimumToken);
                    if (value < minimum)
                    {
                        errors.Add($"{path}: value {value} is less than minimum {minimum}");
                    }
                }

                if (schema.TryGetValue("maximum", out var maximumToken))
                {
                    var maximum = GetDecimalValue(maximumToken);
                    if (value > maximum)
                    {
                        errors.Add($"{path}: value {value} exceeds maximum {maximum}");
                    }
                }
            }

            private decimal GetDecimalValue(JToken token)
            {
                if (token == null || token.Type == JTokenType.Null)
                {
                    return 0m;
                }

                if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                {
                    return Convert.ToDecimal(((JValue)token).Value, CultureInfo.InvariantCulture);
                }

                if (token.Type == JTokenType.String && decimal.TryParse(token.Value<string>(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }

                return 0m;
            }

            private bool IsTypeAllowed(JToken typeDefinition, JToken instance)
            {
                if (typeDefinition.Type == JTokenType.String)
                {
                    return MatchesType(typeDefinition.Value<string>(), instance);
                }

                if (typeDefinition is JArray array)
                {
                    foreach (var option in array)
                    {
                        if (option.Type == JTokenType.String && MatchesType(option.Value<string>(), instance))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                return true;
            }

            private bool MatchesType(string type, JToken instance)
            {
                switch (type)
                {
                    case "object":
                        return instance.Type == JTokenType.Object;
                    case "array":
                        return instance.Type == JTokenType.Array;
                    case "string":
                        return instance.Type == JTokenType.String;
                    case "number":
                        return instance.Type == JTokenType.Integer || instance.Type == JTokenType.Float;
                    case "integer":
                        return instance.Type == JTokenType.Integer || (instance.Type == JTokenType.Float && IsInteger(instance));
                    case "boolean":
                        return instance.Type == JTokenType.Boolean;
                    case "null":
                        return instance.Type == JTokenType.Null;
                    default:
                        return true;
                }
            }

            private bool IsInteger(JToken token)
            {
                if (token.Type == JTokenType.Integer)
                {
                    return true;
                }

                if (token.Type == JTokenType.Float)
                {
                    var value = ((JValue)token).Value;
                    if (value == null)
                    {
                        return false;
                    }

                    var decimalValue = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    return decimal.Truncate(decimalValue) == decimalValue;
                }

                return false;
            }

            private string DescribeType(JToken typeDefinition)
            {
                if (typeDefinition.Type == JTokenType.String)
                {
                    return typeDefinition.Value<string>() ?? "unknown";
                }

                if (typeDefinition is JArray array)
                {
                    var values = array.Values<string?>().Where(v => !string.IsNullOrEmpty(v));
                    return string.Join(" | ", values);
                }

                return "unspecified";
            }

            private string DescribeInstance(JToken instance)
            {
                switch (instance.Type)
                {
                    case JTokenType.Object:
                        return "object";
                    case JTokenType.Array:
                        return "array";
                    case JTokenType.String:
                        return "string";
                    case JTokenType.Integer:
                    case JTokenType.Float:
                        return "number";
                    case JTokenType.Boolean:
                        return "boolean";
                    case JTokenType.Null:
                        return "null";
                    default:
                        return instance.Type.ToString();
                }
            }

            private bool JsonEquals(JToken left, JToken right)
            {
                var leftCanonical = JsonCompat.Normalize(left.ToString(Formatting.None));
                var rightCanonical = JsonCompat.Normalize(right.ToString(Formatting.None));
                return string.Equals(leftCanonical, rightCanonical, StringComparison.Ordinal);
            }

            private JToken ResolveReference(string reference)
            {
                if (string.IsNullOrEmpty(reference) || reference == "#")
                {
                    return _schemaDocument;
                }

                if (!reference.StartsWith("#", StringComparison.Ordinal))
                {
                    throw new NotSupportedException($"Only local schema references are supported. Encountered '{reference}'.");
                }

                var pointer = reference.Substring(1);
                var segments = pointer.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var current = _schemaDocument;
                foreach (var rawSegment in segments)
                {
                    var segment = rawSegment.Replace("~1", "/").Replace("~0", "~");
                    if (current.Type == JTokenType.Object)
                    {
                        var obj = (JObject)current;
                        if (!obj.TryGetValue(segment, out current))
                        {
                            throw new InvalidDataException($"Schema reference '{reference}' could not be resolved.");
                        }
                    }
                    else if (current.Type == JTokenType.Array)
                    {
                        if (!int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                        {
                            throw new InvalidDataException($"Schema reference '{reference}' targets non-object value.");
                        }

                        var array = (JArray)current;
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
