#!/usr/bin/env python3
"""Find tests that only run with pyb=True (use primitives_ext.py functions)"""
import os, re, glob

ROOT = os.path.dirname(os.path.dirname(os.path.realpath(__file__)))

with open(os.path.join(ROOT, 'test/test-ext-accuracy.scm')) as f:
    content = f.read()

checks = re.findall(r'\(check "([^"]+)"', content)
print(f'Total check forms: {len(checks)}')

# Core builtins from primitives.py
with open(os.path.join(ROOT, 'miniscm/primitives.py')) as f:
    py = f.read()
core = set()
for m in re.finditer(r"builtin\s*\(\s*'([^']+)'\s*,", py):
    core.add(m.group(1))

# Ext builtins from primitives_ext.py
ext = set()
with open('primitives_ext.py') as f:
    ext_content = f.read()
for m in re.finditer(r"builtin\s*\(\s*'([^']+)'\s*,", ext_content):
    ext.add(m.group(1))

# Scheme lib
scheme = set()
for fn in sorted(glob.glob(os.path.join(ROOT, 'miniscm/scm/*.scm'))):
    with open(fn) as f:
        scm = f.read()
    for m in re.finditer(r'\(define\s+\(([a-z][a-z0-9?<=!*/>-]+)', scm):
        scheme.add(m.group(1))
    for m in re.finditer(r'\(define\s+([a-z][a-z0-9?<=!*/>-]+)\b', scm):
        scheme.add(m.group(1))
    for m in re.finditer(r'\(define-syntax\s+([a-z][a-z0-9?<=!*/>-]+)', scm):
        scheme.add(m.group(1))

special = {'and','let','let-values','let*','do','when','unless','case','cond',
           'lambda','define','define-syntax','define-values','set!','if',
           'begin','quote','quasiquote','import','syntax-rules'}

all_avail = core | scheme | special

tested_fns = set()
for m in re.finditer(r'\(check\s+"[^"]+"\s+\(([a-z][a-z0-9?<=!*/>-]+)', content):
    tested_fns.add(m.group(1))

python_only = tested_fns - all_avail
ext_only_in_test = python_only & ext

print(f'\nFunctions tested but ONLY in primitives_ext.py (no Scheme equivalent):')
print(f'Total: {len(ext_only_in_test)}')
for f in sorted(ext_only_in_test):
    print(f'  {f}')