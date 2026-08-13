# Errors

## [ERR-20260813-001] test5-baseline

**Logged**: 2026-08-13T21:35:00Z
**Priority**: high
**Status**: pending
**Area**: tests

### Summary
The paired `test5` baseline found a Python-only `my-cond2` syntax-rules failure and shared `demo-power.scm` lazy, `=def`, and `for-else` failures.

### Error
```text
Python test3: my-cond2 false/chain expected no/works, actual many/many
Both implementations test-demo-power: Promise/car errors, =def names unbound, for-else variable unbound
```

### Context
- Python: `timeout 45 python3 miniscm.py test5/*.scm`
- C#: `timeout 45 dotnet bin/Release/net10.0/miniscm.dll test5/*.scm`
- C# passes `test5/test3.scm`; Python does not.

### Suggested Fix
Compare Python native syntax-rules literal matching with C# before changing macro expansion. Fix shared `scm/demo-power.scm` semantics only where the test contract is valid.

### Metadata
- Reproducible: yes
- Related Files: miniscm/native_syntax.py, minischeme/NativeSyntax.cs, test5/test3.scm, miniscm/scm/demo-power.scm, minischeme/scm/demo-power.scm
- See Also: LRN-20260813-001

---
