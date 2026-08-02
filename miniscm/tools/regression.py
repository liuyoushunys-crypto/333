#!/usr/bin/env python3
"""miniscm 回归测试 — 运行核心测试套件并汇总 PASS/FAIL。

用法:
  python3 tools/regression.py              # 运行默认核心测试集
  python3 tools/regression.py --all        # 运行全部 test/ 文件
  python3 tools/regression.py test/test-arithmetic.scm ...   # 指定文件
"""
import subprocess, sys, os, re, glob

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PY = os.path.join(BASE, 'miniscm.py')

# 默认核心回归测试集 (pyb=True)
CORE_TESTS = [
    'test/test-boot-core.scm',      # let/cond/and/or/宏基础
    'test/test-boot-sugar-usage.scm',
    'test/test-case-lambda.scm',
    'test/test-arithmetic.scm',     # 算术
    'test/test-lists.scm',          # SRFI-1 列表
    'test/test-strings.scm',        # SRFI-13 字符串
    'test/test-vectors.scm',
    'test/test-char-set.scm',
    'test/test-compiler.scm',       # JIT/宏引擎
    'test/test-standards.scm',
    'test/test-data-structures.scm',
]

# 每个测试允许的已知失败数 (pyb 扩展库实现缺陷 / DSL 测试差异, 非核心回归)
KNOWN_FAILS = {
    'test-arithmetic.scm': 3,  # fx-greatest/least/fxnot (fixnum 宽度)
    'test-strings.scm': 1,     # digit-value a (非数字字符)
    'test-language.scm': 16,   # DSL 语言演示 (typeof 返回 number 等差异)
}

def run_test(path, timeout=180):
    """运行单个测试文件, 返回 (pass, fail, errors)。"""
    full = path if os.path.isabs(path) else os.path.join(BASE, path)
    if not os.path.exists(full):
        return 0, 0, [f'FILE NOT FOUND: {path}']
    try:
        r = subprocess.run([sys.executable, PY, full],
                           capture_output=True, text=True, timeout=timeout)
        out = r.stdout
    except subprocess.TimeoutExpired:
        return 0, 0, ['TIMEOUT']
    if r.returncode != 0:
        return 0, 0, [f'EXIT CODE {r.returncode}']
    p = len(re.findall(r'\[PASS\]|CHECK PASS', out))
    f = len(re.findall(r'\[FAIL\]|CHECK FAIL', out))
    # 严重错误 (崩溃/未绑定等)
    errs = [l for l in out.splitlines()
            if re.search(r'error:|RecursionError|NameError|Traceback', l)
            and 'expected' not in l and 'actual' not in l]
    return p, f, errs

def main():
    args = sys.argv[1:]
    if args and args[0] == '--all':
        files = sorted(glob.glob(os.path.join(BASE, 'test', '*.scm')))
        files = [os.path.relpath(f, BASE) for f in files]
    elif args:
        files = args
    else:
        files = CORE_TESTS

    print(f'=== miniscm 回归测试 (pyb=True) ===')
    print(f'测试文件: {len(files)}\n')
    total_p = total_f = 0
    failures = []
    for f in files:
        p, fl, errs = run_test(f)
        total_p += p; total_f += fl
        name = os.path.basename(f)
        known = KNOWN_FAILS.get(name, 0)
        status = 'PASS'
        if fl > known or errs:
            status = 'FAIL'
            failures.append((f, fl, known, errs[:2]))
        print(f'  [{status:4s}] {name:35s} PASS={p:4d} FAIL={fl:3d}' +
              (f' (known {known})' if fl > 0 and fl <= known else '') +
              (f' ERRORS={errs[:2]}' if errs else ''))
    print(f'\n=== 汇总 ===')
    print(f'总 PASS: {total_p}  总 FAIL: {total_f}')
    if failures:
        print(f'\n有问题的测试:')
        for f, fl, known, errs in failures:
            print(f'  {f}: FAIL={fl} (known {known}) {" ".join(errs)}')
        sys.exit(1)
    print('全部通过 ✓')
    sys.exit(0)

if __name__ == '__main__':
    main()
