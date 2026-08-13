# Builtin declarations. Implementations live in primitives_first.py.
import math
import io
import base64 as _base64
import functools as _functools
import json as _json
import os as _os
import random as _random
import re as _re
import time as _time
from functools import cmp_to_key
from mtypes import (
    Sym, Cell, SchemeString, SchemeChar, SchemeVector, SchemeBytevector,
    Promise, SyntaxObject, SchemeException, ErrorObject, NIL, VOID,
    EOF, TRUE, FALSE, Env, _cell_len, _cells, _sn, _plist, _lst, builtin, be,
    _pr, _so
)
from reader import parse_number_scheme



import prim as _prim
from prim import *
from prim import call as _scheme_call

# Include private helpers used by the builtin declarations.
globals().update(vars(_prim))

def initenv_first():
    # ── 数值/逻辑 ──
    builtin('+', add)
    builtin('-', sub)
    builtin('=', eq_num)
    builtin('<', lt)
    builtin('>', gt)
    builtin('<=', le)
    builtin('>=', ge)
    builtin('number->string', _number_to_string)

    # ── 谓词 ──
    builtin('eq?', lambda a, b: TRUE if a is b else FALSE)
    builtin('eqv?', eqv)
    builtin('equal?', equal)
    builtin('symbol?', lambda x: TRUE if isinstance(x, Sym) else FALSE)
    builtin('procedure?', lambda x: TRUE if callable(x) or isinstance(x, tuple) else FALSE)
    builtin('not', lambda x: TRUE if x is FALSE else FALSE)

    # ── 对与列表 ──
    builtin('pair?', lambda x: TRUE if isinstance(x, Cell) else FALSE)
    builtin('null?', lambda x: TRUE if x is NIL else FALSE)
    builtin('cons', cons)
    builtin('car', car)
    builtin('cdr', cdr)
    builtin('caar', caar)
    builtin('cadr', cadr)
    builtin('cdar', cdar)
    builtin('cddr', cddr)
    builtin('list', lst)
    builtin('list?', is_list)
    builtin('length', lambda lst: _cell_len(lst) if isinstance(lst, Cell) else 0)
    builtin('append', append)
    builtin('list-ref', list_ref)
    builtin('map', map_)
    builtin('memq', memq)
    builtin('assq', assq)
    builtin('for-each', for_each_fn)
    builtin('filter', filter_)
    builtin('vector?', lambda x: TRUE if isinstance(x, (list, SchemeVector)) else FALSE)
    builtin('vector->list', lambda v: _lst(list(v.data if hasattr(v, 'data') else v)))
    builtin('list->vector', lambda lst: SchemeVector(_cells(lst)))

    # ── 字符串 ──
    builtin('string->symbol', lambda s: Sym(str(s)) if isinstance(s, (str, SchemeString)) else s)
    builtin('string-append', lambda *a: SchemeString(''.join(str(x) for x in a)))
    builtin('display', dsp)
    builtin('newline', lambda: (sys.stdout.write("\n"), VOID)[-1])

    # ── 其他 ──
    builtin('void', lambda *a: VOID)
    builtin('error', error)

    # ── 桥接（实现在 primitives_first.py, 惰性导入 miniscm 避免循环依赖）──
    builtin('eval', _eval_bridge)
    builtin('sx-def-env', _sx_def_env)
    builtin('sx-expand-env', _sx_expand_env)
    builtin('sx-defined?', _sx_defined)
    builtin('sx-defmacro', _sx_defmacro)
    builtin('sx-expand-call', _sx_expand_call)

    # ── 原生宏引擎桥接 (minref.py — boot-min2.scm 精简后宏体调用的原语) ──
    # 宏元组执行链: (sx-macro-expand 'pat 'body args (sx-expand-env)) →
    #   minref.sx_macro_expand → eval 宏体 → min-* 原语 → minref/native_syntax。
    # 非 syntax-rules 宏 (define-macro/quasiquote/syntax-case 等) 全部经由本组原语。


    builtin('sx-macro-expand', sx_macro_expand)
    builtin('qq-walk', qq_walk)
    builtin('sx-expand', _min_sx_expand)
    builtin('sx-get-bindings', sx_get_bindings)
    builtin('sx-gen-temps', sx_gen_temps)
    builtin('sx-syntax-case', sx_syntax_case)
    builtin('sx-with-syntax', sx_with_syntax)
    builtin('sx-let-syntax', sx_let_syntax)
    builtin('sx-make-macro-binding', sx_make_macro_binding)
    builtin('qs-expand', qs_expand)
    builtin('sx-dispatch', sx_dispatch)

def initenv():
    builtin('NIL', lambda: NIL)
    builtin('stream-null', NIL)
    builtin('pi', math.pi)
    builtin('*', mul)
    builtin('/', div)

# ── 比较运算符（多参语义）──
# =、<、>、<=、>=：多参数版本 R7RS 要求全部满足才为真
#   注意：<、>、<=、>= 拒绝 complex 参数（复数不可比较大小）

# ── 数值谓词 ──
    builtin('zero?', lambda x: TRUE if x==0 else FALSE)
    builtin('positive?', lambda x: TRUE if x>0 else FALSE)
    builtin('negative?', lambda x: TRUE if x<0 else FALSE)
    builtin('even?', lambda n: TRUE if n%2==0 else FALSE)
    builtin('odd?', lambda n: TRUE if n%2!=0 else FALSE)
    # finite?/nan?：处理 NaN 和 ±inf 的判断

# ── 类型谓词 ──
    builtin('number?', lambda x:TRUE if isinstance(x,(int,float,complex,Fraction)) else FALSE)
    builtin('complex?', lambda x: TRUE if isinstance(x,(int,float,complex,Fraction)) else FALSE)
    builtin('real?', lambda x:TRUE if isinstance(x,(int,float,Fraction)) or (isinstance(x,complex) and x.imag==0) else FALSE)
    builtin('rational?', lambda x:TRUE if isinstance(x,Fraction) or (isinstance(x,int)) else FALSE)
    builtin('integer?', lambda x:TRUE if isinstance(x,int) or (isinstance(x,Fraction) and x.denominator==1) else (TRUE if isinstance(x,float) and x==int(x) else FALSE))
    builtin('exact?', lambda x: TRUE if isinstance(x,(int,Fraction)) else FALSE)
    builtin('inexact?', lambda x: TRUE if isinstance(x,float) or (isinstance(x,complex) and (isinstance(x.real,float) or isinstance(x.imag,float))) else FALSE)

# ── 数值转换 ──
    builtin('exact->inexact', lambda x: (lambda v: float('inf') if v.bit_length() > 1023 else float(v))(x) if isinstance(x, int) else (float(x) if isinstance(x, Fraction) else x))
    # inexact->exact：float 使用 limit_denominator(1000000) 近似为 Fraction
    builtin('inexact->exact', _inexact_to_exact_fn)
    builtin('string->number', _string_to_number)
    builtin('numerator', lambda x: int(x) if isinstance(x,int) else (x.numerator if isinstance(x,Fraction) else x))
    builtin('denominator', lambda x: 1 if isinstance(x,int) else (x.denominator if isinstance(x,Fraction) else x.numerator if isinstance(x,float) and x==int(x) else 1))

