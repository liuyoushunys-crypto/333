#!/usr/bin/env python3
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.realpath(__file__))), 'miniscm'))
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

fn = be.lookup('json-write')
print('json-write:', fn)
r = fn('hello')
print('result:', repr(r))
print('type:', type(r))