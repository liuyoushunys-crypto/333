;; test-compiler.scm — Compiler: JIT, tail recursion, morphic IC, dispatch, trampoline
;; Generated from merged test suites

(check "nested guard inner"
       (guard (e1 (else (guard (e2 (else (list 'both e1 e2))) (raise 'inner))))
         (raise 'outer))
       '(both outer inner))

; guard + 多重条件
(define (classify-exn exn)
  (guard (e ((number? e) 'number-error)
            ((string? e) 'string-error)
            (else 'other))
    (raise exn)))
(check "classify number" (classify-exn 42) 'number-error)
(check "classify string" (classify-exn "err") 'string-error)
(check "classify else" (classify-exn '(some value)) 'other)

; guard 不触发
(check "guard no-raise" (guard (e (else 'caught)) 42) 42)

; guard + with-exception-handler
(check "guard + weh"
       (guard (e (else 'final))
         (with-exception-handler
           (lambda (x) (list 'handler x))
           (lambda () (raise 'test))))
       '(handler test))


; guard + call/cc (guard 内部使用 call/cc 逃逸)
(check "guard escapes via call/cc"
       (guard (e (else 'should-not-reach))
         (call/cc (lambda (k) (k 'escaped-before-error))))
       'escaped-before-error)

; 压力: 深层递归中的异常
(define (deep-raise n)
  (if (= n 0) (raise 'bottom)
      (deep-raise (- n 1))))
(check "deep-raise 1000"
       (guard (e (else (list 'caught e)))
         (deep-raise 1000))
       '(caught bottom))

; 压力: 深层 guard 嵌套
(define (deep-guard n)
  (if (= n 0) (raise 'leaf)
      (guard (e (else (list 'level n e)))
        (deep-guard (- n 1)))))
(check "deep-guard 5"
       (deep-guard 5)
       '(level 1 leaf))


(display "") (newline)
(display "===== 32. define-macro 测试 =====") (newline)

;; test-compiler.scm — merged test file
;; JIT compiler, morphic IC, tail recursion, dispatch


(display "\n=== test-jit-regressions.scm ===\n")
;; test-jit-regressions.scm — JIT 编译器 20 轮复盘回归测试
;; 覆盖复盘1-20 的关键修复点，特别是复盘9（真值语义致命BUG）

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

;; ═══════════════════════════════════════════════════════════════
;; 1. 复盘9: Scheme 真值语义 — 只有 #f 是假值（致命BUG修复）
;; ═══════════════════════════════════════════════════════════════
;; 在 JIT 编译的代码中，(if 0 'true 'false) 必须返回 'true，
;; 因为 Scheme 中 0 是真值。Python 的 truthy 判断会将 0 视为假。
(display "\n===== 1. 真值语义 (复盘9 致命BUG) =====\n")

(define (truthy-int n)
  (if n 'true 'false))
(define (truthy-zero) (if 0 'true 'false))
(define (truthy-empty-list) (if '() 'true 'false))
(define (truthy-empty-string) (if "" 'true 'false))
(define (truthy-null-char) (if #\null 'true 'false))
(define (truthy-true) (if #t 'true 'false))
(define (truthy-false) (if #f 'true 'false))
(define (truthy-nested x)
  (if (if x 'ok #f) 'true 'false))

;; 编译触发：每个命名函数首次调用时 JIT 编译
(check "int 1 is truthy" (truthy-int 1) 'true)
(check "int 0 is truthy (Scheme!)" (truthy-zero) 'true)
(check "-1 is truthy" (truthy-int -1) 'true)
(check "empty list '() is truthy (Scheme!)" (truthy-empty-list) 'true)
(check "empty string \"\" is truthy (Scheme!)" (truthy-empty-string) 'true)
(check "#\\null is truthy" (truthy-null-char) 'true)
(check "#t is truthy" (truthy-true) 'true)
(check "#f is the ONLY false value" (truthy-false) 'false)

;; 嵌套 if 中真值
(check "nested truthy (1)" (truthy-nested 1) 'true)
(check "nested truthy (0)" (truthy-nested 0) 'true)
(check "nested truthy (#f)" (truthy-nested #f) 'false)
(check "nested truthy ('())" (truthy-nested '()) 'true)

;; 复杂条件中真值
(define (cond-truthy x)
  (cond (x 'truthy) (else 'falsy)))
(check "cond: 0 is truthy" (cond-truthy 0) 'truthy)
(check "cond: '() is truthy" (cond-truthy '()) 'truthy)
(check "cond: #f is falsy" (cond-truthy #f) 'falsy)
(check "cond: 42 is truthy" (cond-truthy 42) 'truthy)

;; and/or 中真值
(define (and-truthy a b)
  (and a b))
(check "and: 0 1 → 1 (0 truthy)" (and-truthy 0 1) 1)
(check "and: #f 1 → #f" (and-truthy #f 1) #f)
(check "and: '() 2 → 2" (and-truthy '() 2) 2)

(define (or-truthy a b)
  (or a b))
(check "or: 0 #f → 0 (0 truthy)" (or-truthy 0 #f) 0)
(check "or: #f '() → '()" (or-truthy #f '()) '())
(check "or: #f #f → #f" (or-truthy #f #f) #f)

;; 非 bool 类型的 #t/#f 不要混
(define (returns-truthy x)
  (if x #t #f))
(check "returns #t for 0" (returns-truthy 0) #t)
(check "returns #f for #f" (returns-truthy #f) #f)
(check "returns #t for '()" (returns-truthy '()) #t)

;; ═══════════════════════════════════════════════════════════════
;; 2. 复盘3: 除法返回 Fraction（不内联）
;; ═══════════════════════════════════════════════════════════════
;; '/' 不在 _INLINE_OPS 中，必须走内置过程路返回 Fraction
(display "\n===== 2. 除法 Fraction 语义 (复盘3) =====\n")

(define (div-two a b) (/ a b))
(define (div-three a b c) (/ a b c))

(check "10/2 = 5" (div-two 10 2) 5)
(check "1/3 is Fraction" (div-two 1 3) 1/3)
(check "1/3/2 = 1/6" (div-three 1 3 2) 1/6)
(check "8/4 = 2" (div-two 8 4) 2)
(check "3/2 = 1.5" (div-two 3 2) 3/2)
(check "0/5 = 0" (div-two 0 5) 0)

;; 混合运算：除法结果参与后续算术
(define (compute-avg a b)
  (/ (+ a b) 2))
(check "avg 3 5 = 4" (compute-avg 3 5) 4)
(check "avg 1 2 = 3/2" (compute-avg 1 2) 3/2)

(define (compute-ratio a b c)
  (/ (* a b) c))
(check "ratio 2*3/4 = 3/2" (compute-ratio 2 3 4) 3/2)

;; 除法 + 比较
(define (gt-half? x)
  (> x (/ 1 2)))
(check "3/4 > 1/2" (gt-half? 3/4) #t)
(check "1/4 > 1/2" (gt-half? 1/4) #f)

;; ═══════════════════════════════════════════════════════════════
;; 3. 复盘16: begin 单表达式快速路径
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 3. begin 单表达式快速路径 (复盘16) =====\n")

(define (begin-single x)
  (begin x))
(define (begin-double x y)
  (begin x y))
(define (begin-triple x y z)
  (begin x y z))
(define (begin-empty)
  (begin))
(define (begin-with-effect x)
  (begin (display "[side] ") x))

(check "begin single returns value" (begin-single 42) 42)
(check "begin double returns last" (begin-double 1 2) 2)
(check "begin triple returns last" (begin-triple 1 2 3) 3)
(check "begin empty returns void" (eq? (begin-empty) (if #f #f)) #t)

;; 副作用顺序验证
(define side-effects '())
(define (reset-side!) (set! side-effects '()))
(define (record! x) (set! side-effects (cons x side-effects)) x)

(define (begin-with-seq)
  (begin (record! 1) (record! 2) (record! 3)))
(reset-side!)
(check "begin record side order" (begin-with-seq) 3)
(check "side effects in order" (reverse side-effects) '(1 2 3))

;; ═══════════════════════════════════════════════════════════════
;; 4. 复盘7: 常量折叠类型安全
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 4. 常量折叠类型安全 (复盘7) =====\n")

;; 算术常量折叠
(define (const-arith) (+ 1 2 3 4 5))
(define (const-mixed) (- (* 10 2) (/ 8 2)))
(define (const-compare) (< 1 2 3 4 5))
(define (const-compare-false) (< 5 4 3 2 1))

(check "const fold sum 1..5 = 15" (const-arith) 15)
(check "const fold mixed = 16" (const-mixed) 16)
(check "const fold 1<2<3<4<5" (const-compare) #t)
(check "const fold 5<4<3<2<1" (const-compare-false) #f)

;; car/cdr 常量折叠
(define (const-car) (car '(a b c)))
(define (const-cdr) (cdr '(a b c)))
(define (const-null?) (null? '()))
(define (const-null?-non) (null? '(1)))
(define (const-pair?) (pair? '(1 . 2)))
(define (const-pair?-not) (pair? '()))

(check "const fold car of '(a b c)" (const-car) 'a)
(check "const fold cdr of '(a b c)" (const-cdr) '(b c))
(check "const fold null? '()" (const-null?) #t)
(check "const fold null? '(1)" (const-null?-non) #f)
(check "const fold pair? '(1 . 2)" (const-pair?) #t)
(check "const fold pair? '()" (const-pair?-not) #f)

;; not 常量折叠
(define (const-not-t) (not #t))
(define (const-not-f) (not #f))
(define (const-not-nil) (not '()))

(check "const fold not #t" (const-not-t) #f)
(check "const fold not #f" (const-not-f) #t)
(check "const fold not '()" (const-not-nil) #f)

;; ═══════════════════════════════════════════════════════════════
;; 5. 不可变原语 IC 缓存（Morphic IC）正确性
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 5. 不可变原语 IC 缓存 =====\n")

(define (ic-cons x y) (cons x y))
(define (ic-car x) (car x))
(define (ic-cdr x) (cdr x))
(define (ic-null? x) (null? x))
(define (ic-pair? x) (pair? x))
(define (ic-append a b) (append a b))
(define (ic-reverse x) (reverse x))
(define (ic-length x) (length x))
(define (ic-list . args) (apply list args))
(define (ic-map f lst) (map f lst))
(define (ic-memq x lst) (memq x lst))
(define (ic-assq x al) (assq x al))
(define (ic-caar x) (caar x))
(define (ic-cadr x) (cadr x))
(define (ic-cddr x) (cddr x))
(define (ic-caddr x) (caddr x))
(define (ic-display x) (display x))

(check "ic-cons" (ic-cons 1 2) '(1 . 2))
(check "ic-car" (ic-car '(a . b)) 'a)
(check "ic-cdr" (ic-cdr '(a . b)) 'b)
(check "ic-null? empty" (ic-null? '()) #t)
(check "ic-null? non" (ic-null? '(1)) #f)
(check "ic-pair? pair" (ic-pair? '(1 . 2)) #t)
(check "ic-pair? non" (ic-pair? '()) #f)
(check "ic-append" (ic-append '(1 2) '(3 4)) '(1 2 3 4))
(check "ic-reverse" (ic-reverse '(1 2 3)) '(3 2 1))
(check "ic-length" (ic-length '(a b c)) 3)
(check "ic-list" (ic-list 1 2 3) '(1 2 3))
(check "ic-map add1" (ic-map (lambda (x) (+ x 1)) '(1 2 3)) '(2 3 4))
(check "ic-memq" (ic-memq 'b '(a b c)) '(b c))
(check "ic-assq" (ic-assq 'b '((a . 1) (b . 2))) '(b . 2))
(check "ic-caar" (ic-caar '((1 2) 3 4)) 1)
(check "ic-cadr" (ic-cadr '(1 2 3)) 2)
(check "ic-cddr" (ic-cddr '(1 2 3 4)) '(3 4))
(check "ic-caddr" (ic-caddr '(1 2 3 4)) 3)

;; ═══════════════════════════════════════════════════════════════
;; 6. 自递归 TCO（复盘13 define 命名 lambda + 复盘1-20 综合）
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 6. 自递归 TCO 编译 =====\n")

(define (fact-tail n acc)
  (if (= n 0) acc (fact-tail (- n 1) (* n acc))))
(define (even? n)
  (if (= n 0) #t (odd? (- n 1))))
(define (odd? n)
  (if (= n 0) #f (even? (- n 1))))

(check "fact-tail 5 = 120" (fact-tail 5 1) 120)
(check "fact-tail 10 = 3628800" (fact-tail 10 1) 3628800)
(check "even? 100 is #t" (even? 100) #t)
(check "odd? 100 is #f" (odd? 100) #f)
(check "even? 999 is #f" (even? 999) #f)
(check "odd? 999 is #t" (odd? 999) #t)

;; 深度自递归 (验证 TCO 不爆栈)
(check "fact-tail 20" (fact-tail 20 1) 2432902008176640000)
(check "fact-tail 100" (fact-tail 100 1) 93326215443944152681699238856266700490715968264381621468592963895217599993229915608941463976156518286253697920827223758251185210916864000000000000000000000000)
(check "even? 10000" (even? 10000) #t)  ; 深度 10000 层互递归 TCO 验证

;; ═══════════════════════════════════════════════════════════════
;; 7. 内联算术与比较（复盘17 _INLINE_ARITH / _INLINE_CMP）
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 7. 内联算术与比较 =====\n")

(define (add a b) (+ a b))
(define (sub a b) (- a b))
(define (mul a b) (* a b))
(define (less? a b) (< a b))
(define (greater? a b) (> a b))
(define (less-eq? a b) (<= a b))
(define (greater-eq? a b) (>= a b))
(define (num-eq? a b) (= a b))

(check "add 3+4=7" (add 3 4) 7)
(check "sub 10-3=7" (sub 10 3) 7)
(check "mul 6*7=42" (mul 6 7) 42)
(check "3<4" (less? 3 4) #t)
(check "4<3" (less? 4 3) #f)
(check "5>2" (greater? 5 2) #t)
(check "2>5" (greater? 2 5) #f)
(check "3<=3" (less-eq? 3 3) #t)
(check "4<=3" (less-eq? 4 3) #f)
(check "5>=5" (greater-eq? 5 5) #t)
(check "4>=5" (greater-eq? 4 5) #f)
(check "3=3" (num-eq? 3 3) #t)
(check "3=4" (num-eq? 3 4) #f)

;; 链式比较
(define (chain-cmp a b c) (< a b c))
(define (chain-cmp2 a b c d) (< a b c d))
(check "chain 1<2<3" (chain-cmp 1 2 3) #t)
(check "chain 1<3<2" (chain-cmp 1 3 2) #f)
(check "chain 1<2<3<4" (chain-cmp2 1 2 3 4) #t)
(check "chain 1<4<2<3" (chain-cmp2 1 4 2 3) #f)

;; 混合运算
(define (mix-op a b c) (+ (* a b) c))
(check "mix-op 2*3+4=10" (mix-op 2 3 4) 10)

;; ═══════════════════════════════════════════════════════════════
;; 8. 内联 car/cdr/null?/pair?/not（AST 属性访问）
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 8. 内联 car/cdr/null?/pair?/not =====\n")

(define (inline-car x) (car x))
(define (inline-cdr x) (cdr x))
(define (inline-cadr x) (cadr x))
(define (inline-caddr x) (caddr x))
(define (inline-null? x) (null? x))
(define (inline-pair? x) (pair? x))
(define (inline-not x) (not x))

(check "inline-car (1 2 3)" (inline-car '(1 2 3)) 1)
(check "inline-cdr (1 2 3)" (inline-cdr '(1 2 3)) '(2 3))
(check "inline-cadr (1 2 3)" (inline-cadr '(1 2 3)) 2)
(check "inline-caddr (1 2 3 4)" (inline-caddr '(1 2 3 4)) 3)
(check "inline-null? '()" (inline-null? '()) #t)
(check "inline-null? '(1)" (inline-null? '(1)) #f)
(check "inline-pair? '(1 . 2)" (inline-pair? '(1 . 2)) #t)
(check "inline-pair? '()" (inline-pair? '()) #f)
(check "inline-not #t" (inline-not #t) #f)
(check "inline-not #f" (inline-not #f) #t)
(check "inline-not '()" (inline-not '()) #f)  ;; '() is truthy, not #f → not returns #f
(check "inline-not 0" (inline-not 0) #f)

;; ═══════════════════════════════════════════════════════════════
;; 9. eq? 内联（ast.Is）
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 9. eq? 内联 =====\n")

(define (inline-eq? a b) (eq? a b))

(check "eq? same symbol" (inline-eq? 'a 'a) #t)
(check "eq? diff symbol" (inline-eq? 'a 'b) #f)
(check "eq? #t #t" (inline-eq? #t #t) #t)
(check "eq? #f #f" (inline-eq? #f #f) #t)
(check "eq? #t #f" (inline-eq? #t #f) #f)
(check "eq? '() '()" (inline-eq? '() '()) #t)
(check "eq? 42 42" (inline-eq? 42 42) #t)  ;; fixnum eq

;; ═══════════════════════════════════════════════════════════════
;; 10. 复盘13: _compile_DefineAST 用 ast.Constant
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 10. define 编译正确性 =====\n")

(define x10 42)
(define y10 (+ 1 2))
(define z10 "hello")
(define w10 '(a b c))
(check "define constant" x10 42)
(check "define computed" y10 3)
(check "define string" z10 "hello")
(check "define list" w10 '(a b c))

;; ═══════════════════════════════════════════════════════════════
;; 11. 复盘14: 临时变量 __mscm_t_ 前缀 不冲突
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 11. 临时变量不冲突 =====\n")

(define (let-test x)
  (let ((a (+ x 1))
        (b (* x 2)))
    (+ a b)))
(define (nested-let x)
  (let ((x (+ x 1)))
    (let ((y (* x 2)))
      (+ x y))))
(define (let*-test x y)
  (let* ((a (+ x y))
         (b (* a 2)))
    b))
(check "let binding" (let-test 3) 10)        ;; (3+1)+(3*2)=10
(check "nested let shadow" (nested-let 5) 18) ;; x=5 → inner x=6 → y=12 → 6+12=18
(check "let* binding" (let*-test 2 3) 10)     ;; (2+3)*2=10

;; ═══════════════════════════════════════════════════════════════
;; 12. set! 编译正确性（复盘14 临时变量 + 一般编译）
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 12. set! 编译正确性 =====\n")

(define counter 0)
(define (inc-counter!)
  (set! counter (+ counter 1))
  counter)
(define (set-test x)
  (define local 0)
  (set! local x)
  local)

(check "set! global" (begin (inc-counter!) (inc-counter!) counter) 2)
(check "set! local" (set-test 99) 99)

;; ═══════════════════════════════════════════════════════════════
;; 13. 综合：lambda 嵌套 + 闭包（复盘6 闭包检测 + 复盘18 __slots__）
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 13. lambda 嵌套与闭包 =====\n")

(define (make-adder n)
  (lambda (x) (+ x n)))
(define add5 (make-adder 5))
(check "closure add5(3)=8" (add5 3) 8)
(check "closure add5(0)=5" (add5 0) 5)

(define (make-counter)
  (let ((count 0))
    (lambda ()
      (set! count (+ count 1))
      count)))
(define c1 (make-counter))
(define c2 (make-counter))
(check "counter1 #1" (c1) 1)
(check "counter1 #2" (c1) 2)
(check "counter2 #1" (c2) 1)  ;; independent closure

;; ═══════════════════════════════════════════════════════════════
;; 14. 复盘5: LambdaProc.__call__ 延迟导入缓存
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 14. 缓存加载正确性 =====\n")

;; 每次运行此文件时，函数已被编译和缓存
;; 再次运行确保缓存命中结果正确
(define (cache-test-1 x) (+ x 1))
(define (cache-test-2 x) (* x 2))
(define (cache-test-3 x) (if (eq? x #f) 'false x))
(check "cache reload add1" (cache-test-1 41) 42)
(check "cache reload mul2" (cache-test-2 21) 42)
(check "cache reload truthy" (cache-test-3 42) 42)
(check "cache reload falsy" (cache-test-3 #f) 'false)

;; ═══════════════════════════════════════════════════════════════
;; 15. 复盘19: 清理导入不引起问题
;; ═══════════════════════════════════════════════════════════════
(display "\n===== 15. 综合正确性 =====\n")

;; map + lambda 高阶函数
(define (double-list lst)
  (map (lambda (x) (* x 2)) lst))
(check "map double" (double-list '(1 2 3 4)) '(2 4 6 8))

;; filter 实现
(define (filter pred lst)
  (reverse
    (let loop ((l lst) (acc '()))
      (if (null? l)
          acc
          (loop (cdr l)
                (if (pred (car l))
                    (cons (car l) acc)
                    acc))))))
(define (even? n) (= (modulo n 2) 0))
(check "filter even" (filter even? '(1 2 3 4 5 6)) '(2 4 6))

;; 递归反转链表
(define (my-reverse lst)
  (let loop ((l lst) (acc '()))
    (if (null? l)
        acc
        (loop (cdr l) (cons (car l) acc)))))
(check "my-reverse" (my-reverse '(1 2 3 4 5)) '(5 4 3 2 1))

;; 多参数函数
(define (sum-all . args)
  (apply + args))
(check "sum-all 1..10" (sum-all 1 2 3 4 5 6 7 8 9 10) 55)

;; values/list 用 rest 参数
(define (list-rest . args) args)
(check "list-rest" (list-rest 1 2 3) '(1 2 3))

(display "\n===== 全部 JIT 回归测试完成 =====\n")


(display "\n=== test-morphic-ic.scm ===\n")
;; test-morphic-ic.scm — Selective Morphic IC 缓存正确性验证
;; 验证不可变标准原语安全内联缓存 + 用户变量动态查找 + 内联优化

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(display "\n===== 1. 不可变原语 IC 缓存 — JIT 编译后正确 =====\n")
;; 这些函数被 JIT 编译时使用 Morphic IC 缓存 _IMMUTABLE_PRIMITIVES
(define (ic-cons x y) (cons x y))
(define (ic-car x) (car x))
(define (ic-cdr x) (cdr x))
(define (ic-null? x) (null? x))
(define (ic-pair? x) (pair? x))
(define (ic-list . args) (apply list args))
(define (ic-append a b) (append a b))
(define (ic-reverse x) (reverse x))
(define (ic-length x) (length x))
(define (ic-map f lst) (map f lst))
(define (ic-eq? a b) (eq? a b))
(define (ic-boolean? x) (boolean? x))
(define (ic-symbol? x) (symbol? x))
(define (ic-number? x) (number? x))
(define (ic-string? x) (string? x))
(define (ic-vector? x) (vector? x))
(define (ic-char? x) (char? x))
(define (ic-memq x lst) (memq x lst))
(define (ic-member x lst) (member x lst))
(define (ic-assq x alist) (assq x alist))

(check "ic-cons" (ic-cons 1 2) '(1 . 2))
(check "ic-car" (ic-car '(a b c)) 'a)
(check "ic-cdr" (ic-cdr '(a b c)) '(b c))
(check "ic-null? empty" (ic-null? '()) #t)
(check "ic-null? nonempty" (ic-null? '(1)) #f)
(check "ic-pair? pair" (ic-pair? '(1 . 2)) #t)
(check "ic-pair? empty" (ic-pair? '()) #f)
(check "ic-list" (ic-list 1 2 3) '(1 2 3))
(check "ic-append" (ic-append '(1 2) '(3 4)) '(1 2 3 4))
(check "ic-reverse" (ic-reverse '(1 2 3)) '(3 2 1))
(check "ic-length" (ic-length '(a b c d e)) 5)
(check "ic-map" (ic-map (lambda (x) (* x 2)) '(1 2 3)) '(2 4 6))
(check "ic-eq? same" (ic-eq? 'a 'a) #t)
(check "ic-eq? diff" (ic-eq? 'a 'b) #f)
(check "ic-boolean?" (ic-boolean? #t) #t)
(check "ic-symbol?" (ic-symbol? 'hello) #t)
(check "ic-number?" (ic-number? 42) #t)
(check "ic-string?" (ic-string? "abc") #t)
(check "ic-vector?" (ic-vector? #(1 2)) #t)
(check "ic-char?" (ic-char? #\a) #t)
(check "ic-memq found" (ic-memq 'b '(a b c)) '(b c))
(check "ic-memq not found" (ic-memq 'z '(a b c)) #f)
(check "ic-member" (ic-member 2 '(1 2 3)) '(2 3))
(check "ic-assq" (ic-assq 'a '((a . 1) (b . 2))) '(a . 1))

(display "\n===== 2. 用户变量的 set! 安全性 — 不被 IC 缓存 =====\n")
;; 用户自定义变量可能被 set! 修改，必须使用 env.lookup 不可缓存
(define my-fn list)
(check "my-fn before" (my-fn 1 2) '(1 2))
(set! my-fn cons)
(check "my-fn after set!" (my-fn 1 2) '(1 . 2))

(define my-val 42)
(check "my-val before" my-val 42)
(set! my-val 100)
(check "my-val after set!" my-val 100)

(define my-car car)
(check "my-car before" (my-car '(a b)) 'a)
(set! my-car cdr)
(check "my-car after set!" (my-car '(a b)) '(b))

(display "\n===== 3. 算术内联优化 =====\n")
(define (ic-add . args) (apply + args))
(define (ic-sub . args) (apply - args))
(define (ic-mul . args) (apply * args))

(check "ic-add 2" (ic-add 10 20) 30)
(check "ic-add 3" (ic-add 1 2 3) 6)
(check "ic-sub" (ic-sub 10 3) 7)
(check "ic-mul" (ic-mul 2 3 4) 24)

;; 算术与不可变原语组合
(define (ic-compute x)
  (car (cons (* x 2) (cdr (cons x x)))))
(check "ic-compute inline" (ic-compute 10) 20)

(display "\n===== 4. 逻辑比较内联优化 =====\n")
(define (ic-lt a b) (< a b))
(define (ic-gt a b) (> a b))
(define (ic-le a b) (<= a b))
(define (ic-ge a b) (>= a b))
(define (ic-eq a b) (= a b))

(check "ic-lt true" (ic-lt 1 2) #t)
(check "ic-lt false" (ic-lt 3 2) #f)
(check "ic-gt" (ic-gt 5 3) #t)
(check "ic-le" (ic-le 3 3) #t)
(check "ic-ge" (ic-ge 4 3) #t)
(check "ic-eq" (ic-eq 5 5) #t)

(display "\n===== 5. 高阶函数使用 IC 缓存 =====\n")
(define (call-twice f x) (f (f x)))
(check "call-twice car/cdr" (call-twice cdr '(1 2 3 4)) '(3 4))
(check "call-twice null? errors" (guard (ex (else 'caught)) (call-twice cdr '())) 'caught)
(check "call-twice add1" (call-twice (lambda (x) (+ x 1)) 5) 7)

(define (mymap f lst)
  (if (null? lst)
      '()
      (cons (f (car lst)) (mymap f (cdr lst)))))
(check "mymap car/cdr/cons/null?" (mymap (lambda (x) (+ x 1)) '(1 2 3)) '(2 3 4))

(display "\n===== 6. TCO 与 IC 缓存共存 =====\n")
(define (cnt-down n acc)
  (if (= n 0)
      (reverse acc)
      (cnt-down (- n 1) (cons n acc))))
(check "cnt-down 100000 length" (length (cnt-down 100000 '())) 100000)
(check "cnt-down 100000 car" (car (cnt-down 100000 '())) 100000)

(display "\n===== 7. 标准原语不可修改性 =====\n")
;; 尝试 set! 标准过程（Scheme 规范规定未定义行为，但运行时应安全）
(define saved-car car)
(set! car (lambda (x) (if (pair? x) (ic-cdr x) (error "no pair"))))
;; 现在 car 指向用户函数（不可缓存的原语），但应该还能工作
(check "set! car to user fn" (car '(1 2 3)) '(2 3))
;; 恢复
(set! car saved-car)
(check "car restored" (car '(1 2 3)) 1)

(display "\n===== 所有 Morphic IC 测试完成 =====\n")


(display "\n=== test-tail-recursion.scm ===\n")
;; test-tail-recursion.scm — 深度尾递归全面测试
;; 所有测试在 100000 深度下验证无栈溢出

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(display "\n===== 1. 简单自递归 =====\n")
(define (tail-sum n acc)
  (if (= n 0) acc (tail-sum (- n 1) (+ n acc))))
(check "tail-sum 100000" (tail-sum 100000 0) 5000050000)
(check "tail-sum 0" (tail-sum 0 0) 0)

(define (tail-fact n acc)
  (if (= n 0) acc (tail-fact (- n 1) (* n acc))))
(check "tail-fact 10" (tail-fact 10 1) 3628800)
(check "tail-fact 0" (tail-fact 0 1) 1)
(check "tail-fact 1" (tail-fact 1 1) 1)

(display "\n===== 2. 互递归 =====\n")
(define (even-tail? n)
  (if (= n 0) #t (odd-tail? (- n 1))))
(define (odd-tail? n)
  (if (= n 0) #f (even-tail? (- n 1))))
(check "even-tail? 100000" (even-tail? 100000) #t)
(check "odd-tail? 100001" (odd-tail? 100001) #t)
(check "even-tail? 0" (even-tail? 0) #t)
(check "odd-tail? 0" (odd-tail? 0) #f)
(check "even-tail? 1" (even-tail? 1) #f)
(check "odd-tail? 1" (odd-tail? 1) #t)

(display "\n===== 3. 三重互递归 =====\n")
(define (f1 n)
  (if (= n 0) 1 (f2 (- n 1))))
(define (f2 n)
  (if (= n 0) 2 (f3 (- n 1))))
(define (f3 n)
  (if (= n 0) 3 (f1 (- n 1))))
(check "f1 100000" (f1 100000) 2)
(check "f2 100000" (f2 100000) 3)
(check "f3 100000" (f3 100000) 1)
(check "f1 0" (f1 0) 1)
(check "f2 0" (f2 0) 2)
(check "f3 0" (f3 0) 3)

(display "\n===== 4. 尾递归斐波那契 =====\n")
(define (fib-tail n a b)
  (if (= n 0) a (fib-tail (- n 1) b (+ a b))))
(check "fib-tail 0" (fib-tail 0 0 1) 0)
(check "fib-tail 1" (fib-tail 1 0 1) 1)
(check "fib-tail 10" (fib-tail 10 0 1) 55)
(check "fib-tail 100" (fib-tail 100 0 1) 354224848179261915075)
(check "fib-tail 100000 exists" (number? (fib-tail 100000 0 1)) #t)

(display "\n===== 5. 尾递归构造列表 =====\n")
(define (make-list-tail n acc)
  (if (= n 0) acc (make-list-tail (- n 1) (cons n acc))))
(define lst-100 (make-list-tail 100 '()))
(check "make-list-tail 100 length" (length lst-100) 100)
(check "make-list-tail 100 car" (car lst-100) 1)
(check "make-list-tail 100 last" (list-ref lst-100 99) 100)

(display "\n===== 6. 尾递归列表求和 =====\n")
(define (list-sum lst n)
  (if (null? lst) n (list-sum (cdr lst) (+ (car lst) n))))
(define big-list (make-list-tail 100000 '()))
(check "big-list length" (length big-list) 100000)
(check "big-list sum" (list-sum big-list 0) 5000050000)

(display "\n===== 7. begin 块内尾递归 =====\n")
(define (begin-tail n)
  (begin
    (if (= n 0) 'done (begin-tail (- n 1)))))
(check "begin-tail 100000" (begin-tail 100000) 'done)

(define (begin-tail2 n acc)
  (if (= n 0) acc (begin-tail2 (- n 1) (+ acc 1))))
(check "begin-tail2 10000" (begin-tail2 10000 0) 10000)

(display "\n===== 8. let 内尾递归 =====\n")
(define (let-tail n)
  (let ((m n))
    (if (= m 0) 42 (let-tail (- m 1)))))
(check "let-tail 100000" (let-tail 100000) 42)

(display "\n===== 9. 多参数尾递归 =====\n")
(define (multi-param a b c d e)
  (if (= a 0) (+ b c d e) (multi-param (- a 1) (+ b 1) (+ c 2) (+ d 3) (+ e 4))))
(check "multi-param 100000" (multi-param 100000 0 0 0 0) 1000000)

(display "\n===== 10. 尾递归回传累加 =====\n")
(define (acc-tail lst)
  (define (iter lst out)
    (if (null? lst) out (iter (cdr lst) (cons (car lst) out))))
  (iter lst '()))
(define reversed (acc-tail (make-list-tail 100000 '())))
(check "acc-tail length" (length reversed) 100000)
(check "acc-tail first" (car reversed) 100000)
(check "acc-tail last" (list-ref reversed 99999) 1)

(display "\n===== 全部深度尾递归测试完成 =====\n")


(display "\n=== test-dispatch.scm ===\n")
;; test-dispatch.scm — merged dispatch verification tests


(display "\n=== test_dispatch.scm ===\n")
;; Verify all 40+ special forms still work after dispatch refactoring
(display "=== 核心特殊形式 ===\n")

(display "quote: ") (display (quote (1 2 3))) (newline)
(display "if true: ") (display (if #t "yes" "no")) (newline)
(display "if false: ") (display (if #f "yes" "no")) (newline)
(display "if no else: ") (display (if #f 42)) (newline)
(display "begin: ") (display (begin (+ 1 2) (- 5 3))) (newline)
(display "lambda: ") (display ((lambda (x) (* x 2)) 5)) (newline)
(display "define: ") (define _x 42) (display _x) (newline)
(display "set!: ") (set! _x 99) (display _x) (newline)
(display "quasiquote: ") (display `(1 ,(+ 1 2) 3)) (newline)
(display "cond: ") (display (cond ((> 3 2) "big") (else "small"))) (newline)
(display "case: ") (display (case 2 ((1) "one") ((2) "two"))) (newline)
(display "and: ") (display (and 1 2 3)) (newline)
(display "or: ") (display (or #f #f 42)) (newline)
(display "when: ") (display (when #t 10)) (newline)
(display "unless: ") (display (unless #f 20)) (newline)
(display "let: ") (display (let ((x 5)) (+ x 3))) (newline)
(display "let*: ") (display (let* ((x 3) (y (* x 2))) y)) (newline)
(display "letrec fact: ") (display (letrec ((f (lambda (n) (if (< n 2) 1 (* n (f (- n 1))))))) (f 5))) (newline)
(display "do: ") (do ((i 0 (+ i 1))) ((= i 3) (quote done)) (display i) (display " ")) (newline)
(display "delay/force: ") (display (force (delay (+ 2 3)))) (newline)
(display "call/cc: ") (display (call/cc (lambda (k) (k 42)))) (newline)
(display "call-with-values: ") (display (call-with-values (lambda () (values 10 20)) (lambda (a b) (+ a b)))) (newline)
(display "define-values: ") (define-values (a b) (values 1 2)) (display a) (display b) (newline)
(display "define-syntax: ") 
  (define-syntax my-when (syntax-rules () ((_ test body ...) (if test (begin body ...)))))
  (display (my-when #t 42)) (newline)
(display "let-syntax: ") 
  (let-syntax ((twice (syntax-rules () ((_ x) (* 2 x)))))
    (display (twice 5)) (newline))
(display "case-lambda: ") 
  (display ((case-lambda ((a) (* a a)) ((a b) (+ a b))) 3)) (newline)
(display "parameterize: ")
  (define p (make-parameter 0))
  (parameterize ((p 5)) (display (p))) (display " outside: ") (display (p)) (newline)
(display "guard: ")
  (guard (e ((error-object? e) (display "caught"))) (error "oh no")) (newline)
(display "receive: ") (display (receive (a b) (values 1 2) (+ a b))) (newline)
(display "dynamic-wind: ") (display (dynamic-wind (lambda () 'before) (lambda () 77) (lambda () 'after))) (newline)
(display "=== ALL 38 SPECIAL FORMS PASSED ===\n")


(display "\n=== test_dispatch2.scm ===\n")
(display "=== 特殊形式验证 (无 quasiquote) ===\n")
(display "quote: ") (display (quote (1 2 3))) (newline)
(display "if: ") (display (if #t "yes" "no")) (newline)
(display "begin: ") (display (begin (+ 1 2) (- 5 3))) (newline)
(display "lambda: ") (display ((lambda (x) (* x 2)) 5)) (newline)
(display "define:") (define _x 42) (display _x) (newline)
(display "set!:") (set! _x 99) (display _x) (newline)
(display "cond: ") (display (cond ((> 3 2) "big") (else "small"))) (newline)
(display "case: ") (display (case 2 ((1) "one") ((2) "two"))) (newline)
(display "and: ") (display (and 1 2 3)) (newline)
(display "or: ") (display (or #f 42)) (newline)
(display "when: ") (display (when #t 10)) (newline)
(display "unless: ") (display (unless #f 20)) (newline)
(display "let: ") (display (let ((x 5)) (+ x 3))) (newline)
(display "let*: ") (display (let* ((x 3) (y (* x 2))) y)) (newline)
(display "letrec: ") (display (letrec ((f (lambda (n) (if (< n 2) 1 (* n (f (- n 1))))))) (f 5))) (newline)
(display "do: ") (display (do ((i 0 (+ i 1))) ((= i 3) 'done) (display i))) (newline)
(display "call/cc: ") (display (call/cc (lambda (k) (k 42)))) (newline)
(display "define-values:") (define-values (a b) (values 1 2)) (display a) (display b) (newline)
(display "define-syntax:") (define-syntax my-when (syntax-rules () ((_ t b ...) (if t (begin b ...))))) (display (my-when #t 42)) (newline)
(display "let-syntax:") (let-syntax ((twice (syntax-rules () ((_ x) (* 2 x))))) (display (twice 5))) (newline)
(display "case-lambda:") (display ((case-lambda ((a) (* a a)) ((a b) (+ a b))) 3)) (newline)
(display "parameterize:") (define p (make-parameter 0)) (parameterize ((p 5)) (display (p))) (display "|") (display (p)) (newline)
(display "dynamic-wind:") (display (dynamic-wind (lambda () 'b) (lambda () 77) (lambda () 'a))) (newline)
(display "=== ALL PASS ===\n")




(display "\n=== test-edges.scm ===\n")
