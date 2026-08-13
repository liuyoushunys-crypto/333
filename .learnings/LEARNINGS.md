# Learnings

## [LRN-20260813-001] macro-fix-discipline

**Logged**: 2026-08-13T21:30:00Z
**Priority**: critical
**Status**: pending
**Area**: tests

### Summary
Macro-system fixes must be minimal, cross-implementation, and guided by the implementation that already passes.

### Details
The Python and C# macro expansion and execution paths are highly complex and already stabilized through extensive debugging. For any macro-related test where one implementation passes and the other fails, first compare the passing implementation's expansion, binding, hygiene, and evaluation behavior. Avoid broad rewrites or speculative changes; identify the smallest shared semantic discrepancy, assess regressions, and run both implementations' macro regressions after every change.

### Suggested Action
Use per-test paired baselines, preserve existing algorithms, and require `define-macro`, `syntax-rules`, hygiene, quasiquote, and named-let regressions after each macro change.

### Metadata
- Source: user_feedback
- Related Files: miniscm/primitives_first.py, miniscm/native_syntax.py, minischeme/Evaluator.cs, minischeme/NativeSyntax.cs
- Tags: macros, python-csharp-parity, minimal-fix, regression-control
- Pattern-Key: macros.cross-implementation-minimal-fix
- Recurrence-Count: 1
- First-Seen: 2026-08-13
- Last-Seen: 2026-08-13

---
