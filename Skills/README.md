# InventorModel Skills

The canonical MCP Skill package is:

```text
inventor-model/
├─ SKILL.md
└─ references/
   ├─ dsl.md
   ├─ sketches.md
   ├─ features.md
   ├─ tools.md
   ├─ verification.md
   └─ patterns.md
```

`SKILL.md` contains the operating contract for external AI clients that use `InventorModel.Mcp.exe`.

Detailed syntax and capability boundaries are split into `references/` so agents can load only the material needed for the current modeling task.

InventorModel does not provide an embedded Agent or Addin UI. The MCP client is responsible for model reasoning, conversation, image understanding, and Skill loading.
