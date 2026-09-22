# InventorModel DSL v0.1

The language is intentionally line-oriented and small. There is one modeling representation: `.ivmodel`; the implementation parses it directly into an in-memory AST and executes native Inventor operations.

The canonical syntax and capability references live with the AI skill so human and agent documentation cannot drift:

- [DSL fundamentals](../Skills/inventor-model/references/dsl.md)
- [Sketch syntax](../Skills/inventor-model/references/sketches.md)
- [Feature syntax](../Skills/inventor-model/references/features.md)
- [Tools and workflow](../Skills/inventor-model/references/tools.md)
- [Verification and repair](../Skills/inventor-model/references/verification.md)
- [Modeling patterns](../Skills/inventor-model/references/patterns.md)

Run `validate` before `build` to catch syntax, missing arguments, unknown parameters, invalid references, and unsupported feature types without starting Inventor.