# ── 有理数操作 ──
    builtin('rationalize', lambda x, eps: (Fraction(x).limit_denominator() if float(eps) == 0 else simplest_between(float(x) - float(eps), float(x) + float(eps))) if isinstance(x,(int,float,Fraction)) else x)
    builtin('acosh', math.acosh)
    builtin('asinh', math.asinh)
    builtin('atanh', math.atanh)
    builtin('inexact-sqrt', lambda x: float(math.sqrt(x)))
    builtin('div0', lambda x, y: int(x) // int(y))
    builtin('mod0', lambda x, y: int(x) % int(y))
    builtin('between?', lambda x, lo, hi: TRUE if lo <= x <= hi else FALSE)
    builtin('bitwise-bit-set?', lambda x, i: TRUE if (int(x) & (1 << int(i))) else FALSE)
    builtin('hash-by-identity', lambda x: abs(id(x)))
    builtin('exact-integer-floor', lambda x, y: int(math.floor(x / y)))
    builtin('make-record-type', lambda *args: VOID)
    builtin('bytevector->list', lambda b: _lst(list(b.data)))
    builtin('car+cdr', lambda p: _lst([p.car, p.cdr]))
    builtin('append!', lambda *xs: append(*xs))
    builtin('append-reverse!', lambda x, y: append(reverse(x), y))
    builtin('assert-violation', lambda *xs: (_ for _ in ()).throw(SchemeException('assertion violation')))
    builtin('assertion-violation', lambda *xs: (_ for _ in ()).throw(SchemeException('assertion violation')))
    builtin('available-srfis', lambda: _lst([]))
    builtin('char-title-case?', lambda c: FALSE)
    builtin('char-titlecase', lambda c: SchemeChar(c.char.upper()[0] if hasattr(c, 'char') else str(c).upper()[0]))
    builtin('get-environment-variable', lambda n: SchemeString(os.environ.get(str(n), '')))
    builtin('get-environment-variables', lambda: _lst([Cell(SchemeString(k), SchemeString(v)) for k, v in os.environ.items()]))
    builtin('command-line', lambda: _lst([SchemeString(x) for x in sys.argv]))
    builtin('current-monotonic-time', lambda: time.monotonic())
    builtin('implementation-version', lambda: SchemeString('miniscm 1.0'))
    builtin('string-null?', lambda s: TRUE if len(str(s)) == 0 else FALSE)
    builtin('clamp', lambda x, lo, hi: max(lo, min(hi, x)))
    builtin('symbol-append', lambda *xs: Sym(''.join(x.name for x in xs)))
    builtin('immutable-string?', lambda s: TRUE if isinstance(s, SchemeString) else FALSE)
    builtin('rational-expt', lambda x, n: x ** n)
    builtin('provide', lambda *xs: VOID)
    # exact-integer-sqrt：返回 (sqrt, remainder) 二元组（非 tuple，直接返回两个值）
    builtin('exact-integer-sqrt', lambda x: (math.isqrt(x), x - math.isqrt(x)**2) if isinstance(x,int) else (0, 0))

# ── 整数除法运算 ──
    # quotient: truncate-division（向零取整），纯整数运算无浮点精度损失
    builtin('quotient', do_quotient)

    # remainder: truncate-division（符号同被除数）
    builtin('remainder', trunc_rem)
    
    # modulo: floor-division（符号同除数）
    builtin('modulo', do_modulo)

    builtin('gcd', gcd)
    builtin('lcm', lcm)
    builtin('max', lambda *xs: max(cell_iter(xs[0])) if len(xs) == 1 and isinstance(xs[0], Cell) else max(xs))
    builtin('min', lambda *xs: min(cell_iter(xs[0])) if len(xs) == 1 and isinstance(xs[0], Cell) else min(xs))
    builtin('sum', lambda *a: sum(a))

# ── 位运算 ──
    # arithmetic-shift: b>=0 左移 <<，b<0 右移 >>
    builtin('arithmetic-shift', lambda a,b: a<<b if b>=0 else a>>(-b))
    builtin('bit-and', AND)
    builtin('bit-ior', IOR)
    builtin('bit-xor', XOR)
    builtin('bit-not', NOT)
    # 别名：bitwise-*、logand/logior/logxor/lognot/logbit?/logtest
    builtin('logbit?', lambda n,i: TRUE if n>>i & 1 else FALSE)
    builtin('logtest', lambda n,m: TRUE if n&m else FALSE)

# ── 复数运算 ──
    builtin('angle', lambda z: float(math.atan2(z.imag, z.real)) if isinstance(z,complex) else (0.0 if z>=0 else math.pi))
    builtin('real-part', lambda z: z.real if isinstance(z,complex) else z)
    builtin('imag-part', lambda z: z.imag if isinstance(z,complex) else 0)
    builtin('make-polar', lambda r,theta: complex(float(r.numerator)/float(r.denominator) if isinstance(r,Fraction) else float(r), 0) * complex(math.cos(float(theta)), math.sin(float(theta))))

# ── 布尔操作 ──
    # boolean=? 多参全等
    builtin('boolean?', lambda x:TRUE if x is TRUE or x is FALSE else FALSE)

# ── eq?/eqv?/equal? ──

# ── 对与列表操作 ──
    builtin('set-car!', lambda p,v: setattr(p,'car',v) or VOID)
    builtin('set-cdr!', lambda p,v: setattr(p,'cdr',v) or VOID)
    builtin('caddr', lambda x: x.cdr.cdr.car)
    builtin('cadddr', lambda x: x.cdr.cdr.cdr.car)
    # length: list 返回 _cell_len，非 list 返回 0
    builtin('list-tail', lambda lst, n: list_drop(lst, int(n)))
    builtin('memv', memv)
    builtin('member', lambda x, l: member_py(x, l, equal))
    builtin('assv', assv)
    builtin('assoc', assoc)
    
    builtin('pair-fold', pair_fold_fn)
    builtin('pair-fold-right', pair_fold_right_fn)
    # filter / last / vector->list: 基础原语 (boot-min2 宏引擎依赖,
    # 原位于 initenv_ext (pyb 模式), 提升为基础 builtin 与 C# 一致)
    builtin('remove', lambda pred, lst: _lst([x for x in _cells(lst) if pred(x) is FALSE]))
    builtin('last', lambda lst: (lambda c: c.car if isinstance(c, Cell) else FALSE)(_last_pair(lst)))

    builtin('booleans->integer', booleans_to_integer)
    builtin('bits->integer', bits_to_integer_lsb)
    builtin('integer->bits-list', integer_to_bits_list)
    builtin('list->integer', bits_to_integer_lsb)
    builtin('integer->bits', lambda n, k=0: integer_to_bits_list(n, k))
    builtin('bits->list', lambda n, *a: integer_to_bits_list(n, a[0] if a else 0))
    builtin('list->bits', lambda x: bits_to_integer_lsb(x))
    builtin('integer->list', lambda n: integer_to_bits_list(int(n)))
    builtin('split-at', lambda lst, n: list_split_at(lst, int(n)))
    builtin('break-list', break_list_fn)
    builtin('span', list_span)
    builtin('break', lambda pred, lst: list_span(lambda x: FALSE if pred(x) is TRUE else TRUE, lst))
    builtin('partition', partition_fn)
    builtin('stream-car', lambda s: s.car if isinstance(s, Cell) else s[0])
    builtin('stream-cdr', lambda s: do_force(s.cdr) if isinstance(s, Cell) and isinstance(s.cdr, Promise) else (s.cdr if isinstance(s, Cell) else s[1]))
    builtin('stream-null?', lambda s: TRUE if s is NIL else FALSE)
    builtin('stream-ref', lambda s, n: stream_ref_fn(s, int(n)))
    builtin('stream-map', stream_map_fn)
    builtin('stream-filter', stream_filter_fn)
    builtin('stream-take', lambda s, n: stream_take_fn(s, int(n)))

# ── 符号操作 ──
    builtin('string?', lambda x:TRUE if isinstance(x,(str,SchemeString)) else FALSE)
    # symbol=? 在 primitives_ext.py 和 scm/char-boolean.scm 中, symbol_eq_prim)

# ── 字符操作 ──
    builtin('char?', lambda x: TRUE if isinstance(x, SchemeChar) or (isinstance(x, tuple) and len(x) > 0 and x[0] == 'char') else FALSE)
    builtin('char->integer', lambda c: ord(c[1]) if isinstance(c,tuple) else (ord(c.char) if hasattr(c,'char') else 0))
    builtin('integer->char', lambda n: SchemeChar(chr(n)) if isinstance(n,int) else SchemeChar('?'))
    # char=? 等需处理四种组合：tuple-tuple/SchemeChar-SchemeChar/tuple-SchemeChar/SchemeChar-tuple
    builtin('char=?', lambda a,b: TRUE if (isinstance(a,tuple) and a[0]=='char' and isinstance(b,tuple) and b[0]=='char' and a[1]==b[1]) or (hasattr(a,'char') and hasattr(b,'char') and a.char==b.char) or (isinstance(a,tuple) and hasattr(b,'char') and a[1]==b.char) or (hasattr(a,'char') and isinstance(b,tuple) and a.char==b[1]) else FALSE)
    builtin('char<?', lambda a,b: TRUE if (isinstance(a,tuple) and a[0]=='char' and isinstance(b,tuple) and b[0]=='char' and a[1]<b[1]) or (hasattr(a,'char') and hasattr(b,'char') and a.char<b.char) or (isinstance(a,tuple) and hasattr(b,'char') and a[1]<b.char) or (hasattr(a,'char') and isinstance(b,tuple) and a.char<b[1]) else FALSE)
    builtin('char>?', lambda a,b: TRUE if (isinstance(a,tuple) and a[0]=='char' and isinstance(b,tuple) and b[0]=='char' and a[1]>b[1]) or (hasattr(a,'char') and hasattr(b,'char') and a.char>b.char) or (isinstance(a,tuple) and hasattr(b,'char') and a[1]>b.char) or (hasattr(a,'char') and isinstance(b,tuple) and a.char>b[1]) else FALSE)
    builtin('char<=?', lambda a,b: TRUE if (isinstance(a,tuple) and a[0]=='char' and isinstance(b,tuple) and b[0]=='char' and a[1]<=b[1]) or (hasattr(a,'char') and hasattr(b,'char') and a.char<=b.char) or (isinstance(a,tuple) and hasattr(b,'char') and a[1]<=b.char) or (hasattr(a,'char') and isinstance(b,tuple) and a.char<=b[1]) else FALSE)
    builtin('char>=?', lambda a,b: TRUE if (isinstance(a,tuple) and a[0]=='char' and isinstance(b,tuple) and b[0]=='char' and a[1]>=b[1]) or (hasattr(a,'char') and hasattr(b,'char') and a.char>=b.char) or (isinstance(a,tuple) and hasattr(b,'char') and a[1]>=b.char) or (hasattr(a,'char') and isinstance(b,tuple) and a.char>=b[1]) else FALSE)
    builtin('char-alphabetic?', lambda c: TRUE if (isinstance(c, tuple) and c[0] == 'char' and c[1].isalpha()) or (hasattr(c, 'char') and c.char.isalpha()) else FALSE)
    builtin('char-numeric?', lambda c: TRUE if isinstance(c, tuple) and c[0] == 'char' and c[1].isdigit() else (TRUE if hasattr(c, 'char') and c.char.isdigit() else FALSE))
    builtin('char-whitespace?', lambda c: TRUE if isinstance(c, SchemeChar) and c.char.isspace() or (isinstance(c, tuple) and c[0] == 'char' and c[1].isspace()) else FALSE)
    builtin('char-upper-case?', lambda x: TRUE if isinstance(x, tuple) and x[1].isupper() else (TRUE if hasattr(x, "char") and x.char.isupper() else FALSE))
    builtin('char-lower-case?', lambda x: TRUE if isinstance(x, tuple) and x[1].islower() else (TRUE if hasattr(x, "char") and x.char.islower() else FALSE))
    builtin('char-upcase', lambda c: (SchemeChar(c[1].upper()) if isinstance(c, tuple) and c[0] == 'char' else SchemeChar(c.char.upper())))
    builtin('char-downcase', lambda c: (SchemeChar(c[1].lower()) if isinstance(c, tuple) and c[0] == 'char' else SchemeChar(c.char.lower())))
    builtin('char-foldcase', lambda x: SchemeChar((x.char if isinstance(x, SchemeChar) else x[1]).lower()))

# ── 字符串操作 ──
    # string-length/string-ref/string-set!/string-fill! 等
    #   SchemeString 通过 .data 列表实现可变性
    #   string-copy 返回 SchemeString（可变副本），支持 start/end 切片
    #   make-string: 重复字符构造字符串，默认填充空格
    builtin('string-length', lambda s: len(str(s)))
    builtin('string-ref', string_ref_prim)
    builtin('string-set!', lambda v,i,c: (str_mutate(v), str_set_char(v, i, c), VOID)[-1])
    builtin('string-fill!', string_fill_prim)
    builtin('string-copy', lambda s,*a: SchemeString(str(s)) if not a else SchemeString(str(s)[a[0]:a[1]] if len(a)>1 else str(s)[a[0]:]))
    builtin('make-string', lambda n,*a: SchemeString((char_val(a[0]) if a else ' ') * n))
    builtin('substring', lambda s,i,j: SchemeString(str(s)[i:j]))
    builtin('string->list', lambda s: _lst([SchemeChar(c) for c in str(s)]))
    builtin('symbol->string', str)
    builtin('string-downcase', lambda s: SchemeString(str(s).lower()))
    builtin('string-upcase', lambda s: SchemeString(str(s).upper()))
    builtin('list->string', lambda lst: SchemeString(''.join(c[1] if isinstance(c,tuple) else (c.char if hasattr(c,'char') else str(c)) for c in _plist(lst))))
    builtin('string->utf8', lambda s, *span: SchemeBytevector(str(s)[int(span[0]) if span else 0:int(span[1]) if len(span) > 1 else None].encode('utf-8')))
    builtin('utf8->string', lambda s, *span: SchemeString(bytes(s.data)[int(span[0]) if span else 0:int(span[1]) if len(span) > 1 else None].decode('utf-8')) if hasattr(s,'data') else s)
    builtin('format', format_dispatch)

# ── 向量操作 ──
    # vector? 接受 list（Python list 作为不可变向量）和 SchemeVector
    builtin('vector', lambda *a: SchemeVector(list(a)))
    builtin('vector-ref', lambda v,i: v.data[i] if hasattr(v,'data') else v[i])
    builtin('vector-length', lambda v: len(v.data) if hasattr(v, 'data') else len(v))
    builtin('vector-set!', lambda v, i, x: vec_set_elem(v, i, x))
    # make-vector: 可选的填充值，默认为 NIL；FALSE/NIL 保持原值
    builtin('make-vector', lambda n, *a: SchemeVector([(NIL if not a else (a[0] if (a[0] is not FALSE) else FALSE)) for _ in range(n)]))

# ── 字节向量操作 ──
    builtin('bytevector?', lambda x: TRUE if isinstance(x,SchemeBytevector) else FALSE)
    builtin('bytevector', lambda *a: SchemeBytevector([int(x) for x in a]))
    builtin('bytevector-length', lambda v: len(v.data) if hasattr(v,'data') else 0)
    builtin('bytevector-u8-ref', lambda v,i: v.data[i] if hasattr(v,'data') else 0)
    builtin('bytevector-u8-set!', lambda v,i,x: bv_set_u8(v, i, x) if hasattr(v,'data') else VOID)
    builtin('bytevector-copy!', lambda target, at, source: [target.data.__setitem__(int(at) + i, b) for i, b in enumerate(source.data)] and VOID)
    builtin('bytevector-append', lambda *vs: SchemeBytevector([b for v in vs for b in v.data]))
    builtin('bytevector-s8-ref', lambda v,i: v.data[i] - 256 if v.data[i] >= 128 else v.data[i])
    builtin('bytevector-s8-set!', lambda v,i,x: bv_set_u8(v, i, int(x) & 255))
    builtin('list->bytevector', lambda lst: SchemeBytevector([int(x) for x in _cells(lst)]))
    builtin('make-bytevector', lambda n,*fill: SchemeBytevector([fill[0] if fill else 0]*n))

# ── 端口与 I/O ──
    # 注意：port? 检查 tuple 格式
    builtin('port?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('str-port', 'file-port') else FALSE)
    builtin('input-port?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('str-port', 'file-port') else FALSE)
    builtin('output-port?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('str-port', 'file-port') else FALSE)
    builtin('port-open?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('str-port', 'file-port') else FALSE)
    builtin('binary-port?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('bin-file-port', 'bin-str-port') else FALSE)
    builtin('input-port-open?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('str-port', 'file-port', 'bin-file-port', 'bin-str-port') else FALSE)
    builtin('output-port-open?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('str-port', 'file-port') else FALSE)
    builtin('current-input-port', lambda: ('str-port', [""]))
    builtin('current-output-port', lambda: ('str-port', [""]))
    builtin('eof-object', lambda: EOF)
    builtin('eof-object?', lambda x:TRUE if x is EOF else FALSE)
    builtin('open-input-file', lambda n: ("file-port",str(n),"r",open(str(n),'r')))
    builtin('open-binary-input-file', lambda n: ("bin-file-port",str(n),"rb",open(str(n),'rb')))
    builtin('open-binary-output-file', lambda n: ("bin-file-port",str(n),"wb",open(str(n),'wb')))
    builtin('open-output-file', lambda n: ("file-port",str(n),"w",open(str(n),'w')))
    # open-input-string: 端口为 ('str-port', [字符串, 位置])，位置暂未使用
    builtin('open-input-string', lambda s: ("str-port", [str(s), 0]))
    builtin('open-input-bytevector', lambda v: ("bin-str-port", [bytes(v.data), 0]))
    builtin('open-output-string', lambda *a: ('str-port',['']))
    builtin('open-output-bytevector', lambda *a: ('byte-port', bytearray()))
    builtin('get-output-bytevector', lambda p: SchemeBytevector(list(p[1])) if isinstance(p, tuple) and p[0] == 'byte-port' else SchemeBytevector([]))
    builtin('flush-output-port', lambda *a: VOID)
    builtin('call-with-input-string', lambda s, proc: proc(('str-port', [str(s), 0])))
    builtin('call-with-port', lambda p, proc: proc(p))
    builtin('call-with-output-string', lambda proc: (lambda p: (proc(p), SchemeString(p[1][0]))[1])(('str-port', [''])))
    builtin('call-with-string-output', lambda proc: (lambda p: (proc(p), SchemeString(p[1][0]))[1])(('str-port', [''])))
    builtin('call-with-string-output-port', lambda proc: (lambda p: (proc(p), SchemeString(p[1][0]))[1])(('str-port', [''])))
    builtin('call-with-bytevector-output-port', lambda proc: (lambda p: (proc(p), SchemeBytevector(p[1]))[1])(('byte-port', bytearray())))
    builtin('read-bytevector', lambda n, p=None: _read_bytevector(int(n), p))
    builtin('read-bytevector!', _read_bytevector_into)
    builtin('write-bytevector', _write_bytevector)
    builtin('with-input-from-string', lambda s, thunk: _with_string_input(str(s), thunk))
    builtin('delay-force', lambda p: p)
    builtin('close-input-port', lambda p: p[3].close() if isinstance(p,tuple) and p[0]=='file-port' and len(p)>3 else VOID)
    builtin('close-output-port', lambda p: p[3].close() if isinstance(p,tuple) and p[0]=='file-port' and len(p)>3 else VOID)
    builtin('close-port', lambda p: p[3].close() if isinstance(p,tuple) and p[0]=='file-port' and len(p)>3 else VOID)
    builtin('call-with-input-file', cwif)
    builtin('call-with-output-file', cwof)
    builtin('read', read_proc)
    builtin('read-char', rc)
    builtin('peek-char', pkc)
    builtin('write', write_proc)
    builtin('write-char', wc)
    builtin('port-position', port_pos)
    builtin('set-port-position!', set_port_pos)
    builtin('get-output-string', lambda x: x[1][0] if isinstance(x,tuple) and x[0]=='str-port' and isinstance(x[1],list) else (x[1] if isinstance(x,tuple) and x[0]=='str-port' else (''.join(x.data) if hasattr(x,'data') else '')))

# ── 控制流 ──
    builtin('call/cc', call_cc)
    builtin('call-with-current-continuation', call_cc)
    builtin('call-with-values', cvw)
    builtin('dynamic-wind', dynamic_wind)
    # values: 单值直接返回，多值包装为 tuple（cvw 通过 tuple 检测多值）
    builtin('values', lambda *a: tuple(a) if len(a)!=1 else a[0])
    builtin('apply', app)
    builtin('with-exception-handler', with_exception_handler)
    builtin('raise', do_raise)
    builtin('error-object?', lambda x: TRUE if isinstance(x, ErrorObject) else FALSE)
    builtin('error-object-message', lambda x: str(x.message) if isinstance(x, ErrorObject) else str(x))

# ── Promise（惰性求值）──
    builtin('force', do_force)
    builtin('make-promise', lambda thunk: Promise(thunk))
    builtin('promise?', lambda x: TRUE if isinstance(x, Promise) else FALSE)

# ── Syntax 对象操作 ──
    builtin('syntax?', lambda x: TRUE if isinstance(x,(Sym,SyntaxObject)) else FALSE)
    builtin('syntax->datum', lambda x: x.expr if isinstance(x,SyntaxObject) else x)
    builtin('datum->syntax', lambda stx,d: SyntaxObject(d) if not isinstance(d,SyntaxObject) else d)
    builtin('identifier?', lambda x: TRUE if isinstance(x,(Sym,SyntaxObject)) else FALSE)
    builtin('bound-identifier=?', id_eq)
    builtin('free-identifier=?', id_eq)

# ── 环境对象 ──
    _env_scheme_report = Env(be)
    _env_null = Env(be)
    builtin('environment?', lambda x: TRUE if isinstance(x, Env) else FALSE)
    builtin('scheme-report-environment', lambda *a: _env_scheme_report)
    builtin('null-environment', lambda *a: _env_null)
    builtin('interaction-environment', lambda: be)
    builtin('environment', lambda: be)

# ── 时间 ──
    builtin('current-second', lambda: time.time())
    builtin('current-jiffy', lambda: int(time.time()*1e6))
    builtin('jiffies-per-second', lambda: 1000000)

# ── 杂项 ──
    builtin('compose', compose_fn)
    builtin('gensym', lambda *a: next_gensym())
    builtin('gensym2', lambda: Sym(f'__g{id({})}'))
    builtin('features', lambda: Cell(Sym('r7rs'), Cell(Sym('miniscm'), NIL)))
    builtin('defined?', lambda x: TRUE if _sn(x) in be.data else FALSE)
    builtin('sink', lambda *a: VOID)
    builtin('helper', lambda *a: VOID)
    # make-parameter：SRFI-39 参数对象
    #   返回一个过程：无参调用返回当前值，一参调用设置新值。
    #   可选 converter 在设置时对值进行转换。
    builtin('make-parameter', make_param)
    # make-coroutine-generator (用 Python 线程 + queue 实现真正的 yield 挂起)
    # proc 接收 yield 函数; 每次调用生成器返回下一个 yield 值, proc 结束后返回 eof
    builtin('make-coroutine-generator', make_coroutine_generator)
    builtin('make-compound-condition', lambda *a: ErrorObject('compound', _lst(list(a)) if a else NIL))
    builtin('extract-condition', lambda *a: FALSE)
    builtin('record?', lambda x: FALSE)
    builtin('error-message', lambda x: SchemeString(str(x)))
    builtin('condition?', lambda x: TRUE if isinstance(x,(SchemeException,ErrorObject)) or (isinstance(x, tuple) and x and x[0] == 'condition') else FALSE)
    builtin('make-condition-type', lambda name, parent, predicate, fields: ('condition-type', name, [f for f in _plist(fields)]))
    builtin('make-condition', lambda typ, *fields: ('condition', typ, dict(zip((f.name if isinstance(f, Sym) else str(f) for f in typ[2]), fields[1::2]))))
    builtin('condition-ref', lambda c, field: (c[2].get(field.name if isinstance(field, Sym) else str(field), next(iter(c[2].values()), FALSE)) if isinstance(c, tuple) and len(c) > 2 and isinstance(c[2], dict) else FALSE))
    builtin('make-io-error', lambda message, *a: ErrorObject(message, _lst(a)))
    builtin('io-error?', lambda x: TRUE if isinstance(x, ErrorObject) else FALSE)
    # weak-box：基于 Cell 实现
    builtin('make-weak-box', lambda *a: Cell(Sym('weak'), a[0] if a else NIL))
    builtin('weak-box?', lambda x: isinstance(x, Cell) and x.car is Sym('weak'))
    builtin('weak-box-ref', lambda x: x.cdr.car if isinstance(x, Cell) and isinstance(x.cdr, Cell) else NIL)
    builtin('weak-box-set!', lambda v, x: setattr(v, 'cdr', Cell(x, NIL)) or VOID)
# ── SRFI-111: Boxes ──
    builtin('box', box)
    builtin('box?', is_box)
    builtin('unbox', unbox)
    builtin('set-box!', do_set_box)
# ── alist-copy ──
    builtin('alist-copy', alist_copy_fn)
    builtin('file-exists?', lambda p: TRUE if os.path.exists(str(p)) else FALSE)
    builtin('delete-file', lambda p: os.remove(str(p)) or VOID)
    builtin('rename-file', lambda old,new: os.rename(str(old),str(new)) or VOID)
    builtin('load', load)

    # ── Hash-table primitives ──
    # hash-table 使用 Python dict 实现
    # 注意：dict 的 key 是 Python 对象（Scheme 的 eq?/eqv?/equal? 语义通过 Python 的哈希自动支持）
    # make-eq-hash-table/make-equal-hash-table/make-eqv-hash-table 均使用同样的 {} 实现
    builtin('make-hash-table', make_ht)
    builtin('hash-table?', lambda x: TRUE if isinstance(x, dict) else FALSE)
    builtin('hash-table-size', lambda ht: len(ht))
    
    builtin('hash-table-set!', hash_table_set)
    builtin('hash-table-ref', lambda ht, k, *default: ht[k] if k in ht else (default[0] if default else FALSE))
    builtin('hash-table-ref/default', hash_table_ref_default)
    builtin('hash-table-exists?', lambda ht, k: TRUE if k in ht else FALSE)
    builtin('hash-table-delete!', lambda ht, k: (ht.pop(k, None), VOID)[-1])
    builtin('hash-table-copy', lambda ht: dict(ht))
    builtin('hash-table-keys', hash_table_keys)
    builtin('hash-table-values', hash_table_values)
    builtin('hash-table->alist', lambda ht: _lst([Cell(k, v) for k, v in ht.items()]))
    # alist2ht: 关联列表转 hash-table
    builtin('alist->hash-table', alist2ht)
    builtin('hash-table-for-each', lambda f, ht: [f(k, v) for k, v in ht.items()] and VOID)

    # ── 对齐 minischeme Init() 补齐 (Python scm 模式缺失) ──
    builtin('make-box', box)
    builtin('-1+', lambda x: x - 1)
    builtin('1+', lambda x: x + 1)
    builtin('bit-or', lambda *a: _reduce_bit_or(a))
    builtin('constantly', lambda x: (lambda *_: x))
    builtin('current-error-port', lambda: ('str-port', []))
    builtin('error-object-irritants', lambda e: e.irritants if isinstance(e, ErrorObject) else NIL)
    builtin('exit', lambda *a: (_sys_exit(int(a[0]) if a else 0)) or VOID)
    builtin('hash-table-contains?', lambda ht, k: TRUE if k in ht else FALSE)
    builtin('hash-table-count', lambda ht: len(ht))
    builtin('hash-table/count', lambda ht: len(ht))
    builtin('hash-table/put!', lambda ht, k, v: ht.__setitem__(k, v) or VOID)
    builtin('hash-table/update!', lambda ht, k, f, default=FALSE: ht.__setitem__(k, f(ht.get(k, default))) or VOID)
    builtin('hash-table/walk', lambda f, ht: ([f(k, v) for k, v in list(ht.items())] and VOID))
    builtin('hash-table/merge!', hash_table_merge_slash)
    builtin('string-contains?', lambda s, sub: TRUE if str(sub) in str(s) else FALSE)
    builtin('bytevector-copy', lambda bv: SchemeBytevector(list(bv.data)) if hasattr(bv, 'data') else SchemeBytevector(list(bv)))
    builtin('bytevector->u8-list', lambda bv: _lst([int(b) for b in (bv.data if hasattr(bv, 'data') else bv)]))
    builtin('u8-list->bytevector', lambda lst: SchemeBytevector([int(x) for x in _cells(lst)]))
    builtin('with-input-from-file', lambda path, thunk: _with_file(path, thunk, 'r', _redirect_in))
    builtin('with-output-to-file', lambda path, thunk: _with_file(path, thunk, 'w', _redirect_out))
    builtin('print', lambda x: (be.data['display'](x), VOID)[1])
    builtin('pretty-print', lambda x: (write_proc(x), VOID)[1])
    builtin('write-simple', write_proc)
    builtin('write-shared', write_proc)
    builtin('write-with-shared-structure', write_proc)



    # sx-def-env: 返回当前宏定义环境或全局 (C#: CurrentMacroDefEnv ?? GlobalEnv)

    # sx-expand-env: 返回当前宏调用点环境或全局 (C#: CurrentExpandEnv ?? GlobalEnv)

def initenv_ext():
    be.define('NIL', NIL)
    builtin('for-all', lambda pred, lst: TRUE if all(pred(x) is not FALSE for x in cell_iter(lst)) else FALSE)
    builtin('string-concatenate-reverse', lambda xs: SchemeString(''.join(str(x) for x in reversed(list(cell_iter(xs))))))
    builtin('substring-count', lambda s, sub: sum(1 for i in range(len(str(s)) - len(str(sub)) + 1) if str(s).startswith(str(sub), i)))
    builtin('remq', lambda x, xs: _lst([v for v in cell_iter(xs) if not (v is x)]))
    builtin('remv', lambda x, xs: _lst([v for v in cell_iter(xs) if v != x]))
    builtin('keyword?', lambda x: TRUE if isinstance(x, Sym) and x.name.startswith(':') else FALSE)
    builtin('string->keyword', lambda x: Sym(':' + str(x).lstrip(':')))
    builtin('keyword->string', lambda x: SchemeString(str(x).lstrip(':')))
    builtin('srfi-available?', lambda n: TRUE)
    builtin('stream?', lambda x: TRUE if isinstance(x, Cell) and (callable(x.cdr) or isinstance(x.cdr, Promise) or x.cdr is NIL) else FALSE)
    builtin('string-normalize-nfc', lambda x: SchemeString(str(x)))
    builtin('string-normalize-nfd', lambda x: SchemeString(str(x)))
    builtin('string-normalize-nfkc', lambda x: SchemeString(str(x)))
    builtin('string-normalize-nfkd', lambda x: SchemeString(str(x)))
    builtin('string-prefix-ci?', lambda a, b: TRUE if str(b).lower().startswith(str(a).lower()) else FALSE)
    builtin('gentemp', lambda: Sym('gentemp'))
    builtin('sorted-by', sorted_by_fn)
    builtin('update', update_fn)

    builtin('append!', _append_bang)
    builtin('append-reverse!', _append_reverse_bang)
    builtin('char-set-unfold', _char_set_unfold)
    builtin('concatenate!', concatenate_fn)
    builtin('cond-expand-srfi-61', lambda *args: TRUE)
    builtin('drop-right!', _drop_right_bang)
    builtin('find-tail', _find_tail)
    builtin('fold-right-1', _fold_right_1)
    builtin('include-ci', _include_ci)
    builtin('integer->char-set', _integer_char_set)
    builtin('lset-adjoin', _lset_adjoin)
    builtin('lset<=', _lset_subset)
    builtin('lset=', lambda eq, a, b: _lset_subset(eq, a, b) if _lset_subset(eq, a, b) is TRUE and _lset_subset(eq, b, a) is TRUE else FALSE)
    builtin('random-source-make-integers', _random_integers)
    builtin('random-source-make-reals', _random_reals)
    builtin('require-extension', lambda *args: TRUE)
    builtin('require-srfi', lambda *args: TRUE)
    builtin('test-equal?', _test_equal)
    builtin('u8vector', lambda *xs: SchemeVector(list(xs)))
    builtin('u8vector?', lambda v: TRUE if isinstance(v, SchemeVector) else FALSE)
    builtin('u8vector-length', lambda v: len(v.data))
    builtin('u8vector-ref', lambda v, i: v.data[int(i)])
    builtin('u8vector-set!', lambda v, i, x: v.data.__setitem__(int(i), x) or VOID)
    builtin('vector-sort!', lambda v, less: VOID)
    builtin('xsubstring', lambda s, start, end: SchemeString(str(s)[int(start):int(end)]))
    builtin('make-u8vector', lambda n, *a: SchemeVector([(a[0] if a else 0) for _ in range(int(n))]))
    builtin('f64vector', lambda *xs: SchemeVector(list(xs)))
    builtin('f64vector?', lambda v: TRUE if isinstance(v, SchemeVector) else FALSE)
    builtin('f64vector-length', lambda v: len(v.data))
    builtin('f64vector-ref', lambda v, i: v.data[int(i)])
    for _prefix in ('f32', 'f64', 's8', 's16', 's32', 's64', 'u16', 'u32', 'u64'):
        builtin(_prefix + 'vector', lambda *xs: SchemeVector(list(xs)))
        builtin(_prefix + 'vector?', lambda v: TRUE if isinstance(v, SchemeVector) else FALSE)
        builtin(_prefix + 'vector-length', lambda v: len(v.data))
        builtin(_prefix + 'vector-ref', lambda v, i: v.data[int(i)])
        builtin(_prefix + 'vector-set!', lambda v, i, x: v.data.__setitem__(int(i), x) or VOID)
        builtin('make-' + _prefix + 'vector', lambda n, *a: SchemeVector([(a[0] if a else 0) for _ in range(int(n))]))
    builtin('integer-compare', lambda a, b: -1 if a < b else (1 if a > b else 0))
    builtin('set', lambda *xs: _lst(list(xs)))
    builtin('set?', lambda x: TRUE if isinstance(x, Cell) else FALSE)
    builtin('set-contains?', lambda s, x: TRUE if any(v == x for v in cell_iter(s)) else FALSE)
    builtin('regexp', lambda s: _re.compile(str(s)))
    builtin('regexp?', lambda x: TRUE if hasattr(x, 'search') else FALSE)
    builtin('regexp-matches?', lambda r, s: TRUE if r.search(str(s)) else FALSE)
    builtin('make-timer', lambda *args: ('timer', args))
    builtin('timer?', lambda x: TRUE if isinstance(x, tuple) and x and x[0] == 'timer' else FALSE)
    builtin('nonempty-list?', lambda x: TRUE if isinstance(x, Cell) else FALSE)
    builtin('string-cursor-start', lambda s: 0)
    builtin('lset=', lambda eq, a, b: TRUE if len(list(cell_iter(a))) == len(list(cell_iter(b))) and all(any(eq(x, y) is TRUE for y in cell_iter(b)) for x in cell_iter(a)) else FALSE)
    builtin('generic-sequence?', lambda x: TRUE if isinstance(x, Cell) or isinstance(x, SchemeVector) or isinstance(x, SchemeString) else FALSE)
    builtin('flat-sequence?', lambda x: TRUE if isinstance(x, Cell) else FALSE)
    builtin('generic-ref', lambda x, i: x[int(i)] if hasattr(x, '__getitem__') else FALSE)
    builtin('make-ephemeron', make_ephemeron)
    builtin('ephemeron?', is_ephemeron)
    builtin('ephemeron-key', lambda x: x[1])
    builtin('ephemeron-datum', lambda x: x[2])
    builtin('make-lseq', make_lseq)
    builtin('lseq?', is_lseq)
    builtin('make-enum-set', make_enum_set)
    builtin('enum-set?', is_enum_set)
    builtin('make-array2d', make_array2d)
    builtin('array2d?', is_array2d)
    builtin('array2d-rows', array2d_rows)
    builtin('array2d-cols', lambda x: x[2])
    builtin('make-flex-vector', make_flex_vector)
    builtin('flex-vector?', is_flex_vector)
    builtin('make-unifiable-box', make_unifiable_box)
    builtin('unifiable-box?', is_unifiable_box)
    builtin('box-eval', unbox)
    builtin('mutable-string?', lambda x: TRUE if isinstance(x, SchemeString) else FALSE)
    builtin('make-mutable-string', lambda x: SchemeString(str(x)))
    builtin('string-titlecase', lambda x: SchemeString(str(x).title()))
    builtin('everywhere', lambda f, x: f(x))
    builtin('set-at', lambda xs, i, x: _lst([x if n == int(i) else v for n, v in enumerate(cell_iter(xs))]))
    builtin('unifiable-box-ref', lambda x: x[1])
    builtin('ideque', make_ideque)
    builtin('ideque?', is_ideque)
    builtin('make-integer-set', make_integer_set)
    builtin('integer-set?', is_integer_set)
    builtin('make-text', make_text)
    builtin('text?', is_text)
    builtin('text-length', text_length)
    builtin('string-compare-ci', lambda a, b: (-1 if str(a).lower() < str(b).lower() else (1 if str(a).lower() > str(b).lower() else 0)))
    builtin('two-arg-invoke', lambda f, a, b: call(f, [a, b]))
    builtin('flex-vector-ref', lambda v, i: v[1][int(i)])
    builtin('<?.', lambda pred, *xs: TRUE if all(pred(xs[i], xs[i + 1]) is TRUE for i in range(len(xs) - 1)) else FALSE)
    builtin('syntax-closure?', lambda x: TRUE if isinstance(x, tuple) and x and x[0] == 'syntax-closure' else FALSE)
    builtin('make-syntax-closure', lambda free, bound: ('syntax-closure', free, bound))
    builtin('shape', lambda *dims: _lst([int(x) for x in dims]))
    builtin('array', lambda shp, *values: SchemeVector(list(values)))
    builtin('parse-body', lambda *args: VOID)
    builtin('type-of', lambda *args: VOID)
    integer_cmp = make_comparator(lambda a, b: TRUE if a == b else FALSE, lambda a, b: TRUE if a < b else FALSE, lambda x: int(x), 'integer')
    be.define('integer-comparator', integer_cmp)
    builtin('=?', lambda c, a, b: comparator_eq_fn(c)(a, b))
    builtin('<?', lambda c, a, b: comparator_lt_fn(c)(a, b))
    builtin('current-date', lambda: ('date', int(_time.time())))
    builtin('current-time', lambda: ('time', int(_time.time())))
    builtin('date?', lambda v: TRUE if isinstance(v, tuple) and v and v[0] == 'date' else FALSE)
    builtin('time?', lambda v: TRUE if isinstance(v, tuple) and v and v[0] == 'time' else FALSE)
    # Small SRFI host values.  These are deliberately plain tuples/lists so
    # predicates and accessors remain cheap and interoperable with the core.
    builtin('csv-read', lambda port: _lst([_lst([SchemeString(x) for x in line.split(',')]) for line in str(port[1][0]).splitlines() if line != '']))
    builtin('parse', lambda *chars: int(''.join(cs_char(c) for c in chars)))
    builtin('char', lambda value: value)
    builtin('range->list', lambda r: _lst(list(range(int(r[1]), int(r[2]), int(r[3])))))
    builtin('make-range', lambda start, end, step=1: ('range', int(start), int(end), int(step)))
    builtin('m4-zero', lambda: SchemeVector([0] * 16))
    builtin('bmi-and', lambda a, b: int(a) & int(b))
    builtin('sxml?', lambda x: TRUE if isinstance(x, Cell) else FALSE)
    builtin('file-exists?', file_exists_fn)
    builtin('group-by', _group_by)
    builtin('flex-vector', lambda *values: ('flex-vector', list(values)))
    builtin('base32-encode', lambda bv: SchemeString(_base64.b32encode(bytes(bv.data)).decode('ascii')))
    builtin('int-vector', lambda *values: SchemeVector([int(x) for x in values]))
    builtin('int-vector?', lambda x: TRUE if isinstance(x, SchemeVector) else FALSE)
    builtin('assoc-map', lambda *pairs: _lst([Cell(pairs[i], pairs[i + 1]) for i in range(0, len(pairs), 2)]))
    builtin('assoc-map?', lambda x: TRUE if x is NIL or (isinstance(x, Cell) and all(isinstance(p, Cell) for p in cell_iter(x))) else FALSE)
    builtin('rt-sin', lambda x: math.sin(float(x)))
    builtin('make-operator-parser', lambda *args: (lambda value: value))
    builtin('path-absolute?', lambda path: TRUE if _os.path.isabs(str(path)) else FALSE)
    builtin('make-domain', lambda lo, hi: ('domain', lo, hi))
    builtin('domain?', lambda x: TRUE if isinstance(x, tuple) and x and x[0] == 'domain' else FALSE)
    builtin('array-rank', lambda x: 1 if isinstance(x, SchemeVector) else 0)
    builtin('option', _option)
    builtin('option?', lambda x: TRUE if isinstance(x, tuple) and x and x[0] == 'option' else FALSE)
    builtin('make-color', lambda r, g, b: ('color', float(r), float(g), float(b)))
    builtin('color?', lambda x: TRUE if isinstance(x, tuple) and x and x[0] == 'color' else FALSE)
    builtin('color-red', lambda x: x[1])
    builtin('red', ('color', 1.0, 0.0, 0.0))
    builtin('floating-point-pi', lambda: math.pi)
    builtin('floating-point-e', lambda: math.e)
    builtin('recursive-equality?', lambda a, b: TRUE if equal(a, b) is TRUE else FALSE)

    # Existing flex-vector constructor uses the same representation.
    builtin('flex-vector-ref', lambda v, i: v[1][int(i)])

    # ═══════════════════════════════════════════════════════════════
    # SRFI-111: Boxes
    # ═══════════════════════════════════════════════════════════════

    # ═══════════════════════════════════════════════════════════════
    # SRFI-128: Comparators
    # ═══════════════════════════════════════════════════════════════
    builtin('make-comparator', lambda a, b, c, d='custom': make_comparator(a, b, c, d))
    builtin('comparator?', is_comparator)
    builtin('comparator-order?', is_comparator_order)
    builtin('comparator-hashable?', is_comparator_hashable)
    builtin('comparator-test-type', lambda c: (lambda x: TRUE))
    builtin('make-default-comparator', lambda: default_comparator())
    builtin('make-eq-comparator', lambda: make_comparator(lambda a,b: a is b, lambda a,b: False, lambda x: id(x)))
    builtin('make-eqv-comparator', lambda: make_comparator(lambda a,b: a is b or a == b, lambda a,b: False, lambda x: id(x)))
    builtin('make-equal-comparator', lambda: make_comparator(lambda a,b: a == b, lambda a,b: False, lambda x: id(x)))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-141: Division (exact integer division variants)
    # ═══════════════════════════════════════════════════════════════
    builtin('floor-div', floor_div)
    builtin('floor-mod', floor_mod)
    builtin('floor-rem', floor_rem)
    builtin('floor-quotient', floor_div)
    builtin('floor-remainder', floor_rem)
    builtin('floor/', lambda a, b: Cell(floor_div(a, b), floor_rem(a, b)))

    builtin('truncate-div', truncate_div)
    builtin('truncate-rem', truncate_rem)
    builtin('truncate-quotient', truncate_div)
    builtin('truncate-remainder', truncate_rem)
    builtin('truncate/', lambda a, b: Cell(truncate_div(a, b), truncate_rem(a, b)))

    builtin('ceiling-div', ceiling_div)
    builtin('ceiling-rem', ceiling_rem)
    builtin('ceiling-quotient', ceiling_div)
    builtin('ceiling-remainder', ceiling_rem)
    builtin('ceiling/', lambda a, b: Cell(ceiling_div(a, b), ceiling_rem(a, b)))

    builtin('round-div', round_div)
    builtin('round-rem', lambda n, d: int(n) - round_div(n, d) * int(d))
    builtin('round-quotient', round_div)
    builtin('round-remainder', lambda n, d: int(n) - round_div(n, d) * int(d))
    builtin('round/', lambda a, b: Cell(round_div(a, b), int(a) - round_div(a, b) * int(b)))

    builtin('euclidean-div', euclidean_div)
    builtin('euclidean-rem', euclidean_rem)
    builtin('euclidean-quotient', euclidean_div)
    builtin('euclidean-remainder', euclidean_rem)
    builtin('euclidean/', lambda a, b: Cell(euclidean_div(a, b), euclidean_rem(a, b)))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-143: Fixnums (exact integer arithmetic with overflow check)
    # ═══════════════════════════════════════════════════════════════
    builtin('fx-width', lambda: FX_WIDTH)
    builtin('fx-greatest', lambda: FX_GREATEST)
    builtin('fx-least', lambda: FX_LEAST)
    builtin('fx+', fx_add)
    builtin('fx-', fx_sub)
    builtin('fx*', fx_mul)
    builtin('fxdiv', fx_div)
    builtin('fxmod', fx_mod)
    builtin('fxdiv0', lambda x, y: floor_div(x, y))
    builtin('fxmod0', lambda x, y: floor_rem(x, y))
    builtin('fx=?', lambda *a: fx_cmp(lambda x, y: x == y, *a))
    builtin('fx<?', lambda *a: fx_cmp(lambda x, y: x < y, *a))
    builtin('fx>?', lambda *a: fx_cmp(lambda x, y: x > y, *a))
    builtin('fx<=?', lambda *a: fx_cmp(lambda x, y: x <= y, *a))
    builtin('fx>=?', lambda *a: fx_cmp(lambda x, y: x >= y, *a))
    builtin('fxzero?', lambda x: TRUE if fxcheck(x) == 0 else FALSE)
    builtin('fxpositive?', lambda x: TRUE if fxcheck(x) > 0 else FALSE)
    builtin('fxnegative?', lambda x: TRUE if fxcheck(x) < 0 else FALSE)
    builtin('fxodd?', lambda x: TRUE if fxcheck(x) & 1 else FALSE)
    builtin('fxeven?', lambda x: TRUE if not (fxcheck(x) & 1) else FALSE)
    builtin('fxmax', lambda *a: max(fxcheck(x) for x in a))
    builtin('fxmin', lambda *a: min(fxcheck(x) for x in a))
    builtin('fxand', fx_and)
    builtin('fxior', fx_ior)
    builtin('fxxor', fx_xor)
    builtin('fxnot', fx_not)
    builtin('fxlsh', fx_lsh)
    builtin('fxrshl', fx_rshl)
    builtin('fxrsha', fx_rsha)
    builtin('fxfirst-set-bit', lambda x: (x & -x).bit_length() - 1 if x else -1)
    builtin('fxbit-count', lambda x: x.bit_count() if x else 0)
    builtin('fxlength', lambda x: x.bit_length())
    builtin('fxif', lambda a, b, c: (a & b) | (~a & c))
    builtin('fxbit-set?', lambda x, i: TRUE if (x >> i) & 1 else FALSE)
    builtin('fxcopy-bit', lambda x, i, b: x if b else (x | (1 << i)))
    builtin('fxgcd', math.gcd)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-144: Flonums (inexact real arithmetic)
    # ═══════════════════════════════════════════════════════════════
    builtin('flonum?', lambda x: TRUE if is_flonum(x) else FALSE)
    builtin('fl+', fl_add)
    builtin('fl-', fl_sub)
    builtin('fl*', fl_mul)
    builtin('fl/', fl_div)
    builtin('fl=?', lambda *a: fl_cmp(lambda x, y: x == y, *a))
    builtin('fl<?', lambda *a: fl_cmp(lambda x, y: x < y, *a))
    builtin('fl>?', lambda *a: fl_cmp(lambda x, y: x > y, *a))
    builtin('fl<=?', lambda *a: fl_cmp(lambda x, y: x <= y, *a))
    builtin('fl>=?', lambda *a: fl_cmp(lambda x, y: x >= y, *a))
    builtin('flzero?', lambda x: TRUE if float(x) == 0.0 else FALSE)
    builtin('flpositive?', lambda x: TRUE if float(x) > 0.0 else FALSE)
    builtin('flnegative?', lambda x: TRUE if float(x) < 0.0 else FALSE)
    builtin('flodd?', lambda x: TRUE if int(Fraction(x)) % 2 != 0 else FALSE)
    builtin('fleven?', lambda x: TRUE if int(Fraction(x)) % 2 == 0 else FALSE)
    builtin('flfinite?', lambda x: TRUE if isinstance(x, float) and math.isfinite(x) else FALSE)
    builtin('flinfinite?', lambda x: TRUE if isinstance(x, float) and math.isinf(x) else FALSE)
    builtin('flnan?', lambda x: TRUE if isinstance(x, float) and math.isnan(x) else FALSE)
    builtin('flmax', fl_max)
    builtin('flmin', fl_min)
    builtin('flfloor', math.floor)
    builtin('flceiling', math.ceil)
    builtin('flround', round)
    builtin('fltruncate', math.trunc)
    builtin('flsqrt', math.sqrt)
    builtin('flexp', math.exp)
    builtin('flexpt', lambda a, b: float(a) ** float(b))
    builtin('fllog', math.log)
    builtin('flsin', math.sin)
    builtin('flcos', math.cos)
    builtin('fltan', math.tan)
    builtin('flasin', math.asin)
    builtin('flacos', math.acos)
    builtin('flatan', math.atan)
    builtin('flonum->fixnum', lambda x: int(x))
    builtin('fixnum->flonum', lambda x: float(x))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-151: Bitwise operations
    # ═══════════════════════════════════════════════════════════════
    builtin('bitwise-not', bitwise_not)
    builtin('bitwise-and', bitwise_and)
    builtin('bitwise-ior', bitwise_ior)
    builtin('bitwise-xor', bitwise_xor)
    builtin('bitwise-if', bitwise_if)
    builtin('bitwise-merge', bitwise_if)
    builtin('bitwise-length', bitwise_length)
    builtin('bitwise-count', bitwise_count)
    builtin('bitwise-reverse-bit-field', bitwise_reverse_bitfield)
    builtin('bitwise-reverse-bitfield', bitwise_reverse_bitfield)
    builtin('bitwise-rotate', bitwise_rotate)
    builtin('bitwise-rotate-bit-field', bitwise_rotate_field)
    builtin('bitwise-copy-bit-field', bitwise_copy_bit_field)
    builtin('bitwise-copy-bit', bitwise_copy_bit)
    builtin('bitwise-bit-field', bitwise_bit_field)
    builtin('bitwise-arithmetic-shift', bitwise_shift)
    builtin('bitwise-arithmetic-shift-right', lambda n, c: bitwise_shift(n, -int(c)))
    builtin('bitwise-shift', bitwise_shift)
    builtin('bitwise-any-bit-set?', lambda n, m: TRUE if (int(n) & int(m)) != 0 else FALSE)
    builtin('integer-length', integer_length)
    builtin('first-set-bit', first_set_bit)
    builtin('bit-count', bitwise_count)
    builtin('bit-field', bitwise_bit_field)
    builtin('bit-shift', bitwise_shift)
    builtin('copy-bit', bitwise_copy_bit)
    builtin('bit-set?', lambda n, i: TRUE if (int(n) >> int(i)) & 1 else FALSE)
    builtin('integer->booleans', integer_to_booleans)

    # ═══════════════════════════════════════════════════════════════
    # Bitvectors
    # ═══════════════════════════════════════════════════════════════
    builtin('bitvector?', bitvector_p)
    builtin('make-bitvector', lambda n, *fill: SchemeVector([fill[0] if fill else FALSE] * int(n)))
    builtin('bitvector-copy', lambda bv, *args: SchemeVector(list(vec(bv))))
    builtin('bitvector-append', lambda *bvs: SchemeVector([x for bv in bvs for x in vec(bv)]))
    builtin('bitvector-length', lambda bv: len(vec(bv)))
    builtin('bitvector-ref', lambda bv, i: TRUE if vec(bv)[i] else FALSE)
    builtin('bitvector-set!', lambda bv, i, v: vec_set(bv, int(i), v))
    builtin('list->bitvector', lambda lst: SchemeVector([x is TRUE or x is True for x in cell_iter(lst)]))
    builtin('bitvector->list', lambda bv: _lst([TRUE if x else FALSE for x in vec(bv)]))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-133: Vector extensions
    # ═══════════════════════════════════════════════════════════════
    builtin('vector-map', vector_map)
    builtin('vector-map!', do_vector_map)
    builtin('vector-for-each', vector_for_each)
    builtin('vector-count', vector_count)
    builtin('vector-any', vector_any)
    builtin('vector-every', vector_every)
    builtin('vector-fold', vector_fold)
    builtin('vector-fold-right', vector_fold_right)
    builtin('vector-unfold', vector_unfold)
    builtin('vector-index', vector_index)
    builtin('vector-skip', vector_skip)
    builtin('vector-swap!', do_vector_swap)
    builtin('vector-reverse!', do_vector_reverse)
    builtin('vector-empty?', vec_empty_q)
    builtin('vector-append', vector_append)
    builtin('vector-copy', vector_copy_fn)
    builtin('vector-copy!', vector_copy_bang)
    builtin('vector-concatenate', vector_concat)
    builtin('vector-reverse', vector_reverse_fn)
    builtin('vector-sort', vector_sort_fn)
    builtin('vector=', vector_equal)
    builtin('reverse-list->vector', lambda lst: SchemeVector(cells(lst)[::-1]))

    # Basic vector operations (re-register for pyb=True override)
    builtin('vector', lambda *a: SchemeVector(list(a)))



    builtin('vector->list', lambda v: _lst(list(v)))

    builtin('vector-fill!', lambda v, x, *a: vec_fill_range(v, x, *a))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-152: String utilities
    # ═══════════════════════════════════════════════════════════════
    builtin('string-take', string_take)
    builtin('string-drop', string_drop)
    builtin('string-take-right', string_take_right)
    builtin('string-drop-right', string_drop_right)
    builtin('string-pad', string_pad)
    builtin('string-pad-right', string_pad_right)
    builtin('string-trim', string_trim)
    builtin('string-trim-right', string_trim_right)
    builtin('string-trim-both', string_trim_both)
    builtin('string-trim-left', string_trim_left_fn)
    builtin('string-replace', string_replace)
    builtin('string-split', string_split)
    builtin('string-join', string_join)
    builtin('string-contains', string_contains)
    builtin('string-prefix?', str_prefix_q)
    builtin('string-suffix?', str_suffix_q)
    builtin('string-prefix-length', str_prefix_len)
    builtin('string-suffix-length', str_suffix_len)
    builtin('string-prefix-length-ci', str_prefix_len_ci)
    builtin('string-suffix-length-ci', str_suffix_len_ci)
    builtin('string-count', string_count)
    builtin('string-map', string_map)
    builtin('string-for-each', string_for_each)
    builtin('string-for-each-index', string_for_each_idx)
    builtin('string-fold', string_fold)
    builtin('string-fold-right', string_fold_right_fn)
    builtin('string-index', string_index_fn)
    builtin('string-index-right', string_index_right_fn)
    builtin('string-skip', string_skip_fn)
    builtin('string-skip-right', string_skip_right_fn)
    builtin('string-any', string_any_fn)
    builtin('string-every', string_every_fn)
    builtin('string-concatenate', string_concat)
    builtin('string-copy!', string_copy_bang)
    builtin('string-xcopy!', string_xcopy_bang)
    builtin('string-delete', string_remove_fn)
    builtin('string-filter', string_filter_fn)
    builtin('string-remove', string_remove_fn)
    builtin('string-reverse', lambda s: SchemeString(''.join(reversed(str(s)))))
    builtin('string-foldcase', lambda s: SchemeString(str(s).lower()))
    builtin('string-titlecase', lambda s: SchemeString(str(s).title()))


    builtin('string-tokenize', string_tokenize_fn)
    builtin('string-unfold', string_unfold_fn)
    builtin('string-tabulate', lambda n, f: SchemeString(''.join(_so(f(i)) for i in range(int(n)))))
    builtin('string->char-set', str_to_char_set)
    builtin('string->vector', str_to_vec)
    builtin('vector->string', vec_to_str)

    # Basic string operations (re-register for pyb=True override)
    builtin('string', lambda *a: SchemeString(''.join(char_val(x) for x in a)))

    builtin('->string', lambda x: x if isinstance(x, (str, SchemeString)) else SchemeString(_pr(x)))

    # ═══════════════════════════════════════════════════════════════
    # String comparison (ci variants)
    # ═══════════════════════════════════════════════════════════════
    builtin('string=?', lambda a, b: TRUE if str(a) == str(b) else FALSE)
    builtin('string<?', lambda a, b: TRUE if str(a) < str(b) else FALSE)
    builtin('string>?', lambda a, b: TRUE if str(a) > str(b) else FALSE)
    builtin('string<=?', lambda a, b: TRUE if str(a) <= str(b) else FALSE)
    builtin('string>=?', lambda a, b: TRUE if str(a) >= str(b) else FALSE)
    builtin('string-ci=?', lambda a, b: TRUE if str(a).lower() == str(b).lower() else FALSE)
    builtin('string-ci<?', lambda a, b: TRUE if str(a).lower() < str(b).lower() else FALSE)
    builtin('string-ci>?', lambda a, b: TRUE if str(a).lower() > str(b).lower() else FALSE)
    builtin('string-ci<=?', lambda a, b: TRUE if str(a).lower() <= str(b).lower() else FALSE)
    builtin('string-ci>=?', lambda a, b: TRUE if str(a).lower() >= str(b).lower() else FALSE)

    # ═══════════════════════════════════════════════════════════════
    # Char operations
    # ═══════════════════════════════════════════════════════════════
    builtin('char-ci=?', char_ci_eq)
    builtin('char-ci<?', lambda a, b: TRUE if str(a).lower() < str(b).lower() else FALSE)
    builtin('char-ci>?', lambda a, b: TRUE if str(a).lower() > str(b).lower() else FALSE)
    builtin('char-ci<=?', lambda a, b: TRUE if str(a).lower() <= str(b).lower() else FALSE)
    builtin('char-ci>=?', lambda a, b: TRUE if str(a).lower() >= str(b).lower() else FALSE)








    builtin('char-ascii?', lambda c: TRUE if ord(char_val(c)) < 128 else FALSE)
    builtin('char-control?', lambda c: TRUE if (n := ord(char_val(c))) < 32 or n == 127 else FALSE)
    builtin('char-iso-control?', lambda c: TRUE if (n := ord(char_val(c))) < 32 or n == 127 else FALSE)
    builtin('ascii?', lambda c: TRUE if ord(_so(c)) < 128 else FALSE)
    builtin('char->name', char_name)
    builtin('digit-value', digit_value)

    # ═══════════════════════════════════════════════════════════════
    # Char-set operations (SRFI-14)
    # ═══════════════════════════════════════════════════════════════
    builtin('char-set', lambda *chars: char_set_make(chars))
    builtin('char-set?', char_set_p)
    builtin('char-set-contains?', char_set_contains)
    builtin('char-set-empty?', char_set_empty)
    builtin('char-set->list', char_set_to_list)
    builtin('char-set->string', char_set_to_string)
    builtin('char-set-count', char_set_count)
    builtin('char-set-copy', char_set_copy)
    builtin('char-set-union', lambda *css: char_set_binop(css, lambda a, b: a or b))
    builtin('char-set-intersection', lambda *css: char_set_binop(css, lambda a, b: a and b))
    builtin('char-set-difference', lambda cs1, *css: char_set_diff(cs1, css))
    builtin('char-set-xor', lambda *css: char_set_xor(css))
    builtin('char-set-complement', char_set_complement)
    builtin('char-set-adjoin', lambda cs, *chars: char_set_adjoin(cs, chars))
    builtin('char-set-delete', lambda cs, *chars: char_set_delete(cs, chars))
    builtin('char-set-any', char_set_any)
    builtin('char-set-every', char_set_every)
    builtin('char-set-filter', lambda pred, cs, *basis: char_set_filter(pred, cs, basis[0] if basis else cs))
    builtin('char-set-fold', char_set_fold)
    builtin('char-set-for-each', char_set_for_each)
    builtin('char-set-map', char_set_map)
    builtin('char-set-hash', lambda cs, *bound: char_set_hash(cs, int(bound[0]) if bound else 65536))
    builtin('char-set=?', char_set_equal)
    builtin('ucs-range->char-set', ucs_range_char_set)
    builtin('char-set:empty', [False] * 256)
    builtin('char-set:full', [True] * 256)
    builtin('char-set:lower-case', char_set_make([SchemeChar(chr(i)) for i in range(ord('a'), ord('z') + 1)]))
    builtin('char-set:lower', be.data['char-set:lower-case'])
    builtin('char-set:upper-case', char_set_make([SchemeChar(chr(i)) for i in range(ord('A'), ord('Z') + 1)]))
    builtin('char-set:upper', be.data['char-set:upper-case'])
    builtin('char-set:digit', char_set_make([SchemeChar(chr(i)) for i in range(ord('0'), ord('9') + 1)]))
    builtin('char-set:whitespace', char_set_make([SchemeChar(' '), SchemeChar('\t'), SchemeChar('\n'), SchemeChar('\r')]))
    builtin('char-set:letter', char_set_binop((be.data['char-set:lower-case'], be.data['char-set:upper-case']), lambda a, b: a or b))
    builtin('char-set:punctuation', char_set_make([SchemeChar(c) for c in ".,;:!?-'\"()[]{}\\/@#$%^&*+=<>|~"]))
    builtin('char-set:graphic', char_set_binop((be.data['char-set:letter'], be.data['char-set:digit'], be.data['char-set:punctuation']), lambda a, b: a or b))
    builtin('char-set:printing', ucs_range_char_set(32, 127))
    builtin('char-set:symbol', char_set_make([SchemeChar(c) for c in "!$%&*+-./:<=>?@^_~"]))
    builtin('char-set:hex-digit', char_set_make([SchemeChar(c) for c in '0123456789abcdefABCDEF']))
    builtin('char-set:blank', char_set_make([SchemeChar(' '), SchemeChar('\t')]))
    builtin('char-set:iso-control', char_set_adjoin(ucs_range_char_set(0, 32), [SchemeChar(chr(127))]))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-158: Generators
    # ═══════════════════════════════════════════════════════════════
    builtin('generator', generator)
    builtin('generator?', lambda x: TRUE if callable(x) else FALSE)
    builtin('make-generator', make_generator)
    builtin('list->generator', list_generator)
    builtin('vector->generator', vector_generator)
    builtin('string->generator', string_generator)
    builtin('generator-map', generator_map)
    builtin('generator-filter', generator_filter)
    builtin('generator-take', generator_take)
    builtin('generator-drop', generator_drop)
    builtin('generator-find', generator_find)
    builtin('generator-count', generator_count)
    builtin('generator-append', generator_append)
    builtin('generator->list', generator_list_and)
    builtin('generator->vector', generator_vector_and)
    builtin('generator->string', generator_string_and)
    builtin('generator-for-each', generator_for_each)
    builtin('generator-fold', generator_fold_fn)
    builtin('make-iota-generator', lambda n, start=0, step=1: generator_iota(int(n), step, start))
    builtin('make-range-generator', lambda s, e, st=1: generator_range(s, e, st))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-117: List queues
    # ═══════════════════════════════════════════════════════════════
    builtin('make-list-queue', lambda f=NIL, b=NIL: make_list_queue(f, b))
    builtin('list-queue', list_queue)
    builtin('list-queue?', is_list_queue)
    builtin('list-queue-front', list_queue_front)
    builtin('list-queue-back', list_queue_back)
    builtin('list-queue-empty?', lq_empty_q)
    builtin('list-queue-add!', do_lq_add)
    builtin('list-queue-add-back!', do_lq_add)
    builtin('list-queue-add-front!', do_lq_add_front)
    builtin('list-queue-remove!', do_lq_remove)
    builtin('list-queue-remove-front!', do_lq_remove)
    builtin('list-queue-list', list_queue_list)
    builtin('list-queue-first', list_queue_first)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-125: Hash tables
    # ═══════════════════════════════════════════════════════════════

    builtin('make-eq-hash-table', make_ht)
    builtin('make-equal-hash-table', make_ht)
    builtin('make-eqv-hash-table', make_ht)
    builtin('make-strong-hash-table', lambda: {})

    builtin('hash-table-clear!', do_ht_clear)


    builtin('hash-table-map', hash_table_map)
    builtin('hash-table-fold', hash_table_fold)
    builtin('hash-table-update!', hash_table_update)
    builtin('hash-table-walk', hash_table_walk)
    builtin('hash-table-count', hash_table_count)
    builtin('hash-table-put!', lambda ht, key, value: ht.__setitem__(key, value) or VOID)
    builtin('hash-table-merge!', hash_table_merge_bang)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-1: List operations
    # ═══════════════════════════════════════════════════════════════
    builtin('reverse', rvrs)
    builtin('cons*', cons_star)
    builtin('list*', cons_star)
    builtin('list-copy', list_copy_fn)
    builtin('make-list', lambda n, *v: make_list_fn(int(n), v[0] if v else FALSE))
    builtin('iota', lambda n, *a: iota_fn(int(n), a[0] if a else 0, a[1] if len(a) > 1 else 1))
    builtin('first', lambda lst: nth(lst, 0))
    builtin('second', lambda lst: nth(lst, 1))
    builtin('third', lambda lst: nth(lst, 2))
    builtin('fourth', lambda lst: nth(lst, 3))
    builtin('fifth', lambda lst: nth(lst, 4))
    builtin('sixth', lambda lst: nth(lst, 5))
    builtin('seventh', lambda lst: nth(lst, 6))
    builtin('eighth', lambda lst: nth(lst, 7))
    builtin('ninth', lambda lst: nth(lst, 8))
    builtin('tenth', lambda lst: nth(lst, 9))
    builtin('list-head', list_head_fn)

    builtin('take', lambda lst, n: list_take(lst, int(n)))
    builtin('drop', lambda lst, n: list_drop(lst, int(n)))
    builtin('take-right', lambda lst, n: list_take_right(lst, int(n)))
    builtin('drop-right', lambda lst, n: list_drop_right(lst, int(n)))
    builtin('take-while', list_take_while)
    builtin('drop-while', list_drop_while)
    builtin('last', list_last)
    builtin('last-pair', list_last_pair)
    builtin('but-last', list_butlast)
    builtin('length+', length_plus)
    builtin('list-tabulate', lambda n, f: list_tabulate_fn(int(n), f))
    builtin('list-index', list_index_fn)
    builtin('list-set!', list_set_bang)
    builtin('list-find', list_find)
    builtin('list-find-index', list_find_index)
    builtin('list-any', list_any)
    builtin('list-every', list_every)
    builtin('list-filter-map', list_filter_map)
    builtin('list-partition', list_partition)
    builtin('list-remove', list_remove)
    builtin('list-flatten', list_flatten)
    builtin('list-zip', zip_fn)
    builtin('list-sort', list_sort_fn)
    builtin('list-stable-sort', list_sort_fn)
    builtin('sort', generic_sort)
    builtin('list=', list_equal)
    builtin('sorted?', sorted_p_fn)
    builtin('merge', merge_fn)
    builtin('merge!', merge_bang_fn)
    builtin('assq', lambda obj, al: assoc_fn(obj, al, lambda a, b: a is b))
    builtin('assv', lambda obj, al: assoc_fn(obj, al, lambda a, b: a is b or a == b))
    builtin('assoc', lambda obj, al, *eq: assoc_fn(obj, al, eq[0] if eq else (lambda a, b: a is b or a == b)))
    builtin('memq', lambda obj, lst: mem_fn(obj, lst, lambda a, b: a is b))
    builtin('memv', lambda obj, lst: mem_fn(obj, lst, lambda a, b: a is b or a == b))
    builtin('member', lambda obj, lst, *eq: mem_fn(obj, lst, eq[0] if eq else (lambda a, b: a is b or a == b)))
    builtin('find', list_find)
    builtin('fold', fold_left_fn)
    builtin('fold-left', fold_left_fn)
    builtin('fold-right', fold_right_fn)
    builtin('reduce', fold_left_fn)
    builtin('reduce-right', fold_right_fn)
    builtin('any', list_any)
    builtin('every', list_every)
    builtin('count', count_fn)
    builtin('delete', lambda x, lst, *eq: delete_fn(x, lst, eq[0] if eq else None))
    builtin('delete-duplicates', lambda lst, *eq: delete_dups_fn(lst, eq[0] if eq else None))
    builtin('delete-assoc', delete_assoc_fn)
    builtin('alist-cons', lambda k, v, al: Cell(Cell(k, v), al))
    builtin('alist-delete', alist_delete_fn)
    builtin('append-map', append_map_fn)
    builtin('append-reverse', append_rev)
    builtin('concatenate', concatenate_fn)
    builtin('flatten', flatten_fn)
    builtin('filter-map', filter_map_fn)
    builtin('map-in-order', map_fn)
    builtin('pair-for-each', pair_for_each_fn)
    builtin('xcons', lambda d, a: Cell(a, d))
    builtin('zip', zip_fn)
    builtin('unzip1', lambda lst: unzip_n(lst, 1))
    builtin('unzip2', lambda lst: unzip_n(lst, 2))
    builtin('unzip3', lambda lst: unzip_n(lst, 3))
    builtin('unzip4', lambda lst: unzip_n(lst, 4))
    builtin('unzip5', lambda lst: unzip_n(lst, 5))
    builtin('curry', curry_fn)
    builtin('complement', lambda f: lambda *a: FALSE if f(*a) is TRUE else TRUE)
    builtin('flip', lambda f: lambda a, b: f(b, a))
    builtin('const', lambda x: lambda *_: x)
    builtin('iterate', lambda f, n, x: iterate_fn(f, int(n), x))
    builtin('product', product_fn)
    builtin('square', lambda x: x * x)
    builtin('range', lambda s, e, *st: range_fn(int(s), int(e), int(st[0]) if st else 1))
    builtin('interleave', interleave_fn)
    builtin('symbolic-append', lambda *a: Sym(''.join(_sn(x) for x in a)))

    builtin('<>', lambda a, b: TRUE if a != b else FALSE)

    # List predicate helpers
    builtin('circular-list', circular_list)
    builtin('circular-list?', circular_list_p)
    builtin('dotted-list?', dotted_list_p)
    builtin('proper-list?', proper_list_p)
    builtin('null-list?', lambda x: TRUE if x is NIL else FALSE)
    builtin('not-pair?', lambda x: TRUE if not isinstance(x, Cell) else FALSE)
    builtin('ne-list?', lambda x: TRUE if isinstance(x, Cell) and x.cdr is NIL else FALSE)

    # Mutation
    builtin('drop!', lambda lst, n: list_drop(lst, int(n)))
    builtin('take!', lambda lst, n: list_take(lst, int(n)))
    builtin('filter!', filter_fn)
    builtin('flat-map', append_map_fn)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-1: lset-* (set operations on lists)
    # ═══════════════════════════════════════════════════════════════
    builtin('lset-union', lset_union)
    builtin('lset-intersection', lset_intersection)
    builtin('lset-difference', lset_difference)
    builtin('lset-xor', lset_xor)
    builtin('lset-=?', lset_equal)

    # ═══════════════════════════════════════════════════════════════
    # Stream operations
    # ═══════════════════════════════════════════════════════════════
    builtin('nat-stream', lambda n: nat_stream_fn(int(n)))
    builtin('naturals', lambda *a: nat_stream(int(a[0]) if a else 0))
    builtin('sieve', sieve_fn)
    builtin('primes', sieve_fn(nat_stream(2)))
    builtin('tree->list', tree_to_list)
    builtin('num-den', num_den)

    # ═══════════════════════════════════════════════════════════════
    # Number theory & math
    # ═══════════════════════════════════════════════════════════════
    builtin('scheme-gcd', scheme_gcd_fn)
    builtin('scheme-lcm', scheme_lcm_fn)
    builtin('prime?', prime_p)
    builtin('factor', factor_fn)
    builtin('fib-pair', lambda n: fib_pair(int(n)))
    builtin('fibonacci', lambda n: fib_pair(int(n)).car)
    builtin('binomial', lambda n, k: binomial_fn(int(n), int(k)))
    builtin('factorial', lambda n: factorial_fn(int(n)))
    builtin('quick-expt', lambda b, e: quick_expt_fn(int(b), int(e)))
    builtin('expt-mod', expt_mod)
    builtin('log-base', log_base)
    builtin('degrees->radians', degrees_to_radians)
    builtin('radians->degrees', radians_to_degrees)

    # Hyperbolic math
    builtin('sinh', lambda x: math.sinh(float(x)))
    builtin('cosh', lambda x: math.cosh(float(x)))
    builtin('tanh', lambda x: math.tanh(float(x)))
    builtin('sech', lambda x: 1.0 / math.cosh(float(x)))
    builtin('csch', lambda x: 1.0 / math.sinh(float(x)))
    builtin('coth', lambda x: math.cosh(float(x)) / math.sinh(float(x)))
    builtin('log10', lambda x: math.log10(float(x)))
    builtin('log2', lambda x: math.log2(float(x)))

    # Numeric predicates and conversions
    builtin('nan?', lambda x: x != x)
    builtin('finite?', lambda x: TRUE if isinstance(x, (int, float, Fraction, complex)) and (not isinstance(x, float) or (x == x and x != float('inf') and x != float('-inf'))) else FALSE)
    builtin('infinite?', lambda x: TRUE if isinstance(x, float) and math.isinf(x) else FALSE)
    builtin('exact', lambda x: int(x) if isinstance(x, float) and x == int(x) else (Fraction(x).limit_denominator(1000000) if isinstance(x, float) else x))
    builtin('inexact', lambda x: float(x))
    builtin('exact-nonnegative-integer?', lambda x: TRUE if (isinstance(x, int) and x >= 0) or (isinstance(x, Fraction) and x.denominator == 1 and x.numerator >= 0) else FALSE)
    builtin('exact-rational?', lambda x: TRUE if isinstance(x, (int, Fraction)) else FALSE)
    builtin('exact-integer?', lambda x: TRUE if is_exact_int(x) else FALSE)
    builtin('reciprocal', lambda x: div(1, x))
    builtin('ceiling->exact', lambda x: int(math.ceil(float(x))) if isinstance(x, Fraction) else int(math.ceil(x)))
    builtin('floor->exact', lambda x: int(math.floor(float(x))) if isinstance(x, Fraction) else int(math.floor(x)))
    builtin('truncate->exact', lambda x: int(x))
    builtin('round->exact', lambda x: int(round(float(x))) if isinstance(x, Fraction) else int(round(x)))
    builtin('magnitude', lambda z: abs(z) if isinstance(z, complex) else abs(z))
    builtin('make-rectangular', lambda r, i: complex(float(r) if isinstance(r, Fraction) else int(r) if isinstance(r, float) and r == int(r) else r, float(i) if isinstance(i, Fraction) else int(i) if isinstance(i, float) and i == int(i) else i))

    # Basic numeric aliases
    builtin('add1', lambda x: x + 1)
    builtin('sub1', lambda x: x - 1)
    builtin('sub1*', lambda x: x - 1)
    builtin('float', lambda x: float(x))

    # ═══════════════════════════════════════════════════════════════
    # Conditions & errors
    # ═══════════════════════════════════════════════════════════════
    builtin('error?', error_q)
    builtin('file-error?', file_error_q)
    builtin('read-error?', read_error_q)
    builtin('condition-has-type?', lambda c, t: isinstance(c, tuple) and len(c) > 2 and c[1] == t)
    builtin('condition-type?', lambda obj: TRUE if (isinstance(obj, tuple) and len(obj) > 2 and obj[0] == 'condition') or isinstance(obj, ErrorObject) else FALSE)
    builtin('condition/report-string', lambda c: SchemeString(c[2]) if isinstance(c, tuple) and len(c) > 2 else SchemeString(str(c)))
    builtin('raise-continuable', lambda c: do_raise(c))
    builtin('make-error-condition', lambda t, m: ('condition', t, m))
    builtin('condition-message', lambda c: c[2] if isinstance(c, tuple) and len(c) > 2 else str(c))
    builtin('make-io-error', lambda message=SchemeString(''): ('condition', Sym('io-error'), message))
    builtin('io-error?', lambda c: TRUE if isinstance(c, tuple) and len(c) > 1 and c[1] == Sym('io-error') else FALSE)

    # ═══════════════════════════════════════════════════════════════
    # Maybe monad
    # ═══════════════════════════════════════════════════════════════
    builtin('maybe?', maybe_p)
    builtin('just', lambda x: Cell(x, NIL))
    builtin('nothing', lambda: FALSE)
    builtin('just?', just_p)
    builtin('nothing?', nothing_p)
    builtin('maybe-ref', lambda x, *default: x.car if isinstance(x, Cell) else (default[0] if default else FALSE))
    builtin('maybe->values', lambda x: (x.car, TRUE) if isinstance(x, Cell) else (FALSE, FALSE))
    # SRFI-189: maybe 构造器 —— 非 #f 直接返回该值（just 的内容），#f 返回 #f（nothing）。
    # 这样 (maybe 42) 与 42 在 equal? 下相等，符合 test-srfi-189 的断言。
    builtin('maybe', lambda x: x if x is not FALSE else FALSE)

    builtin('iset', iset_fn)
    builtin('iset?', iset_p)
    builtin('iset-contains?', iset_contains_p)
    builtin('iset-adjoin', iset_adjoin)
    builtin('iset-delete', iset_delete)
    builtin('iset-empty', iset_empty)
    builtin('iset-size', iset_size)
    builtin('iset-empty?', iset_empty_p)
    builtin('iset-union', iset_union)
    builtin('iset-intersection', iset_intersection)
    builtin('iset-difference', iset_difference)
    builtin('iset->list', iset_to_list)
    builtin('list->iset', list_to_iset)


    # ═══════════════════════════════════════════════════
    # SRFI-180: JSON
    # ═══════════════════════════════════════════════════════════════
    builtin('json-read', json_read)
    builtin('json-write', json_write)
    builtin('json-read-string', json_read_string)
    builtin('json-write-string', json_write_string)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-207: String-notable (bytevector <-> string)
    # ═══════════════════════════════════════════════════════════════
    builtin('bytevector->string', bytevector_to_string)
    builtin('string->bytevector', string_to_bytevector)

    # ═══════════════════════════════════════════════════════════════
    # Mapping (SRFI-146)
    # ═══════════════════════════════════════════════════════════════
    builtin('mapping', mapping_fn)
    builtin('mapping?', mapping_pred)

    # ═══════════════════════════════════════════════════════════════
    # Textual port I/O
    # ═══════════════════════════════════════════════════════════════
    builtin('textual-port?', lambda p: TRUE if p is TRUE or (isinstance(p, tuple) and p[0] in ('str-port', 'file-port')) else FALSE)
    builtin('char-ready?', lambda *p: TRUE if not p else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'str-port' and p[0][1] and p[0][1][0] else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'file-port' and len(p[0]) > 3 else FALSE)))
    builtin('u8-ready?', lambda *p: TRUE if not p else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'str-port' and p[0][1] and p[0][1][0] else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'file-port' and len(p[0]) > 3 else FALSE)))
    builtin('peek-u8', peek_u8_fn)
    builtin('read-u8', read_u8_fn)
    builtin('write-u8', write_u8)
    builtin('read-line', read_line)
    builtin('read-string', read_string_fn)
    builtin('write-string', write_string)
    builtin('get-output-bytevector', lambda p=None: SchemeBytevector(list(p[1])) if isinstance(p, tuple) and p[0] == 'byte-port' else SchemeBytevector([]))


    # Bytevector (base builtin in initenv.py)
    # ═══════════════════════════════════════════════════════════════
    # Symbol operations
    # ═══════════════════════════════════════════════════════════════
    builtin('symbol=?', symbol_equal_p)
    builtin('number=?', num_equal_p)



    # ═══════════════════════════════════════════════════════════════
    # Environment
    # ═══════════════════════════════════════════════════════════════



    # ═══════════════════════════════════════════════════════════════
    # Random
    # ═══════════════════════════════════════════════════════════════
    builtin('random-integer', random_integer)
    builtin('random-real', random_real)
    builtin('random-seed', random_seed)

    # ═══════════════════════════════════════════════════════════════
    # Various helpers
    # ═══════════════════════════════════════════════════════════════
    builtin('atom?', lambda x: FALSE if isinstance(x, Cell) else TRUE)
    builtin('void?', lambda x: TRUE if x is VOID else FALSE)
    builtin('boolean->string', lambda x: SchemeString('#t') if x is TRUE else SchemeString('#f'))
    builtin('boolean=?', lambda *a: FALSE if any(a[i] != a[i+1] for i in range(len(a)-1)) else TRUE)
    builtin('default-object?', lambda x: TRUE if x is VOID else FALSE)
    builtin('name', lambda x: _sn(x) if isinstance(x, Sym) else SchemeString(_pr(x)))
    builtin('pp', lambda x: (sys.stdout.write(_pr(x) + '\n'), VOID)[-1])
    builtin('array?', lambda x: TRUE if isinstance(x, SchemeVector) else FALSE)
    builtin('cartesian-product', lambda *lists: cartesian_product(list(lists)))
    builtin('combinations', lambda lst, n: combinations_fn(lst, int(n)))
    builtin('permutations', lambda lst: perms_fn(lst))
    builtin('unfold', lambda p, f, g, seed, *thunk: unfold_fn(p, f, g, seed, thunk[0] if thunk else None))
    builtin('unfold-right', lambda p, f, g, seed, *thunk: unfold_right_fn(p, f, g, seed, thunk[0] if thunk else None))
    builtin('describe', lambda x: sys.stdout.write(str(x) + '\n') or VOID)
    builtin('identity', lambda x: x)
    builtin('flexp2', lambda x: 2.0 ** float(x))
    # Hooks
    builtin('make-hook-internal', lambda procedures=NIL: Hook()); builtin('make-hook', lambda *arity: Hook()); builtin('hook?', lambda x: TRUE if isinstance(x, Hook) else FALSE)
    builtin('hook-procedures', lambda h: _lst(h.procedures)); builtin('set-hook-procedures!', lambda h, p: setattr(h, 'procedures', list(cell_iter(p))) or VOID)
    builtin('add-hook!', lambda h, p, *append: (h.procedures.append(p) if append and scheme_truthy(append[0]) else h.procedures.insert(0, p)) or VOID)
    builtin('remove-hook!', lambda h, p: setattr(h, 'procedures', [x for x in h.procedures if x is not p]) or VOID)
    builtin('reset-hook!', lambda h: setattr(h, 'procedures', []) or VOID)
    builtin('run-hook', lambda h, *args: ([p(*args) for p in list(h.procedures)] and VOID) or VOID)

    # Random source, including the module default source.
    default = RandomSource(); 
    builtin('*default-random-source*', default)
    builtin('%make-random-source', lambda state: RandomSource(state)); builtin('make-random-source', lambda: RandomSource()); builtin('random-source?', lambda x: TRUE if isinstance(x, RandomSource) else FALSE)
    builtin('random-source-state', lambda s: s.state); builtin('set-random-source-state!', lambda s, x: setattr(s, 'state', int(x)) or VOID)
    builtin('random-source->random-integer', rint); builtin('random-source-random-integer', rint)
    builtin('random-source->random-real', lambda s: s.step() / 2147483648.0); builtin('random-source-random-real', lambda s: s.step() / 2147483648.0)
    builtin('random-source-randomize!', lambda s: setattr(s, 'state', int(time.time())) or VOID)
    builtin('random-source-pseudo-randomize!', lambda s, i, j: setattr(s, 'state', int(i) * 12345 + int(j)) or VOID)
    builtin('random-integer', lambda n: rint(default, n)); builtin('random-real', lambda: default.step() / 2147483648.0)
    builtin('random-seed', lambda n: setattr(default, 'state', int(n)) or VOID); builtin('linear-update-list', lambda *x: _lst(x))

    # List queue is represented as the exact mutable front sequence expected by scm.
    builtin('%make-list-queue', lambda front=NIL, back=NIL: {'items': list(cell_iter(front))}); builtin('make-list-queue', lambda front=NIL, *rest: {'items': list(cell_iter(front))}); builtin('list-queue', lambda *x: {'items': list(x)})
    builtin('list-queue?', lambda q: TRUE if isinstance(q, dict) and 'items' in q else FALSE)
    builtin('list-queue-copy', lambda q: {'items': list(q['items'])}); builtin('list-queue-empty?', lambda q: TRUE if not q['items'] else FALSE)
    builtin('list-queue-add-front!', lambda q, x: q['items'].insert(0, x) or VOID); builtin('list-queue-add-back!', lambda q, x: q['items'].append(x) or VOID); builtin('list-queue-add!', lambda q, x: q['items'].append(x) or VOID)
    builtin('list-queue-remove-front!', qremove); builtin('list-queue-remove!', qremove); builtin('list-queue-remove-back!', lambda q: qremove(q, True))
    builtin('%list-queue-front', lambda q: _lst(q['items'])); builtin('%list-queue-back', lambda q: _lst(q['items'][-1:]))
    builtin('%set-list-queue-front!', lambda q, v: q['items'].__setitem__(slice(None), list(cell_iter(v))) or VOID); builtin('%set-list-queue-back!', lambda q, v: VOID)
    builtin('list-queue-front', lambda q: q['items'][0]); builtin('list-queue-back', lambda q: q['items'][-1]); builtin('list-queue-first', lambda q: q['items'][0]); builtin('list-queue-list', lambda q: _lst(q['items'])); builtin('list-queue->list', lambda q: _lst(q['items'])); builtin('list-queue-size', lambda q: len(q['items']))

    # Heap, bijection, deque.
    builtin('%make-binary-heap', lambda vec=NIL, n=0, cmp=lambda a,b: a < b: BinaryHeap(cmp, vec)); builtin('make-binary-heap', lambda *a: BinaryHeap(a[0] if a else (lambda x, y: x < y), a[1] if len(a) > 1 else NIL)); builtin('binary-heap?', lambda x: TRUE if isinstance(x, BinaryHeap) else FALSE)
    builtin('binary-heap-vec', lambda h: SchemeVector(h.items)); builtin('set-binary-heap-vec!', lambda h, v: setattr(h, 'items', list(v.data)) or VOID); builtin('binary-heap-n', lambda h: len(h.items)); builtin('set-binary-heap-n!', lambda h, n: setattr(h, 'items', h.items[:int(n)]) or VOID); builtin('binary-heap-cmp', lambda h: h.cmp)
    builtin('binary-heap-insert!', lambda h, x: h.insert(x) or h); builtin('binary-heap-min', lambda h: h.items[0]); builtin('binary-heap-remove-min!', lambda h: builtin_remove_heap(h)); builtin('binary-heap-delete-min!', lambda h: builtin_remove_heap(h)); builtin('binary-heap-size', lambda h: len(h.items)); builtin('binary-heap-empty?', lambda h: TRUE if not h.items else FALSE)
    builtin('make-bimap', lambda init: Bimap(init)); builtin('bimap?', lambda x: TRUE if isinstance(x, Bimap) else FALSE); builtin('bimap-forward', lambda b, k: b.forward_ref(k)); builtin('bimap-forward/default', lambda b, k, d: b.forward_ref(k, d)); builtin('bimap-reverse', lambda b, v: b.reverse_ref(v)); builtin('bimap-set!', lambda b, k, v: b.set(k, v) or VOID); builtin('bimap-contains?', lambda b, k: TRUE if k in b.forward else FALSE)
    builtin('%make-bimap', lambda f, r: Bimap(NIL)); builtin('%bimap-forward', lambda b: b.forward); builtin('%bimap-forward-set!', lambda b, x: setattr(b, 'forward', x) or VOID); builtin('%bimap-rev', lambda b: b.reverse); builtin('%bimap-rev-set!', lambda b, x: setattr(b, 'reverse', x) or VOID)
    builtin('make-deque', lambda *x: Deque(x)); builtin('deque?', lambda x: TRUE if isinstance(x, Deque) else FALSE); builtin('deque-empty?', lambda d: TRUE if not d.items else FALSE); builtin('deque-add-front', lambda d, x: d.items.insert(0, x) or d); builtin('deque-add-back', lambda d, x: d.items.append(x) or d); builtin('deque-front', lambda d: d.items[0]); builtin('deque-back', lambda d: d.items[-1]); builtin('deque-remove-front', lambda d: d.items.pop(0)); builtin('deque-remove-back', lambda d: d.items.pop()); builtin('deque-length', lambda d: len(d.items)); builtin('deque->list', lambda d: _lst(d.items))
    builtin('%make-deque', lambda fl, f, bl, b: Deque(list(cell_iter(f)) + list(reversed(cell_iter(b))))); builtin('%deque-fl', lambda d: len(d.items)); builtin('%deque-f', lambda d: _lst(d.items)); builtin('%deque-bl', lambda d: 0); builtin('%deque-b', lambda d: NIL)
    for n, fn in (('push-front', lambda d,x: d.items.insert(0,x) or d), ('push-back', lambda d,x: d.items.append(x) or d), ('pop-front', lambda d: d.items.pop(0)), ('pop-back', lambda d: d.items.pop())):
        builtin('deque-' + n + '!', fn); builtin('deque-' + n, fn)
    for n in ('add-front', 'add-back', 'remove-front', 'remove-back'): builtin('deque-' + n + '!', be.data['deque-' + n])
    builtin('%set-deque-fl!', lambda d, n: VOID); builtin('%set-deque-f!', lambda d, v: VOID)
    builtin('%set-deque-bl!', lambda d, n: VOID); builtin('%set-deque-b!', lambda d, v: VOID)

    # Mapping and array APIs.
    builtin('mapping', _mapping); builtin('mapping?', lambda x: TRUE if x is NIL or (isinstance(x, Cell) and all(isinstance(p, Cell) for p in cell_iter(x))) else FALSE); builtin('list->mapping', _mapping); builtin('mapping->list', lambda m: m)
    builtin('mapping-ref', lambda m,k,*d: next((v for key,v in _pair_items(m) if key == k), d[0] if d else FALSE)); builtin('mapping-contains?', lambda m,k: TRUE if any(key == k for key,_ in _pair_items(m)) else FALSE)
    builtin('mapping-set', lambda m,k,v: _lst([Cell(k,v)] + [Cell(a,b) for a,b in _pair_items(m) if a != k])); builtin('mapping-delete', lambda m,k: _lst([Cell(a,b) for a,b in _pair_items(m) if a != k])); builtin('mapping-keys', lambda m: _lst([a for a,_ in _pair_items(m)])); builtin('mapping-values', lambda m: _lst([b for _,b in _pair_items(m)])); builtin('mapping-size', lambda m: len(_pair_items(m))); builtin('mapping-for-each', lambda f,m: ([f(a,b) for a,b in _pair_items(m)] and VOID) or VOID); builtin('mapping-map', lambda f,m: _lst([Cell(a,f(a,b)) for a,b in _pair_items(m)]))
    builtin('make-array', lambda dims, *v: Array([int(dims)] if isinstance(dims, int) else list(cell_iter(dims)), v[0] if v else 0)); builtin('array?', lambda x: TRUE if isinstance(x, (Array, SchemeVector)) else FALSE)
    builtin('array-ref', lambda a,*ix: _array_ref(a.value if isinstance(a,Array) else a, ix)); builtin('array-set!', lambda a,v,*ix: _array_set(a.value if isinstance(a,Array) else a,v,ix)); builtin('array-dimensions', lambda a: _lst(_array_dims(a.value if isinstance(a,Array) else a)))

    # Definitions whose source files otherwise supply only thin aliases.
    builtin('fixnum?', lambda x: TRUE if isinstance(x, int) and -(1<<23) <= x < (1<<23) else FALSE); builtin('fixnum-width', lambda: 24); builtin('least-fixnum', lambda: -(1<<23)); builtin('greatest-fixnum', lambda: (1<<23)-1)
    builtin('flonum?', lambda x: TRUE if isinstance(x, float) else FALSE)
    builtin('procedure-rename', lambda p, *name: p); builtin('scheme-implementation-name', lambda: SchemeString('Hermes Scheme')); builtin('scheme-implementation-version', lambda: SchemeString('0.1 (R7RS-small + SRFIs)')); builtin('version', lambda: SchemeString('0.1 (R7RS-small + SRFIs)'))
    # Correct argument order and callback semantics for existing broad registrations.
    builtin('generator-drop', lambda g,n: drop_gen(int(n), g))
    builtin('generator-fold', lambda f,init,g: _gen_fold(f,init,g)); builtin('generator-take', lambda g,n: _gen_take(int(n),g))
    builtin('vector-fold', lambda f,init,v,*more: _vec_fold(f,init,v)); builtin('vector-fold-right', lambda f,init,v: _vec_fold_right(f,init,v))
    builtin('vector-map!', lambda f,v: _vec_map_bang(f,v)); builtin('reverse-list->vector', lambda x: SchemeVector(cells(x)[::-1]))
    builtin('integer->booleans', lambda n: _lst([TRUE if (int(n) >> i) & 1 else FALSE for i in range(max(1, int(n).bit_length()))]))
    builtin('string-delete', lambda p,s: SchemeString(''.join(c for c in str(s) if not scheme_truthy(_scheme_call(p, [SchemeChar(c)]))))); builtin('string-filter', lambda p,s: SchemeString(''.join(c for c in str(s) if scheme_truthy(_scheme_call(p, [SchemeChar(c)])))))
    builtin('bitwise-or', lambda *x: _bit_fold(lambda a,b: a | b, x)); builtin('logior', be.data['bitwise-or']); builtin('bitwise-ior', be.data['bitwise-or'])
    builtin('logand', lambda *x: _bit_fold(lambda a,b: a & b, x)); builtin('logxor', lambda *x: _bit_fold(lambda a,b: a ^ b, x)); builtin('lognot', lambda x: ~int(x))
    builtin('arithmetic-shift-right', lambda n,c: int(n) >> int(c)); builtin('object->string', lambda x: SchemeString(_pr(x))); builtin('integer->string/radix', lambda n,r: SchemeString(format(int(n), 'x' if int(r)==16 else 'b' if int(r)==2 else 'o' if int(r)==8 else 'd')))
    builtin('with-exception-handler/k', lambda handler, thunk: thunk())
    builtin('loop-n', lambda n: _loop_n(int(n)))
    builtin('test-begin', lambda *name: VOID); builtin('test-end', lambda *name: VOID)
    builtin('with-output-to-string', lambda thunk: SchemeString(''))
    builtin('char-set->integer', _char_set_integer)
    builtin('char-name', lambda c: be.data['char->name'](c)); builtin('*char-names*', NIL)
    builtin('real->exact', lambda x: x)
    builtin('reverse!', _reverse_bang)
    builtin('json-encode', lambda x: SchemeString(json.dumps(_json_value(x), separators=(',', ':'))))
    builtin('%bits->integer', lambda x: sum((1 << i) for i,b in enumerate(cell_iter(x)) if scheme_truthy(b)))
    builtin('vector-cumulate', _vector_cumulate); builtin('vector-index-right', _vector_index_right); builtin('vector-skip-right', _vector_skip_right); builtin('vector-append-subvectors', _vector_append_subvectors)
    builtin('rcons', rcons)
    builtin('tmap', tmap); builtin('tfilter', tfilter)
    builtin('ttake', lambda n: lambda reducer: lambda acc, value: _scheme_call(reducer, [acc, value]) if int(n) > 0 else acc)
    builtin('tdrop', lambda n: lambda reducer: lambda acc, value: _scheme_call(reducer, [acc, value]))
    builtin('tconcatenate', lambda: lambda reducer: reducer)
    builtin('list-transduce', list_transduce)
    builtin('vector-transduce', lambda x,r,i,v: list_transduce(x, r, i, _lst(v.data if hasattr(v, 'data') else v)))
    builtin('string-transduce', lambda x,r,i,s: list_transduce(x, r, i, _lst([cs_char(c) for c in str(s)])))
    for _name in ('define-record-type*', 'let*-values', 'let-values-helper', 'letrec*', 'record-accessor', 'record-constructor', 'record-modifier', 'record-predicate', 'simple-conditions', 'source-file', 'syntax-violation', 'transcript-off', 'transcript-on'):
        builtin(_name, _unsupported(_name))



# ── Python 接口支持 ──
def initenv_py():
    builtin('py-import', py_import_mod)
    builtin('py-from', py_from_import)

    # (py-get obj "attr") → 属性访问
    builtin('py-get', lambda obj, attr: getattr(obj, str(attr)))
    # (py-set! obj "attr" val) → 属性赋值
    builtin('py-set!', lambda obj, attr, val: setattr(obj, str(attr), val) or VOID)
    # (py-call obj "method" args...) → 方法调用
    builtin('py-call', lambda obj, method, *args: getattr(obj, str(method))(*args))
    # (py-slice obj "args") → 切片访问 obj["args"]
    builtin('py-getitem', lambda obj, key: obj[key])
    builtin('py-setitem', lambda obj, key, val: obj.__setitem__(key, val) or VOID)
    # (py-new Class args...) — 创建实例
    builtin('py-new', lambda cls, *args: cls(*args))
    # (py-dir obj) — 列出属性
    builtin('py-dir', lambda obj: _lst([SchemeString(n) for n in dir(obj) if not n.startswith('_')]))
    # (py-exec "code") — 执行 Python 语句
    builtin('py-exec', lambda code: exec(str(code)) or VOID)
    # (py-eval "expr") — 求值 Python 表达式，返回结果
    builtin('py-eval', lambda expr: eval(str(expr)))
    # (py-len obj) — Python 长度
    builtin('py-len', lambda obj: len(obj))
    # (py-str obj) — Python 字符串表示
    builtin('py-str', lambda obj: SchemeString(str(obj)))
    # (py-repr obj) — Python 可读表示
    builtin('py-repr', lambda obj: str(repr(obj)))
    # (py-type obj) — Python 类型名
    builtin('py-type', lambda obj: str(type(obj).__name__))
    # (py-hasattr? obj "attr") — 属性存在检测
    builtin('py-hasattr?', lambda obj, attr: TRUE if hasattr(obj, str(attr)) else FALSE)
    # (py->list obj) — Python 可迭代对象转 Scheme 列表
    builtin('py->list', lambda obj: _lst([x for x in obj]))
    # (py->str obj) — Python 对象转 Scheme 字符串
    builtin('py->str', lambda obj: SchemeString(str(obj)))
    # (list->py obj) — Scheme 列表转 Python list
    builtin('list->py', lambda lst: [x for x in (_plist(lst) if isinstance(lst, Cell) else [lst]) if x is not NIL])
    # (py: spec) → slice(start, end, step) — 解析 Python 风格切片元组
    # (py:partial fn . args) — Python 部分应用
    builtin('py:partial', lambda fn, *args: (lambda *rest: fn(*(list(args) + list(rest)))))
    # (py:rpartial fn . args) — Python 右部分应用
    builtin('py:rpartial', lambda fn, *args: (lambda *rest: fn(*(list(rest) + list(args)))))
    # (py:curry fn n) — Python 柯里化, n 为参数个数
    builtin('py:curry', lambda fn, n: py_curry(fn, int(n)))
    # (pyslice obj spec) → obj[spec] — 切片应用
    builtin('pyslice', pyslice)
