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

## [LRN-20260813-002] preserve-independent-regression-tests

**Logged**: 2026-08-13T22:45:00Z
**Priority**: high
**Status**: pending
**Area**: tests

### Summary
When a test helper exposes a real implementation defect, separate and preserve the helper test instead of deleting it.

### Details
The `dv-check` suite was mistakenly removed from mixed test files. The intended fix is to place the complete `check`/`dv-sum`/`dv-check` code in an independent test file and repair `floor-div` and related division behavior until both implementations pass. Test removal is not an acceptable substitute for fixing the implementation.

### Suggested Action
Keep focused regression files for division semantics and clear both Python and C# compiled caches before comparing results after macro or evaluator changes.

### Metadata
- Source: user_feedback
- Related Files: test/test-division.scm, miniscm/primitives_ext.py, minischeme/Ext.Numbers.cs
- Tags: correction, regression-tests, floor-div, python-csharp-parity
- Pattern-Key: tests.preserve-failing-regressions
- Recurrence-Count: 1
- First-Seen: 2026-08-13
- Last-Seen: 2026-08-13

---

## [LRN-20260813-003] preserve-external-worktree-changes

**Logged**: 2026-08-13T23:47:00Z
**Priority**: critical
**Status**: pending
**Area**: backend

### Summary
Never roll back, restore, or overwrite code changes made externally by the user.

### Details
Changes already present in the worktree may be intentional user refactors, even when they cause a build failure or differ from the agent's expected structure. Inspect and work with those changes. Do not use restore, checkout, reset, or equivalent actions against them. If they directly conflict with the requested task or make progress impossible, stop and ask the user before changing them.

### Suggested Action
Before editing, inspect `git status` and the relevant diff. Keep unrelated and user-owned changes untouched. Treat explicit user corrections about file organization or implementation structure as authoritative for the remainder of the session and future workspace tasks.

### Metadata
- Source: user_feedback
- Related Files: minischeme/init.cs, minischeme/Program.cs
- Tags: correction, worktree, preserve-user-changes, no-rollback
- Pattern-Key: workflow.never-revert-external-changes
- Recurrence-Count: 1
- First-Seen: 2026-08-13
- Last-Seen: 2026-08-13

---
