# WormWorld CSV ⇄ JSONL Utility

This tiny console app bridges the compact CSV genomes used for source control with the fully-expanded JSONL format described by the v0 schema.

## Usage

```bash
# Convert CSV → JSONL (validating each row against the schema)
CsvJsonl --csv Data/genomes/genomes.csv --out Data/genomes/detailed.jsonl

# Convert JSONL → CSV
CsvJsonl --jsonl Data/genomes/detailed.jsonl --out Data/genomes/roundtrip.csv

# Override the seed column during either conversion
CsvJsonl --csv Data/genomes/genomes.csv --out Data/genomes/alt.jsonl --seed 42
```

* `--csv` and `--jsonl` are mutually exclusive.
* `--out` selects the destination file to create.
* When `--seed` is supplied every genome in the batch is written with the override seed; omit it to keep the stored `seed` values.
* The tool automatically locates `Data/schemas/genome.schema.json` relative to the current working directory and reports any schema violations with row/line numbers.