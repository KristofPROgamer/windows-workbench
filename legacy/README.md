# Archived implementation

These files preserve the audited originals for comparison and mocked regression tests. They are not the supported launch path. The original script performs broad Windows changes; do not use it as the new application's engine.

- `Optimize-Win11.original.ps1`: exact audited bytes, SHA-256 `80200A67C6253C2385F4C24E5CC5A27B54458D2445F4C58734F79CF1A7FE36D3`.
- `README.original.md`: original instructions, retained as historical evidence.
- `win11_debloat_guide.original.md`: original guide, retained as historical evidence.

The new root launcher opens Windows Workbench. Historical audit line numbers refer to the archived original script. The legacy audit harness extracts named functions into mocks; it never invokes the full script.
