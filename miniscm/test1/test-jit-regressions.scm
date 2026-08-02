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
