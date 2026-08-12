# initenv.py — builtin registration extracted from primitives.py
import math, os, sys, time
from mtypes import (
    Sym, Cell, SchemeString, SchemeChar, SchemeVector, SchemeBytevector,
    Promise, SyntaxObject, SchemeException, ErrorObject, NIL, VOID,
    EOF, TRUE, FALSE, Env, _cell_len, _cells, _sn, _plist,
    _lst, builtin, be
)
from reader import parse_number_scheme
from primitives import *
from primitives import set_port_pos, hash_table_keys, hash_table_values, hash_table_ref_default, compose_fn, list_drop


def _last_pair(lst):
    cur = lst
    while isinstance(cur, Cell) and cur.cdr is not NIL:
        cur = cur.cdr
    return cur

# make-coroutine-generator: proc 接收 yield 函数, yield 挂起直到外部请求下一个值。
# 用 Python 线程 + queue 实现 (真正的 coroutine, 与 SRFI-158 语义一致)。
def make_coroutine_generator(proc):
    import queue, threading
    from mtypes import EOF as _EOF
    vals = queue.Queue()
    done = [False]
    resume = threading.Event()

    def _yield(v):
        vals.put(v)
        resume.clear()
        resume.wait()

    def _run():
        try:
            proc(_yield)
        finally:
            vals.put(_EOF)
            done[0] = True

    t = threading.Thread(target=_run, daemon=True)
    t.start()
    started = [False]

    def gen():
        if done[0] and vals.empty():
            return _EOF
        if not started[0]:
            started[0] = True
            resume.set()
        v = vals.get()
        resume.set()
        return v

    return gen


def _reduce_bit_or(args):
    r = 0
    for a in args: r |= int(a)
    return r

def _sys_exit(code):
    raise SystemExit(code)

def _redirect_in(stream):
    sys.stdin = stream

def _redirect_out(stream):
    sys.stdout = stream

def _with_file(path, thunk, mode, redirect):
    old = sys.stdin if mode == 'r' else sys.stdout
    with open(str(path), mode) as f:
        redirect(f)
        try:
            r = call(thunk, [])
        finally:
            if mode == 'r': sys.stdin = old
            else: sys.stdout = old
    return r

def initenv():
    builtin('NIL', lambda: NIL)
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
    def _inexact_to_exact_fn(x):
        if isinstance(x, float):
            if x != x or x == float('inf') or x == float('-inf'):
                raise SchemeException("inexact->exact: not a finite number")
            return Fraction(x).limit_denominator(1000000)
        if isinstance(x, Fraction) and x.denominator == 1: return int(x)
        return x
    builtin('inexact->exact', _inexact_to_exact_fn)
    def _string_to_number(s, radix=10):
        text = str(s)
        radix = int(radix)
        if radix != 10:
            try:
                return int(text, radix)
            except ValueError:
                return FALSE
        return parse_number_scheme(text)
    builtin('string->number', _string_to_number)
    builtin('numerator', lambda x: int(x) if isinstance(x,int) else (x.numerator if isinstance(x,Fraction) else x))
    builtin('denominator', lambda x: 1 if isinstance(x,int) else (x.denominator if isinstance(x,Fraction) else x.numerator if isinstance(x,float) and x==int(x) else 1))

# ── 有理数操作 ──
    builtin('rationalize', lambda x, eps: simplest_between(float(x) - float(eps), float(x) + float(eps)) if isinstance(x,(int,float,Fraction)) else x)
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
    builtin('max', max)
    builtin('min', min)
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
    builtin('string->utf8', lambda s: SchemeBytevector(str(s).encode('utf-8')))
    builtin('utf8->string', lambda s: SchemeString(bytes(s.data).decode('utf-8')) if hasattr(s,'data') else s)
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
    builtin('bytevector-append', lambda *vs: SchemeBytevector([b for v in vs for b in v.data]))
    builtin('bytevector-s8-ref', lambda v,i: v.data[i] - 256 if v.data[i] >= 128 else v.data[i])
    builtin('bytevector-s8-set!', lambda v,i,x: bv_set_u8(v, i, int(x) & 255))
    builtin('make-bytevector', lambda n,*fill: SchemeBytevector([fill[0] if fill else 0]*n))

# ── 端口与 I/O ──
    # 注意：port? 检查 tuple 格式
    builtin('port?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('str-port', 'file-port') else FALSE)
    builtin('input-port?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('str-port', 'file-port') else FALSE)
    builtin('output-port?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('str-port', 'file-port') else FALSE)
    builtin('port-open?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('str-port', 'file-port') else FALSE)
    builtin('binary-port?', lambda x: TRUE if isinstance(x, tuple) and len(x) > 1 and x[0] in ('bin-file-port', 'bin-str-port') else FALSE)
    builtin('current-input-port', lambda: ('str-port', [""]))
    builtin('current-output-port', lambda: ('str-port', [""]))
    builtin('eof-object', lambda: EOF)
    builtin('eof-object?', lambda x:TRUE if x is EOF else FALSE)
    builtin('open-input-file', lambda n: ("file-port",str(n),"r",open(str(n),'r')))
    builtin('open-output-file', lambda n: ("file-port",str(n),"w",open(str(n),'w')))
    # open-input-string: 端口为 ('str-port', [字符串, 位置])，位置暂未使用
    builtin('open-input-string', lambda s: ("str-port", [str(s), 0]))
    builtin('open-input-bytevector', lambda v: ("bin-str-port", [bytes(v.data), 0]))
    builtin('open-output-string', lambda *a: ('str-port',['']))
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
    def port_pos(p):
        if isinstance(p, tuple) and p[0] == 'str-port' and isinstance(p[1], list) and len(p[1]) > 1:
            if not hasattr(set_port_pos, '_saved_str'):
                set_port_pos._saved_str = {}
            original = set_port_pos._saved_str.setdefault(id(p), p[1][0])
            return len(original) - len(p[1][0])
        if isinstance(p, tuple) and p[0] == 'file-port' and len(p) > 3:
            return p[3].tell()
        if isinstance(p, tuple) and p[0] == 'bin-str-port' and isinstance(p[1], list) and len(p[1]) > 1:
            return p[1][1]
        return 0
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
    builtin('condition?', lambda x: TRUE if isinstance(x,(SchemeException,ErrorObject)) else FALSE)
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
    builtin('hash-table-set!', lambda ht, k, v: (ht.__setitem__(k, v), VOID)[-1])
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
    builtin('string-contains?', lambda s, sub: TRUE if str(sub) in str(s) else FALSE)
    builtin('bytevector-copy', lambda bv: SchemeBytevector(list(bv.data)) if hasattr(bv, 'data') else SchemeBytevector(list(bv)))
    builtin('bytevector->u8-list', lambda bv: _lst([int(b) for b in (bv.data if hasattr(bv, 'data') else bv)]))
    builtin('u8-list->bytevector', lambda lst: SchemeBytevector([int(x) for x in _cells(lst)]))
    builtin('with-input-from-file', lambda path, thunk: _with_file(path, thunk, 'r', _redirect_in))
    builtin('with-output-to-file', lambda path, thunk: _with_file(path, thunk, 'w', _redirect_out))



    # sx-def-env: 返回当前宏定义环境或全局 (C#: CurrentMacroDefEnv ?? GlobalEnv)

    # sx-expand-env: 返回当前宏调用点环境或全局 (C#: CurrentExpandEnv ?? GlobalEnv)
