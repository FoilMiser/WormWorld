diff --git a//dev/null b/Docs/genome-spec.md
index 0000000000000000000000000000000000000000..c3878efdae32c4d7c1fd1afc613a3aaa4a85c59a 100644
--- a//dev/null
+++ b/Docs/genome-spec.md
@@ -0,0 +1,118 @@
+# WormWorld Genome Specification v0
+
+## 1. Scope and Determinism
+1.1 **Versioning**: Every genome SHALL declare `version: "v0"`. Future versions must increment the version string.
+1.2 **Identity**: Genomes MUST include a unique `id` (string, RFC4122 UUID recommended) and a human-readable `name` (1–64 UTF-8 chars).
+1.3 **Seeding**: `seed` MUST be a 64-bit unsigned integer (0–18,446,744,073,709,551,615) passed to the single RNG service identified in metadata.
+1.4 **Metadata**: `metadata` SHALL be an object with, at minimum, `rng_service` (string identifier of the deterministic RNG provider). Additional metadata entries are optional but MUST be deterministic constants.
+1.5 **Deterministic Operations**: No RNG use is permitted outside the named RNG service; mutations must obtain randomness exclusively from that service using the stored `seed`.
+1.6 **Serialization**: Compact CSV rows compress nested sections as JSON strings (see §8). Expanded JSON/JSONL MUST respect the JSON Schema in `Data/schemas/genome.schema.json`.
+
+## 2. Body Grid and Cells
+2.1 **Grid Dimensions**: `body.grid.width` and `body.grid.height` are positive integers (units: cells). They define CSV-style coordinates `(row, column)` with 0-based indices.
+2.2 **Cell Records**: `body.cells` is an array of cell entries covering every coordinate in the rectangle `[0,height)×[0,width)`.
+2.3 **Biomass**: All cells have equal biomass implicitly; no field is stored.
+2.4 **Cell Fields**:
+- `coord`: object `{ row, col }` (integers within grid bounds).
+- `tissue`: enum defined in §3.
+- `area`: number ≥0.01, ≤10.00 (arbitrary square-units as density proxy).
+- `material`: object with properties (density-independent):
+  - `elasticity` (0–1), `flexibility` (0–1), `toughness` (0–1),
+  - `shock_resistance` (0–1), `continuous_resistance` (0–1).
+- `edge_shape`: enum `Square`, `Rounded`, `Spiked`, `Tapered`.
+2.5 **Mutation Notes**: Mutations MAY recolor tissues, tweak `area`, or adjust material coefficients but MUST preserve 0–1 bounds and keep coordinates unique.
+
+## 3. Tissue Palette
+3.1 Valid tissues: `Throat`, `Digestive`, `Brain`, `Eye`, `Reproductive`, `MuscleAnchor`, `Fat`, `Skin`, `Armor`, `PheromoneEmitter`, `PheromoneReceptor`, `NerveEnding`.
+3.2 Mutations MUST choose from the palette; introducing new tissues is a schema-breaking change.
+
+## 4. Neural Architecture
+4.1 **Brain Summary**: `brain.cell_count` equals the number of brain tissue cells present (§2). This count determines neural limits.
+4.2 **Inputs**: `brain.input_nodes` MUST equal `brain.cell_count`; engines derive sensor inputs accordingly.
+4.3 **Hidden Budget**: `brain.hidden_nodes.max` = `brain.cell_count²` for zero hidden layers. For each hidden layer beyond the first, add `brain.cell_count` to the max hidden node budget.
+4.4 **Layers**: `brain.layer_limit` = `floor(brain.cell_count/2)`.
+4.5 **Runtime Usage**: `brain.hidden_nodes.used` (0–budget) and `brain.layers.used` (1–limit) store current architecture usage.
+4.6 **Mutation Notes**: Mutations may add/remove hidden nodes (costing energy) or connections (free). Hidden nodes beyond the computed budget MUST be rejected.
+
+## 5. Sensory Organs
+5.1 **Eyes**: `senses.vision` object with fields:
+- `eye_cell_count`: integer matching Eye tissue count.
+- `field_of_view_deg`: 1–360 degrees.
+- `range_cells`: 1–min(128, max(width,height)).
+- `processing_energy`: energy units (0–1, scaled share of total).
+- `clarity_falloff`: number ≥0 describing information loss per cell of distance.
+5.2 **Mutation Notes**: Increasing eye count or energy improves perception but must obey range cap; trade-offs documented by adjusting fields.
+
+5.3 **Pheromones**: `senses.pheromones` summarizes chemical sensing:
+- `pair_count`: integer equal to the number of entries in `pheromone_pairs` (§7).
+- `processing_energy`: 0–1 fraction of total energy reserved for pheromone computation.
+- `information_focus`: enum `TrailTracking`, `FieldSampling`, `BroadcastAnalysis` describing decoding strategy.
+5.4 **Mutation Notes**: Adjusting `processing_energy` shifts energy between chemotaxis and other systems; `information_focus` can mutate among the enum values.
+
+## 6. Reproduction and Musculature
+6.1 **Reproductive Cells**: `reproduction` object:
+- `cell_count`: equals Reproductive tissue count.
+- `mode`: enum `Fast` or `Slow`.
+- `gestation_ticks`: positive integer (time units) computed via simulation (fast < slow).
+- `offspring_energy_ratio`: 0–1 (fraction of parental energy passed).
+6.2 **Mutation Notes**: Switching mode adjusts gestation vs energy trade-off; values must remain consistent (Fast implies lower ratio and shorter gestation).
+
+6.3 **Muscles**: `muscles` array of objects:
+- `id`: string identifier.
+- `anchor_a` / `anchor_b`: coordinates referencing `MuscleAnchor` or other tissues (row/col ints).
+- `nerve_ending_coord`: coordinate of the controlling nerve ending.
+- `rest_length_cells`: number ≥1 (cells).
+- `width_cells`: number ≥1.
+- `strength_factor`: number ≥0.1.
+- `energy_cost`: number ≥0 (per actuation).
+6.4 **Mutation Notes**: Mutations may reroute anchors or adjust strength/energy; every muscle MUST keep a valid nerve connection.
+
+## 7. Pheromone System
+7.1 **Pairs**: `pheromone_pairs` array; each entry holds an `emitter` and `receptor` object.
+7.2 **Emitter Fields**:
+- `cell_coord`: coordinate of `PheromoneEmitter` tissue.
+- `specialization_energy`: 0–1.
+- `length_cells`: 1–32.
+- `field_shape`: enum `Cone`, `Sphere`, `Trail`.
+- `color_signature`: string token (e.g., hex color).
+- `amount_per_tick`: number ≥0.
+- `staying_power_ticks`: integer ≥1.
+7.3 **Receptor Fields**:
+- `cell_coord`: coordinate of `PheromoneReceptor` tissue.
+- `specialization_energy`: 0–1.
+- `range_cells`: 1–64.
+- `color_sensitivity`: string token matching expected emitter colors.
+- `sensitivity`: number 0–1.
+- `info_detail`: enum `Low`, `Medium`, `High`.
+7.4 **Mutation Notes**: Emitters and receptors mutate as pairs; energy specialization adjusts trade-offs.
+
+## 8. Nerve Endings and Hidden Nodes
+8.1 **Nerve Endings**: `nerves.endings` array stores coordinates and connections:
+- `cell_coord`: coordinate of `NerveEnding` tissue.
+- `brain_target`: index of brain input (0–`brain.cell_count`−1).
+- `can_cluster`: boolean. When true, nearby endings (Chebyshev distance ≤1) MAY merge into hidden nodes.
+8.2 **Hidden Nodes from Nerves**: `nerves.cluster_hidden_nodes` records clusters created by nearby endings, costing half the energy of a brain cell per node.
+8.3 **Mutation Notes**: Nerve placements may move; cluster toggles control additional hidden node creation costs.
+
+## 9. Fat, Skin, and Armor
+9.1 **Tissue Roles**: Fat stores energy, Skin supplies damping, Armor adds toughness.
+9.2 **Material Parameters**: Each cell’s material coefficients encode elasticity/flexibility/toughness plus resistances:
+- `shock_resistance` counters impulsive damage.
+- `continuous_resistance` counters sustained force.
+9.3 **Mutation Notes**: Adjust material values to evolve resistance strategies; keep coefficients within bounds.
+
+## 10. Energy Budget and Fitness
+10.1 **Energy**: `energy` object tracks allocations:
+- `vision_share`, `movement_share`, `pheromone_share`, `brain_share`: numbers 0–1 summing ≤1.
+- `reserve` implicitly covers remaining energy.
+10.2 **Fitness Weights**: `fitness_weights` object lists domain-specific multipliers (e.g., `survival`, `reproduction`, `exploration`). Values range 0–10.
+10.3 **Mutation Notes**: Budgets and weights mutate slowly to avoid destabilizing energy conservation.
+
+## 11. Serialization for CSV
+11.1 CSV headers include atomic fields directly; nested objects appear as JSON strings with `_json` suffix (`cells_json`, `brain_json`, etc.).
+11.2 Expanded JSONL mirrors the schema exactly, with all arrays/objects fully materialized.
+11.3 The canonical column order for v0 is: `version,id,name,seed,metadata_json,body_json,brain_json,senses_json,reproduction_json,muscles_json,pheromones_json,nerves_json,energy_json,fitness_json`.
+
+## 12. Mutation Governance
+12.1 Mutations MUST respect deterministic metadata and the RNG constraint.
+12.2 Schema-compliant genomes are considered valid; engines may impose additional biome rules without modifying the stored genome.