# InventorModel DSL v0.2

The language is intentionally line-oriented and small. There is one modeling representation: `.ivmodel`; the implementation parses it directly into an in-memory AST and executes native Inventor operations.

The canonical syntax and capability references live with the AI skill so human and agent documentation cannot drift:

- [DSL fundamentals](../Skills/inventor-model/references/dsl.md)
- [Sketch syntax](../Skills/inventor-model/references/sketches.md)
- [Feature syntax](../Skills/inventor-model/references/features.md)
- [Tools and workflow](../Skills/inventor-model/references/tools.md)
- [Verification and repair](../Skills/inventor-model/references/verification.md)
- [Modeling patterns](../Skills/inventor-model/references/patterns.md)

`build` performs DSL validation internally before touching Inventor. Use `validate` only when you want a dry run or need to debug syntax/semantics separately.


## Incremental synchronization

The complete `.ivmodel` source remains the canonical target state. Re-submitting it through `build` does not imply a full rebuild: InventorModel compares it with the synchronized model and chooses a no-op, parameter update, feature update, local structural rebuild, or same-document full rebuild.

Small conversational deltas can use the `modify` tool. See the canonical Skill references for the supported local command forms.
