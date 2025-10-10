# WormWorld.Analyzers

Roslyn analyzers that enforce deterministic randomness and keep the `Genome` surface aligned with the authoritative schema.

## Diagnostics

| ID    | Description |
|-------|-------------|
| WW001 | Flags any use of `UnityEngine.Random` or `System.Random` outside `RngService`. |
| WW002 | Ensures `Genome.SCHEMA_VERSION` matches the version in `Data/schemas/genome.schema.json`. |
| WW003 | Ensures each public field/property on `Genome` has a matching property in the schema. |

## Building & CI usage

```bash
cd Tools/Analyzers/WormWorld.Analyzers
dotnet build
```

Add the resulting DLL to your solution's analyzers automatically by building with analyzers enabled:

```bash
dotnet build WormWorld.sln /p:RunAnalyzers=true
```

The included `AdditionalFiles.targets` wires the schema into the analyzer when the repository layout matches this project.

## Unity integration

Unity uses the C# compiler directly, so place the built analyzer DLL where Unity can load it and reference it via `Assets/csc.rsp`:

```
/analyzer:../Tools/Analyzers/WormWorld.Analyzers/bin/Debug/netstandard2.0/WormWorld.Analyzers.dll
```

(Adjust the relative path if your local folder layout differs.)

Alternatively, you can reference the analyzer assembly from an `.asmdef` by setting **Use Global Analyzers** and adding the DLL to the analyzer list.

## Updating the schema

When you evolve the `Genome` public API:

1. Update `Docs/genome-spec.md` and `Data/schemas/genome.schema.json`.
2. Bump the schema `version` and mirror that value in `Genome.SCHEMA_VERSION`.
3. Run `dotnet build` to ensure no analyzer warnings remain.
