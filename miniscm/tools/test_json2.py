#!/usr/bin/env python3
import sys
sys.path.insert(0, '/miniscm')
from mtypes import *
from primitives import initenv
initenv()
from miniscm import load_file

load_file('scm/boot-core.scm')
load_file('scm/boot-sugar.scm')
libs = ['char-boolean.scm','numeric.scm','srfi-1-list.scm','srfi-13-string.scm',
        'hof-vector.scm','number-theory.scm','gensym-stream.scm',
        'data-structures-ext.scm','srfi-14-char-set.scm','generators.scm',
        'misc.scm','fill-gaps.scm']
for lib in libs:
    load_file('scm/' + lib)

# Test json-write
try:
    fn = be.lookup('json-write')
    print('json-write:', fn.__class__.__name__)
    r = fn('hello')
    print('json-write(\"hello\") =', _pr(r) if hasattr(r, '__class__') else repr(r))
except Exception as e:
    print('Error:', e)
    import traceback
    traceback.print_exc()