# InventorModel Skills

The canonical Agent Skill is:

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

`SKILL.md` contains the skill metadata and operating contract. Detailed syntax and capability boundaries are split into `references/` so external agents can load only the material they need. The embedded InventorModel AI session loads the same package from the deployed Addin.
