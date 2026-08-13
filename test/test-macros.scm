;; test-macros.scm — Macro system: syntax-rules, syntax-case, define-macro, tutorials
;; Generated from merged test suites

;; =============================================================================
;; test2.scm — 综合压力测试 & 复杂边缘场景 (Enterprise Scheme)
;; =============================================================================
;; =============================================================================
;; 1. define-macro 压力测试
;; =============================================================================
(display ";; === 1. define-macro stress ===\n")

;; 1.1 多层 quasiquote 嵌套
(define-macro (qq-nest x)
  `(let ((y ,x))
     `(list ,y ,,x)))
;; Note: 仅测语法正确性, 不测深层求值

;; 1.2 rest + unquote-splicing 多表达式
(define-macro (begin-with-display . body)
  `(begin (display ">>> ") ,@body))
(check "begin-with-display rest+unquote-splicing"
       (begin-with-display (+ 1 2) (* 3 4))
       (begin (display ">>> ") (+ 1 2) (* 3 4)))

;; 1.3 宏内递归 define-macro
(define-macro (defalias old new)
  `(define-macro (,new . args)
     (cons ',old args)))
(defalias list my-list2)
(check "defalias macro" (my-list2 1 2 3) '(1 2 3))

;; 1.4 宏返回宏的展开
(define-macro (macro-factory x)
  `(define-macro (gen) ',x))
(macro-factory 42)
(check "macro-factory" (gen) 42)

;; 1.5 宏内使用 call/cc
(define-macro (with-escape . body)
  `(call/cc (lambda (k) ,@body)))
(check "macro + call/cc early"   (with-escape (k 99) (+ 1 2)) 99)
(check "macro + call/cc normal"  (with-escape (+ 1 2)) 3)

;; 1.6 宏闭包捕获环境 — 跳过 (define-macro 在 let 内定义非标准行为)

;; 1.7 宏的参数求值时机 (应展开后统一求值, 非展开时)
(define-macro (twice expr) `(begin ,expr ,expr))
(let ((counter 0))
  (define (inc!) (set! counter (+ counter 1)) counter)
  (twice (inc!))
  (check "macro arg eval twice" counter 2))

;; 1.8 define-macro 长参数列表
(define-macro (many-args a b c d e f g)
  `(list ,a ,b ,c ,d ,e ,f ,g))
(check "many-args" (many-args 1 2 3 4 5 6 7) '(1 2 3 4 5 6 7))

;; 1.9 空 rest
(define-macro (no-args . body) `(begin ,@body))
(check "empty rest" (no-args) (begin))


;; =============================================================================
;; 2. syntax-rules 压力测试
;; =============================================================================
(display ";; === 2. syntax-rules stress ===\n")

;; 2.1 多层 ellipsis 嵌套 (简单版)
(define-syntax simple-ellipsis-group
  (syntax-rules ()
    ((_ (a ...) (b ...)) (list (list a ...) (list b ...)))))
(check "simple two-group ellipsis"
       (simple-ellipsis-group (1 2 3) (4 5)) '((1 2 3) (4 5)))

;; 2.2 多个 literal 关键字
(define-syntax multi-literal
  (syntax-rules (begin end)
    ((_ begin x ... end) (list x ...))
    ((_ x ...) 'nope)))
(check "multi-literal matched"   (multi-literal begin 1 2 3 end) '(1 2 3))
(check "multi-literal nomatch"   (multi-literal a b c) 'nope)

;; 2.3 空模式匹配
(define-syntax empty-pat
  (syntax-rules ()
    ((_) 'empty)))
(check "empty pattern" (empty-pat) 'empty)

;; 2.4 通配符 _ 与模式变量混合
(define-syntax wild-mix
  (syntax-rules ()
    ((_ a _ b) (list a b))))
(check "wildcard mix" (wild-mix 1 2 3) '(1 3))

;; 2.5 深层嵌套 pattern
(define-syntax deep-pattern
  (syntax-rules ()
    ((_ (a (b c) d) ...) (list (list a b c d) ...))))
(check "deep pattern match"
       (deep-pattern (1 (2 3) 4) (5 (6 7) 8))
       '((1 2 3 4) (5 6 7 8)))

;; 2.6 hygiene: 同一变量多宏嵌套
(define x 'global-x)
(define-syntax hy1
  (syntax-rules ()
    ((_) x)))
(define-syntax hy2
  (syntax-rules ()
    ((_) (hy1))))
(let ((x 'local-x))
  (check "hygiene two-level" (hy2) 'global-x))

;; 2.7 hygiene: let-syntax 阴影
(define-syntax ref-global
  (syntax-rules () ((_) y)))
(define y 'outer)
(let ((y 'inner))
  (let-syntax ((ref-local (syntax-rules () ((_) y))))
    (check "let-syntax shadows outer" (ref-local) 'inner)
    (check "ref-global still outer" (ref-global) 'outer)))

;; 2.8 syntax-rules 条件展开 (test literal in template)
(define-syntax test-literal
  (syntax-rules (then else)
    ((_ test then x else y) (if test x y))))
(check "test literal in syntax-rules" (test-literal #t then 1 else 2) 1)
(check "test literal else"            (test-literal #f then 1 else 2) 2)


;; =============================================================================
;; 3. syntax-case R6RS 压力测试
;; =============================================================================
(display ";; === 3. syntax-case R6RS stress ===\n")

;; 3.1 syntax-case 多分支 + fender
(define-syntax classify-number
  (lambda (x)
    (syntax-case x ()
      ((_ n)
       (and (integer? (syntax->datum #'n))
            (> (syntax->datum #'n) 0))
       #'(quote positive))
      ((_ n)
       (and (integer? (syntax->datum #'n))
            (< (syntax->datum #'n) 0))
       #'(quote negative))
      ((_ n)
       (integer? (syntax->datum #'n))
       #'(quote zero))
      ((_ n)
       #'(quote unknown)))))
(check "syntax-case fender positive"  (classify-number 5)   'positive)
(check "syntax-case fender negative"  (classify-number -3)  'negative)
(check "syntax-case fender zero"      (classify-number 0)   'zero)
(check "syntax-case fender unknown"   (classify-number 3.5) 'unknown)

;; 3.2 syntax-case 含 ellipsis
(define-syntax sum-via-syntax-case
  (lambda (x)
    (syntax-case x ()
      ((_ n ...)
       #'(apply + (list n ...))))))
(check "syntax-case + ellipsis" (sum-via-syntax-case 1 2 3 4) 10)

;; 3.3 syntax-case with empty ellipsis
(check "syntax-case empty ellipsis" (sum-via-syntax-case) 0)

;; 3.4 quasisyntax 基本使用
(define-syntax basic-qs
  (lambda (x)
    (syntax-case x ()
      ((_ a b)
       #`(list #,a #,b)))))
(check "basic quasisyntax" (basic-qs 1 2) '(1 2))

;; 3.4b datum->syntax 基本测试
(define-syntax ds-basic
  (lambda (x)
    (syntax-case x ()
      ((_ val)
       (with-syntax ((tag (datum->syntax #'x 'my-tag)))
         #'(quote tag))))))
(check "datum->syntax basic"
       (ds-basic 42) 'my-tag)

;; 3.5 syntax-case 多 fender 分支
(define-syntax scalar-or-pair
  (lambda (x)
    (syntax-case x ()
      ((_ lst)
       (pair? (syntax->datum #'lst))
       #'(quote pair))
      ((_ lst)
       (not (pair? (syntax->datum #'lst)))
       #'(quote scalar)))))
(check "syntax-case pair guard"
       (scalar-or-pair '(1 2 3)) 'pair)
(check "syntax-case scalar guard"
       (scalar-or-pair 42) 'scalar)

;; 3.6 syntax-case 绑定创建 (简化)
(define-syntax make-wrapper
  (lambda (x)
    (syntax-case x ()
      ((_ new-name)
       #'(define (new-name . args)
           (apply list args))))))
(make-wrapper wrap)
(check "syntax-case define wrapper"
       (wrap 1 2 3) '(1 2 3))


;; =============================================================================
;; 4. 混合宏系统交互 (define-macro + syntax-rules + syntax-case)
;; =============================================================================
(display ";; === 4. Hybrid macro interaction ===\n")

;; 4.1 syntax-rules 调用 define-macro 定义的宏
(define-macro (add2 a b) `(+ ,a ,b))
(define-syntax call-add2
  (syntax-rules ()
    ((_ x y) (add2 x y))))
(check "syntax-rules calls define-macro" (call-add2 3 4) 7)

;; 4.2 define-macro 展开出 syntax-rules
(define-macro (def-syntax-adder name)
  `(define-syntax ,name
     (syntax-rules ()
       ((_ a b) (+ a b)))))
(def-syntax-adder syn-add)
(check "define-macro defines syntax-rules" (syn-add 10 20) 30)

;; 4.3 syntax-case 直接展开 (dot 模式测试)
(define-syntax simple-wrap
  (lambda (x)
    (syntax-case x ()
      ((_ name val)
       #'(define (name) val)))))
(simple-wrap the-answer 42)
(check "syntax-case define zero-arg" (the-answer) 42)

;; 4.4 三系统链式展开
(define-syntax chain1
  (syntax-rules ()
    ((_ x) (identity x))))
(define-macro (identity x) x)
(define-syntax chain2
  (lambda (x)
    (syntax-case x ()
      ((_ y) #'(chain1 y)))))
(check "three-way macro chain" (chain2 (+ 1 2)) 3)


;; =============================================================================
;; 5. 数字 & 算术边缘场景
;; =============================================================================
(display ";; === 17. Complex macro depth test ===\n")

;; 模式匹配大量字句
(define-syntax match-color
  (syntax-rules (red green blue yellow cyan magenta black white orange purple)
    ((_ red)    'red)
    ((_ green)  'green)
    ((_ blue)   'blue)
    ((_ yellow) 'yellow)
    ((_ cyan)   'cyan)
    ((_ magenta)'magenta)
    ((_ black)  'black)
    ((_ white)  'white)
    ((_ orange) 'orange)
    ((_ purple) 'purple)
    ((_ other)  'unknown)))
(check "match many clauses" (match-color blue) 'blue)
(check "match fallthrough"  (match-color chartreuse) 'unknown)

;; 替换式 cond 大量分支
(define-syntax big-cond
  (syntax-rules (=> else)
    ((_) (error "big-cond: no clause matched"))
    ((_ (test => proc) rest ...)
     (let ((t test))
       (if t (proc t) (big-cond rest ...))))
    ((_ (else expr . more) rest ...)
     expr)
    ((_ (test expr) rest ...)
     (if test expr (big-cond rest ...)))))
(check "big-cond 10 clauses"
       (big-cond
         ((= 1 2) 'nope1)
         ((= 2 3) 'nope2)
         ((= 3 4) 'nope3)
         ((= 4 5) 'nope4)
         ((= 5 6) 'nope5)
         ((= 6 7) 'nope6)
         ((= 7 8) 'nope7)
         (else    'yes))
       'yes)


;; =============================================================================
;; 18. 空列表 & 边界值
(define-syntax my-when
  (syntax-rules ()
    ((_ test body1 body2 ...) (if test (begin body1 body2 ...)))))
(check "my-when true" (my-when #t 42) 42)
(check "my-when false" (my-when #f 42) (if #f 42))

(define-syntax swap!
  (syntax-rules ()
    ((_ a b) (let ((tmp a)) (set! a b) (set! b tmp)))))
(let ((x 1) (y 2))
  (swap! x y)
  (check "swap!" (+ (* x 10) y) 21))

(display "") (newline)
(display "===== 24. lambda / 闭包 =====") (newline)
(check "((lambda (x) x) 42)" ((lambda (x) x) 42) 42)
(define-syntax classify
  (syntax-rules ()
    ((_ 0) 'zero)
    ((_ 1) 'one)
    ((_ n) 'many)))
(check "classify 0" (classify 0) 'zero)
(check "classify 1" (classify 1) 'one)
(check "classify 2" (classify 2) 'many)

; 字面量
(define-syntax literal-test
  (syntax-rules (else)
    ((_ else body) (list 'got-else body))
    ((_ x) (list 'got-x x))))
(check "literal-test symbol" (literal-test 42) '(got-x 42))
(check "literal-test else" (literal-test else 99) '(got-else 99))

; 省略号多变量
(define-syntax zip-with
  (syntax-rules ()
    ((_ f (a ...) (b ...)) (map f '(a ...) '(b ...)))))
(check "zip-with +" (zip-with + (1 2 3) (10 20 30)) '(11 22 33))
(check "zip-with cons" (zip-with cons (a b c) (1 2 3)) '((a . 1) (b . 2) (c . 3)))

; 宏展开宏 (高阶宏)
(define-syntax def-curry
  (syntax-rules ()
    ((_ (name a b) body) (define (name a) (lambda (b) body)))))
(def-curry (add-cur x y) (+ x y))
(check "def-curry add" ((add-cur 3) 4) 7)

; syntax-rules 生成 syntax-rules
(define-syntax def-binary-op
  (syntax-rules ()
    ((_ op expr) (define-syntax op (syntax-rules () ((_ a b) expr))))))
;(def-binary-op add2 (+ a b))
;(check "def-binary-op add2" (add2 3 4) 7)

; 复杂模式: 嵌套省略号
(define-syntax deep-zip
  (syntax-rules ()
    ((_ (a ...) (b ...) (c ...)) (map list '(a ...) '(b ...) '(c ...)))))
(check "deep-zip 3 lists" (deep-zip (1 2) (a b) (x y)) '((1 a x) (2 b y)))

; 压力: 大展开
(define-syntax make-n-args
  (syntax-rules ()
    ((_ n) (make-n-args n 0))
    ((_ 0 acc) acc)
    ((_ n acc) (make-n-args (- n 1) (lambda (x . rest) ((if (= x n) (lambda (y) y) (lambda (y) (+ y 1))) acc))))))

(display "") (newline)
(display "===== 28. Lambda / 闭包 深度测试 =====") (newline)
; 闭包捕获链

; --- 模式: 字面量 else 在 cond 模拟中 ---
(define-syntax my-cond2
  (syntax-rules (else)
    ((_ (else body1 ...)) (begin body1 ...))
    ((_ (test body1 ...) rest ...) (if test (begin body1 ...) (my-cond2 rest ...)))))
(my-cond2 ((= 1 1) 'yes) (else 'no))

(check "my-cond2 true" (my-cond2 ((= 1 1) 'yes) (else 'no)) 'yes)
(check "my-cond2 false" (my-cond2 ((= 1 2) 'yes) (else 'no)) 'no)
(check "my-cond2 chain" (my-cond2 ((= 1 2) 'nope) ((= 2 3) 'nope) (else 'works)) 'works)


; --- 组合: syntax-rules + define-macro 互操作 (通过 define-macro 生成 syntax-rules)
(define-macro (def-const-syntax name val)
  `(define-syntax ,name
     (syntax-rules () ((_) ',val))))
(def-const-syntax my-version 42)
(check "def-const-syntax" (my-version) 42)

(check "(let loop ((i 5) (acc 1)) (if (= i 0) acc (loop (- i 1) (* acc i))))"
       (let loop ((i 5) (acc 1)) (if (= i 0) acc (loop (- i 1) (* acc i)))) 120)
(check "(make-string 3)" (make-string 3) "   ")

; --- 压力: syntax-rules 大量展开 ---
(define-syntax nop-syntax
  (syntax-rules ()
    ((_) 'undefined)))
(let loop ((i 0)) (when (< i 50) (nop-syntax) (nop-syntax) (nop-syntax) (loop (+ i 1))))
(check "syntax stress 150" #t #t)

(check "(-)" (-) 0)
(check "(*)" (*) 1)


; letrec + named-let (阶乘)
(define (fact-named n)
  (let loop ((i n) (acc 1))
    (if (= i 0) acc (loop (- i 1) (* acc i)))))
(check "named-let 阶乘" (fact-named 6) 720)


(display "===== 26. Quasiquote 深度测试 =====") (newline)
; 基本
(check "qq basic" `(a b c) '(a b c))
(check "qq unquote" `(1 ,(+ 1 1) 3) '(1 2 3))
(check "qq splice" `(a ,@'(b c) d) '(a b c d))

; 嵌套 quasiquote
(let ((x 5))
  (check "qq nesting d1" `(1 ,`(2 ,x) 3) '(1 (2 5) 3)))

; 多层解引用
(let ((a 1) (b 2) (c 3))
  (check "qq multi-unquote" `(,a ,b ,c) '(1 2 3)))

; splice 多层
(check "qq splice multi" `(1 ,@(map (lambda (x) (* x 2)) '(1 2 3)) 4) '(1 2 4 6 4))

; 深嵌套 combo: quasiquote + map + lambda
(check "qq + map + lambda"
       `(result ,@(map (lambda (x) (if (> x 0) 'pos 'neg)) '(1 -1 2 -2)))
       '(result pos neg pos neg))

; 压力: 大模板
(define qq-big (let loop ((i 100) (acc '()))
  (if (= i 0) `(done ,@acc) (loop (- i 1) (cons i acc)))))
(check "qq big template" (car qq-big) 'done)
(check "qq big length" (length qq-big) 101)

(display "") (newline)

; 压力: 深层递归中的 call/cc (适度规模)
(define (deep-find n target)
  (call/cc (lambda (exit)
    (let loop ((i 0))
      (if (= i n) #f
          (if (= i target) (exit i)
              (loop (+ i 1))))))))
(check "deep-find 0" (deep-find 1000 0) 0)
(check "deep-find mid" (deep-find 1000 500) 500)
(check "deep-find last" (deep-find 1000 999) 999)
(check "deep-find none" (deep-find 1000 2000) #f)

(display "") (newline)

(display "===== 21. Quasiquote =====") (newline)
(check "quasiquote simple" `(1 2 3) '(1 2 3))
(display "\n=== test-macros.scm ===\n")
;;; ============================================================
;;; scheme-macros.scm 完整功能测试
;;; 覆盖所有 define-syntax 宏的各类分支和边界情况
;;; ============================================================

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display " expected: ") (display expected)
             (display " actual: ") (display actual) (newline))))

;; ============================================================
;; 1. cond
;; ============================================================
(display "===== 1. cond =====") (newline)

;; cond else
(check "cond else" (cond (else 42)) 42)
(check "cond else multi" (cond (else 1 2 3)) 3)

;; cond test => proc
(check "cond => match" (cond ((+ 1 1) => (lambda (x) (* x 2)))) 4)
(check "cond => no match else" (cond (#f => (lambda (x) 'bad)) (else 'good)) 'good)

;; cond test result...
(check "cond match result" (cond ((> 3 1) 'yes)) 'yes)
(check "cond multi result" (cond ((> 3 1) 'a 'b 'c)) 'c)
(check "cond no match" (cond (#f 'yes) (else 'no)) 'no)
(check "cond skip false" (cond (#f 'fail) ((> 2 1) 'ok)) 'ok)

;; cond test (implicit -> test value)
(check "cond test only match" (cond ((+ 1 2))) 3)

;; cond empty -> #f
(check "cond empty" (cond) #f)

;; cond chain
(let ((x 2))
  (check "cond chain" (cond ((= x 1) 'one) ((= x 2) 'two) (else 'other)) 'two))

;; cond nested
(check "cond nested" (cond ((cond (#f #f) (else #t)) 'yes) (else 'no)) 'yes)

;; ============================================================
;; 2. case
;; ============================================================
(display "===== 2. case =====") (newline)

;; case else
(check "case else" (case 42 (else 'any)) 'any)
(check "case else multi" (case 42 (else 1 2 3)) 3)

;; case datum match
(check "case match" (case 'x ((x y z) 'found)) 'found)
(check "case no match else" (case 'w ((x y) 'found) (else 'not-found)) 'not-found)

;; case => proc
(check "case => match" (case 2 ((1 2 3) => (lambda (m) m)) (else #f)) 2)

;; case multi datum
(check "case multi-datum first" (case 1 ((1 2) 'ab) (else 'other)) 'ab)
(check "case multi-datum second" (case 2 ((1 2) 'ab) (else 'other)) 'ab)

;; case chain
(let ((x 5))
  (check "case chain" (case x ((1 2) 'small) ((3 4 5) 'medium) (else 'large)) 'medium))

;; case empty -> #f
(check "case empty" (case 'x) #f)
(check "case empty no match" (case 'x ((y z) 'found)) #f)

;; case single datum
(check "case single datum" (case 'a ((a) 'yes) (else 'no)) 'yes)

;; ============================================================
;; 3. do
;; ============================================================
(display "===== 3. do =====") (newline)

;; do countdown
(let ((result '()))
  (check "do countdown"
    (do ((i 5 (- i 1))) ((= i 0) (reverse result))
      (set! result (cons i result)))
    '(5 4 3 2 1)))

;; do sum
(let ((sum 0))
  (do ((i 1 (+ i 1))) ((> i 10) sum)
    (set! sum (+ sum i)))
  (check "do sum 1-10" sum 55))

;; do without step (reuses init)
(let ((result '()))
  (check "do no-step"
    (do ((x 5)) ((= x 0) (reverse result))
      (set! result (cons x result))
      (set! x (- x 1)))
    '(5 4 3 2 1)))

;; do with multiple vars
(do ((i 0 (+ i 1)) (j 10 (- j 1))) ((= i 5) (check "do multi-var" j 5))
  'noop)

;; ============================================================
;; 4. when
;; ============================================================
(display "===== 4. when =====") (newline)

(check "when true" (when #t 42) 42)
(check "when true multi" (when #t 1 2 3) 3)
(check "when false" (when #f 'bad) #f)
(check "when false side-effect" (let ((x 0)) (when #f (set! x 1)) x) 0)
(check "when true side-effect" (let ((x 0)) (when #t (set! x 1)) x) 1)

;; ============================================================
;; 5. unless
;; ============================================================
(display "===== 5. unless =====") (newline)

(check "unless false" (unless #f 42) 42)
(check "unless false multi" (unless #f 1 2 3) 3)
(check "unless true" (unless #t 'bad) #f)
(check "unless true side-effect" (let ((x 0)) (unless #t (set! x 1)) x) 0)
(check "unless false side-effect" (let ((x 0)) (unless #f (set! x 1)) x) 1)

;; ============================================================
;; 6. nth
;; ============================================================
(display "===== 6. nth =====") (newline)

(check "nth first" (nth 0 'a 'b 'c) 'a)
(check "nth second" (nth 1 'a 'b 'c) 'b)
(check "nth last" (nth 2 'a 'b 'c) 'c)
(check "nth single" (nth 0 'only) 'only)
(check "nth many" (nth 4 0 1 2 3 4 5) 4)

;; ============================================================
;; 7. if-not
;; ============================================================
(display "===== 7. if-not =====") (newline)

(check "if-not true" (if-not #t 'then 'else) 'else)
(check "if-not false" (if-not #f 'then 'else) 'then)
(check "if-not non-bool" (if-not 42 'then 'else) 'else)

;; ============================================================
;; 8. stream-cons
;; ============================================================
(display "===== 8. stream-cons =====") (newline)

(let ((s (stream-cons 1 (stream-cons 2 (stream-cons 3 '())))))
  (check "stream-cons head" (car s) 1)
  (check "stream-cons tail forced" (car (force (cdr s))) 2))

;; ============================================================
;; 9. fluid-let
;; ============================================================
(display "===== 9. fluid-let =====") (newline)

(let ((x 1) (y 2))
  (fluid-let ((x 10) (y 20))
    (check "fluid-let inside" (+ x y) 30))
  (check "fluid-let restored" (+ x y) 3))

(let ((x 1))
  (fluid-let ((x 2))
    (fluid-let ((x 3))
      (check "fluid-let nested" x 3))
    (check "fluid-let outer nested" x 2))
  (check "fluid-let restored nested" x 1))

;; ============================================================
;; 10. receive
;; ============================================================
(display "===== 10. receive =====") (newline)

(receive (a b) (values 10 20)
  (check "receive two values" (+ a b) 30))

(receive (x) (values 42)
  (check "receive single" x 42))

(receive (a b c) (values 1 2 3)
  (check "receive three" (+ a b c) 6))

;; ============================================================
;; 11. with-values
;; ============================================================
(display "===== 11. with-values =====") (newline)

(check "with-values add"
  (with-values (values 3 4) (lambda (a b) (+ a b)))
  7)

(check "with-values list"
  (with-values (values 'x 'y 'z) (lambda s s))
  '(x y z))

;; ============================================================
;; 12. cut (部分应用)
;; ============================================================
(display "===== 12. cut =====") (newline)

(let ((add5 (cut + 5 <>)))
  (check "cut add5" (add5 3) 8))

(let ((add (cut + <> <>)))
  (check "cut add" (add 3 4) 7))

(let ((double (cut * 2 <>)))
  (check "cut double" (double 5) 10))

(let ((sub-from-10 (cut - 10 <>)))
  (check "cut sub" (sub-from-10 3) 7))

;; ============================================================
;; 13. assume
;; ============================================================
(display "===== 13. assume =====") (newline)

(check "assume true" (assume #t) #t)
(check "assume true expr" (assume (= 1 1)) #t)
(let ((x 42)) (check "assume pass" (assume (number? x)) #t))

;; assume false -> error is raised, test via with-exception-handler
(let ((result
        (with-exception-handler
          (lambda (e) 'caught-error)
          (lambda () (assume #f) 'no-error))))
  (check "assume false raises error" result 'caught-error))

;; ============================================================
;; 14. and-let*
;; ============================================================
(display "===== 14. and-let* =====") (newline)

(check "and-let* empty -> #t" (and-let*) #t)
(check "and-let* no bindings" (and-let* () 42) 42)
(check "and-let* single binding pass" (and-let* ((x 42)) x) 42)
(check "and-let* single binding fail" (and-let* ((x #f)) x) #f)
(check "and-let* chain pass" (and-let* ((a 1) (b (+ a 2)) (c (+ b 3))) (+ a b c)) 10)
(check "and-let* chain fail" (and-let* ((a 1) (b #f) (c 3)) 'bad) #f)
(check "and-let* body" (and-let* ((x 5) (y 6)) (* x y)) 30)
(check "and-let* test-only clause" (and-let* ((x 5) ((positive? x))) (* x 2)) 10)
(check "and-let* test-only fail" (and-let* ((x -5) ((positive? x))) 'bad) #f)
(check "and-let* bare variable" (let ((x 42)) (and-let* (x) x)) 42)
(check "and-let* bare variable false" (let ((x #f)) (and-let* (x) 'bad) #f) #f)

;; ============================================================
;; 15. rec
;; ============================================================
(display "===== 15. rec =====") (newline)

(let ((fact (rec (fact n) (if (= n 0) 1 (* n (fact (- n 1)))))))
  (check "rec factorial 5" (fact 5) 120)
  (check "rec factorial 0" (fact 0) 1))

(let ((even? (rec (even? n) (or (= n 0) (odd? (- n 1)))))
      (odd? (rec (odd? n) (and (not (= n 0)) (even? (- n 1))))))
  (check "rec even? 4" (even? 4) #t)
  (check "rec odd? 5" (odd? 5) #t)
  (check "rec even? 3" (even? 3) #f))

;; ============================================================
;; 16. do-ec
;; ============================================================
(display "===== 16. do-ec =====") (newline)

(let ((result '()))
  (do-ec (set! result (cons 1 result)) (for i '(a b c)))
  (check "do-ec for" result '(1 1 1)))

(let ((result '()))
  (do-ec (set! result (cons i result)) (for i '(1 2 3)) (if (odd? i)))
  (check "do-ec for if" result '(3 1)))

;; ============================================================
;; 17. list-ec
;; ============================================================
(display "===== 17. list-ec =====") (newline)

(check "list-ec single" (list-ec 42) '(42))
(check "list-ec for" (list-ec (* i 2) (for i '(1 2 3 4))) '(2 4 6 8))
(check "list-ec for if" (list-ec i (for i '(1 2 3 4 5)) (if (odd? i))) '(1 3 5))
(check "list-ec for nested"
  (list-ec (list i j) (for i '(1 2)) (for j '(a b)))
  '((1 a) (1 b) (2 a) (2 b)))

;; ============================================================
;; 18. sum-ec
;; ============================================================
(display "===== 18. sum-ec =====") (newline)

(check "sum-ec single" (sum-ec 42) 42)
(check "sum-ec for" (sum-ec i (for i '(1 2 3 4 5))) 15)
(check "sum-ec for with if" (sum-ec i (for i '(1 2 3 4 5)) (if (odd? i))) 9)

;; ============================================================
;; 19. any?-ec
;; ============================================================
(display "===== 19. any?-ec =====") (newline)

(check "any?-ec for true" (any?-ec (= i 3) (for i '(1 2 3 4))) #t)
(check "any?-ec for false" (any?-ec (= i 10) (for i '(1 2 3 4))) #f)
(check "any?-ec for if" (any?-ec (and (number? i) (> i 2)) (for i '(1 2 3 4)) (if (odd? i))) #t)

;; ============================================================
;; 20. every?-ec
;; ============================================================
(display "===== 20. every?-ec =====") (newline)

(check "every?-ec for true" (every?-ec (number? i) (for i '(1 2 3 4))) #t)
(check "every?-ec for false" (every?-ec (positive? i) (for i '(1 -2 3))) #f)

;; ============================================================
;; 21. check
;; ============================================================
(display "===== 21. check =====") (newline)

(display "  (check output tested inline above)") (newline)

;; ============================================================
;; 22. check-ec
;; ============================================================
(display "===== 22. check-ec =====") (newline)

(display "  check-ec: ") (check-ec #t (for i '(1 2 3)) (= i i))
(newline)

;; ============================================================
;; 23. aif
;; ============================================================
(display "===== 23. aif =====") (newline)

(check "aif true then" (aif #t 'yes 'no) 'yes)
(check "aif false else" (aif #f 'yes 'no) 'no)
(let ((x 0)) (aif (set! x 1) 'then 'else) (check "aif side-effect" x 1))

;; it binding
(check "aif it binding" (aif (memv 3 '(1 2 3 4 5)) (car it) 'not-found) 3)
(check "aif it binding false" (aif (memv 10 '(1 2 3)) (car it) 'not-found) 'not-found)

;; ============================================================
;; 24. aand
;; ============================================================
(display "===== 24. aand =====") (newline)

(check "aand empty" (aand) #t)
(check "aand single" (aand 42) 42)
(check "aand chain true" (aand 1 2 3) 3)
(check "aand chain false" (aand 1 #f 3) #f)
(check "aand it binding" (aand (memv 2 '(1 2 3)) (memv (car it) '(1 2))) '(2))
(check "aand it short-circuit" (aand #f (car 'bad)) #f)

;; ============================================================
;; 25. alet
;; ============================================================
(display "===== 25. alet =====") (newline)

(check "alet simple" (alet ((x 1) (y 2)) (+ x y)) 3)
(check "alet body multi" (alet ((x 10)) (set! x (+ x 1)) x) 11)

;; ============================================================
;; 26. test-assert
;; ============================================================
(display "===== 26. test-assert =====") (newline)

(check "test-assert pass" (test-assert "dummy" #t) #t)

;; ============================================================
;; 27. test-equal
;; ============================================================
(display "===== 27. test-equal =====") (newline)

(test-equal "test-equal pass" '(1 2 3) '(1 2 3))

;; ============================================================
;; 28. test-approximate
;; ============================================================
(display "===== 28. test-approximate =====") (newline)

(test-approximate "test-approx pass" 3.14159 3.1416 0.001)

;; ============================================================
;; 29. define-immutable
;; ============================================================
(display "===== 29. define-immutable =====") (newline)

(define-immutable (square x) (* x x))
(check "define-immutable" (square 5) 25)

(define-immutable (greet name) (string-append "Hello, " name))
(check "define-immutable string" (greet "World") "Hello, World")

;; ============================================================
;; 30. dbind
;; ============================================================
(display "===== 30. dbind =====") (newline)

;; dbind empty pattern
(let ((val 42))
  (dbind () val (check "dbind empty" 'ok 'ok)))

;; dbind single (模式 (a) 将变量绑定到整个值，不拆解)
(let ((val 42))
  (dbind (a) val (check "dbind single" a 42)))

;; dbind two
(let ((val '(1 2)))
  (dbind (a b) val (check "dbind two" (+ a b) 3)))

;; dbind three
(let ((val '(10 20 30)))
  (dbind (a b c) val (check "dbind three" (+ a b c) 60)))

;; dbind improper
(let ((val '(1 2 3 4 5)))
  (dbind (a . b) val (check "dbind improper car" a 1)
         (check "dbind improper cdr" b '(2 3 4 5))))

;; dbind body multiple
(let ((val '(a b)))
  (dbind (x y) val
    (set! x 'changed)
    (check "dbind multi-body" (list x y) '(changed b))))

;; ============================================================
;; 31. 组合/嵌套/高阶测试
;; ============================================================
(display "===== 31. 组合/嵌套/高阶测试 =====") (newline)

;; --- fluid-let x cut ---
(let ((x 10))
  (let ((add-x (cut + x <>)))
    (fluid-let ((x 100))
      (check "fluid-let cut captures dynamic binding" (add-x 5) 105))
    (check "fluid-let cut restored" (add-x 5) 15)))

;; --- do x when/unless ---
(let ((vals '()))
  (do ((i 0 (+ i 1))) ((= i 5) (check "do when collects evens" (reverse vals) '(0 2 4)))
    (when (even? i)
      (set! vals (cons i vals)))))

(let ((vals '()))
  (do ((i 0 (+ i 1))) ((= i 5) (check "do unless skips evens" (reverse vals) '(1 3)))
    (unless (even? i)
      (set! vals (cons i vals)))))

;; --- case x cut ---
(let ((f (case 'add
           ((add)      (cut + <> <>))
           ((subtract) (cut - <> <>))
           (else       (cut * <> <>)))))
  (check "case returns cut procedure" (f 7 3) 10))

;; --- cond x with-values ---
(cond
  ((> 5 10) 'bad)
  ((with-values (values 1 2) (lambda (a b) (< a b)))
   => (lambda (v)
        (check "cond => with with-values inside test" v #t)))
  (else 'bad))

;; --- and-let* x cond ---
(let ((x 5) (y 15) (z 0))
  (check "and-let* with cond guard"
    (and-let* ((a (+ x y))
               ((> a 10))
               (b (- a x)))
      (cond ((> b 10) 'big) (else 'small)))
    'big))

;; --- aif -> aand -> alet chained ---
(let ((lst '(1 2 3 4 5)))
  (check "aif car of found result"
    (aif (memv 3 lst)
      (aand (cdr it) (car it))
      'not-found)
    4))

(let ((lst '(10 20 30)))
  (check "alet body wraps aif result"
    (alet ((it (aif (car lst) it 'fallback)))
      (* it 2))
    20))

;; --- dbind x rec (mutual recursion) ---
(let ((swap-pairs
        (rec (swap tree)
          (cond ((null? tree) '())
                ((pair? tree)
                 (dbind (a . d) tree
                   (cons (cons (cadr tree) (car tree))
                         (swap (cddr tree)))))))))
  (check "dbind in rec swaps adjacent pairs"
    (swap-pairs '(a b c d e f))
    '((b . a) (d . c) (f . e))))

;; --- list-ec x when (filter without collecting failures) ---
(check "list-ec with nested when"
  (list-ec (* i 10) (for i '(-3 -2 -1 0 1 2 3)) (if (and (positive? i) (not (zero? i)))))
  '(10 20 30))

;; --- deep when/unless nesting ---
(let ((x 0) (y 0))
  (when #t
    (unless #f
      (when (> 3 1)
        (set! x 10) (set! y 20))))
  (check "nested when/unless/when" (+ x y) 30))

;; --- cond nesting case nesting cond ---
(let ((key 'b))
  (check "cond nesting in case in cond"
    (cond ((case key
             ((a) #t)
             ((b) (cond ((> 3 2) 'found-b)))
             (else #f))
           => (lambda (v) (if (eq? v 'found-b) 'ok 'bad)))
          (else 'bad))
    'ok))

;; --- cut x map (functional composition) ---
(let ((add-10 (cut + 10 <>)))
  (check "cut with map" (map add-10 '(1 2 3 4)) '(11 12 13 14)))

(let ((car-of (cut car <>)))
  (check "cut destructures pair" (map car-of '((1 2) (3 4) (5 6))) '(1 3 5)))

;; --- cut x apply ---
(let ((sum-all (cut apply + <>)))
  (check "cut with apply" (sum-all '(1 2 3 4 5)) 15))

;; --- fluid-let x assume x with-values ---
(let ((val 42))
  (with-values (fluid-let ((val 100))
                 (assume (number? val))
                 (values val val))
    (lambda (x y)
      (check "fluid-let with assume and with-values" (+ x y) 200))))

;; --- and-let* x fluid-let (triple binding scope) ---
(let ((x 'outer))
  (check "and-let* captures fluid-let rebinding"
    (and-let* ((orig x)
               ((fluid-let ((x 'inner))
                  (eq? x 'inner)))
               (after x))
      (list orig after))
    '(outer outer))
  (check "fluid-let restored after and-let*" x 'outer))

;; --- cut x cond (branching closures) ---
(let ((f (case 'mul
           ((add) (cut + <> <>))
           ((sub) (cut - <> <>))
           ((mul) (cut * <> <>))
           ((div) (cut / <> <>))
           (else (lambda (a b) 0)))))
  (check "cut from case conditional" (f 6 7) 42))

;; --- stream-cons x rec x and-let* ---
(let ((ints-from
        (rec (from n)
          (stream-cons n (from (+ n 1))))))
  (let ((nats (ints-from 1)))
    (check "stream-cons rec first" (car nats) 1)
    (check "stream-cons rec second" (car (force (cdr nats))) 2)
    (and-let* ((s (force (cdr (force (cdr nats))))))
      (check "stream-cons rec third" (car s) 3))))

;; --- receive x cut x values ---
(receive (f g)
    (values (cut + 1 <>) (cut - <> 1))
  (check "receive binds cut closures" (f (g 10)) 10))

;; --- nested do with fluid-let inside ---
(let ((x 0))
  (do ((i 0 (+ i 1))) ((= i 5)   (check "nested do fluid-let" x 100))
    (fluid-let ((i (* i 10)))
      (set! x (+ x i)))))

;; --- fluid-let x rec dynamic capture ---
(let ((base 1))
  (define (scale n)
    (* base n))
  (check "fluid-let rec dynamic" (fluid-let ((base 10)) (scale 7)) 70)
  (check "fluid-let rec restored" (scale 7) 7))

;; --- do-ec x cut x when ---
(let ((result '()))
  (do-ec (when (odd? i)
           (set! result (cons ((cut * 10 <>) i) result)))
    (for i '(1 2 3 4 5)))
  (check "do-ec when cut" (reverse result) '(10 30 50)))

;; --- and-let* x receive x cond ---
(let ((x 15))
  (check "and-let* with receive test"
    (and-let* ((n x)
               ((receive (a b) (values 10 20) (< n (+ a b)))))
      (cond ((> n 20) 'big)
            ((> n 10) 'medium)
            (else 'small)))
    'medium))

;; --- fluid-let x cond arrow (requires outer binding for fluid-let) ---
(let ((x 2))
  (fluid-let ((x 3))
    (cond ((+ x 1) => (lambda (v)
                        (check "cond arrow within fluid-let" v 4)))
          (else 'bad))))

;; --- cut with procedure from higher-order func ---
(let ((adder (lambda (n) (cut + n <>))))
  (let ((add5 (adder 5)))
    (check "cut with higher-order" (add5 3) 8)))

;; --- list-ec with nested cut ---
(check "list-ec with cut"
  (list-ec ((cut - 10 <>) i) (for i '(1 2 3 4)))
  '(9 8 7 6))

;; --- aif nesting (avoid hygiene interactions) ---
(check "aif nested usage"
  (aif (list 1 2 3)
    (car it)
    'bad)
  1)

;; ============================================================
(display "===== 全部测试完成 =====") (newline)


(display "\n=== test-macro-tutorials.scm ===\n")
;; test-macro-tutorials.scm — merged macro tutorial files


(display "\n=== 01-basic-patterns.scm ===\n")
;; 01-basic-patterns.scm — syntax-case 基础模式匹配

(define-syntax swap
  (lambda (stx)
    (syntax-case stx ()
      ((_ a b)
       (syntax (let ((tmp a)) (set! a b) (set! b tmp)))))))

(define-syntax when
  (lambda (stx)
    (syntax-case stx ()
      ((_ test body ...)
       (syntax (if test (begin body ...)))))))

(define-syntax unless
  (lambda (stx)
    (syntax-case stx ()
      ((_ test body ...)
       (syntax (if (not test) (begin body ...)))))))

(define-syntax or*
  (lambda (stx)
    (syntax-case stx ()
      ((_) (syntax #f))
      ((_ x) (syntax x))
      ((_ x y ...)
       (syntax (let ((t x)) (if t t (or* y ...))))))))

(define-syntax define-curried
  (lambda (stx)
    (syntax-case stx ()
      ((_ (f a ...) body body* ...)
       (syntax (define f (lambda (a ...) body body* ...)))))))

(define x 1)
(define y 2)
(when (> x 0) (set! x (+ x 1)))
(unless (> y 10) (set! y (+ y 10)))
(swap x y)
(or* #f #f 42)
(define-curried (add3 a b c) (+ a b c))


(display "\n=== 02-ellipsis.scm ===\n")
;; 02-ellipsis.scm — syntax-case 省略号模式

(define-syntax explain
  (lambda (stx)
    (syntax-case stx ()
      ((_ tag a ...)
       (syntax (begin (display tag) (display ": ") (display (list a ...)) (newline)))))))

(define-syntax list-of
  (lambda (stx)
    (syntax-case stx ()
      ((_ elt ...)
       (syntax (list elt ...))))))

(define-syntax define-vector
  (lambda (stx)
    (syntax-case stx ()
      ((_ name elt ...)
       (syntax (define name (vector elt ...)))))))

(define-syntax define-enum
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (member ...))
       (syntax (define name (quote (member ...))))))))

(define-syntax for
  (lambda (stx)
    (syntax-case stx ()
      ((_ (i from to) body ...)
       (syntax (do ((i from (+ i 1))) ((> i to)) body ...))))))

(define-syntax multiple-set!
  (lambda (stx)
    (syntax-case stx ()
      ((_ (var ...) (val ...))
       (syntax (begin (set! var val) ...))))))

(define-syntax match-pairs
  (lambda (stx)
    (syntax-case stx ()
      ((_ (key val) ...)
       (syntax '((key . val) ...))))))

(explain "ellipsis" 10 20 30)
(list-of 1 2 3 4 5)
(define a 'a) (define b 'b) (define c 'c)
(define-vector vec-abc a b c)
(define-enum color (red green blue))
(for (i 1 5) (display i) (newline))
(define a 0) (define b 0) (multiple-set! (a b) (1 2))


(display "\n=== 03-fenders.scm ===\n")
;; 03-fenders.scm — syntax-case 护卫

(define-syntax assert-type
  (lambda (stx)
    (syntax-case stx ()
      ((_ name val type)
       (syntax (let ((v val))
                 (unless (type v)
                   (error (quote name) "type mismatch" v))
                 v))))))

(define-syntax define-checked
  (lambda (stx)
    (syntax-case stx ()
      ((_ (name arg ...) body body* ...)
       (syntax (define name (lambda (arg ...) body body* ...)))))))

(define-syntax lambda/arity
  (lambda (stx)
    (syntax-case stx ()
      ((_ (a b) body)
       (syntax (lambda (a b) body))))))

(define-syntax define-option
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (opt val) ...)
       (syntax (define name (list (quote opt) ...)))))))

(define-syntax check-positive
  (lambda (stx)
    (syntax-case stx ()
      ((_ val)
       (syntax (let ((v val)) (if (< v 0) (error "negative" v) v)))))))

(assert-type my-add 42 number?)
(define-checked (double x) (* x 2))
(lambda/arity (a b) (+ a b))
(define-option config (host "localhost") (port 8080))
(check-positive 5)


(display "\n=== 04-with-syntax.scm ===\n")
;; 04-with-syntax.scm — with-syntax 临时绑定

(define-syntax define-struct
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (field ...))
       (with-syntax
         (((make-name ...)
           (map (lambda (f)
                  (datum->syntax #'name
                    (string->symbol
                      (string-append "make-" (symbol->string f)))))
                #'(field ...)))
          ((name? ...)
           (map (lambda (f)
                  (datum->syntax #'name
                    (string->symbol
                      (string-append (symbol->string f) "?"))))
                #'(field ...))))
         (syntax
           (begin
             (define name (lambda (field ...) (list field ...)))
             (define (make-name x) (list 'name x)) ...
             (define (name? x) (and (pair? x) (eq? (car x) 'name))) ...)))))))

(define-syntax hash-let
  (lambda (stx)
    (syntax-case stx ()
      ((_ ht ((key var) ...) body ...)
       (syntax (let ((var (hash-table-ref ht 'key)) ...) body ...))))))

(define-syntax with-file-lines
  (lambda (stx)
    (syntax-case stx ()
      ((_ (var filename) body ...)
       (syntax
         (let ((var (call-with-input-file filename
                      (lambda (p)
                        (let loop ((line (read-line p)) (lines '()))
                          (if (eof-object? line)
                            (reverse lines)
                            (loop (read-line p) (cons line lines))))))))
           body ...))))))

(define-syntax define-accessors
  (lambda (stx)
    (syntax-case stx ()
      ((_ (getter ...) vec)
       (with-syntax
         (((idx ...) (iota (length #'(getter ...)))))
         (syntax
           (begin
             (define (getter vec) (vector-ref vec idx)) ...)))))))

(define-syntax time-it
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr)
       (syntax
         (let ((start (current-second)))
           (let ((val expr))
             (display "elapsed: ")
             (display (- (current-second) start))
             (newline)
             val)))))))


(display "\n=== 05-quasisyntax.scm ===\n")
;; 05-quasisyntax.scm — quasisyntax / unsyntax / unsyntax-splicing

(define-syntax define-infix
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (left op right) body)
       (quasisyntax
         (define name
           (lambda (left right)
             (unsyntax body))))))))

(define-syntax splice-example
  (lambda (stx)
    (syntax-case stx ()
      ((_ a b c)
       (quasisyntax
         (list (unsyntax #'a) (unsyntax #'b) (unsyntax #'c)))))))

(define-syntax def-wrapper
  (lambda (stx)
    (syntax-case stx ()
      ((_ name value)
       (quasisyntax
         (begin
           (display "defining ")
           (display (quote (unsyntax #'name)))
           (newline)
           (define name (unsyntax #'value))))))))

(define-syntax labeled-lambda
  (lambda (stx)
    (syntax-case stx ()
      ((_ name args body)
       (quasisyntax
         (letrec ((name (lambda args
                          (unsyntax #'body))))
           name))))))

(define-infix add (a + b) (+ a b))
(define x 1) (define y 2) (define z 3)
(splice-example x y z)
(def-wrapper greet "hello")
(define add2 (labeled-lambda add2 (x) (+ x 2)))


(display "\n=== 06-nested-patterns.scm ===\n")
;; 06-nested-patterns.scm — 嵌套模式匹配

(define-syntax destructure-let
  (lambda (stx)
    (syntax-case stx ()
      ((_ ((a . b) expr) body ...)
       (syntax (let ((tmp expr)) (let ((a (car tmp)) (b (cdr tmp))) body ...)))))))

(define-syntax destructure-list
  (lambda (stx)
    (syntax-case stx ()
      ((_ ((a b . rest) expr) body ...)
       (syntax (let ((tmp expr)) (let ((a (car tmp)) (b (cadr tmp)) (rest (cddr tmp))) body ...)))))))

(define-syntax match-tree
  (lambda (stx)
    (syntax-case stx ()
      ((_ ((left . right) expr) body ...)
       (syntax
         (let ((t expr))
           (let ((left (car t)) (right (cdr t)))
             body ...)))))))

(define-syntax define-ppair
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (car-expr . cdr-expr))
       (syntax (define name (cons car-expr cdr-expr)))))))

(define-syntax pattern-match
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr
         ((a b) body1)
         ((a . b) body2)
         (_ body3))
       (syntax
         (let ((v expr))
           (cond
             ((and (list? v) (= (length v) 2)) (let ((a (car v)) (b (cadr v))) body1))
             ((pair? v) (let ((a (car v)) (b (cdr v))) body2))
             (else body3))))))))

(define-syntax nested-let
  (lambda (stx)
    (syntax-case stx ()
      ((_ (((a b) (c d)) expr) body ...)
       (syntax
         (let ((tmp expr))
           (let ((a (caar tmp)) (b (cdar tmp))
                 (c (caadr tmp)) (d (cdadr tmp)))
             body ...)))))))

(destructure-let ((x . y) (cons 1 2)) (list x y))
(destructure-list ((a b . rest) (list 1 2 3 4)) (list a b rest))
(match-tree ((a . b) (cons 'x 'y)) (list a b))
(define-ppair my-pair (1 . 2))
(nested-let (((a b) (c d)) '((1 . 2) (3 . 4))) (list a b c d))


(display "\n=== 07-hygiene.scm ===\n")
;; 07-hygiene.scm — syntax-case 卫生宏

(define-syntax let-it
  (lambda (stx)
    (syntax-case stx ()
      ((_ val body ...)
       (syntax (let ((it val)) body ...))))))

(define-syntax anaphoric-if
  (lambda (stx)
    (syntax-case stx ()
      ((_ test then else)
       (syntax (let ((it test)) (if it then else)))))))

(define-syntax define-unhygienic
  (lambda (stx)
    (syntax-case stx ()
      ((_ name val)
       (quasisyntax
         (define name (unsyntax (datum->syntax #'name (quote val)))))))))

(define-syntax with-temp
  (lambda (stx)
    (syntax-case stx ()
      ((_ body ...)
       (syntax
         (let ((temp (make-string 0)))
           (dynamic-wind
             (lambda () (set! temp (string-copy "tmp")))
             (lambda () body ...)
             (lambda () (set! temp #f)))))))))

(define-syntax define-private
  (lambda (stx)
    (syntax-case stx ()
      ((_ name val)
       (with-syntax ((hidden (datum->syntax #'name
                                (string->symbol
                                  (string-append "%" (symbol->string (syntax->datum #'name)))))))
         (syntax (define hidden val)))))))

(define-syntax rename
  (lambda (stx)
    (syntax-case stx ()
      ((_ (orig new) body ...)
       (syntax
         (let-syntax ((new (lambda (stx)
                             (syntax-case stx ()
                               ((_ args ...)
                                (syntax (orig args ...)))))))
           body ...))))))

(let-it (* 2 3) (display it) (newline))
(anaphoric-if (> 3 1) (display it) (display "no"))
(with-temp (display "in dynamic-wind"))
(define-private secret 42)


(display "\n=== 08-let-syntax.scm ===\n")
;; 08-let-syntax.scm — let-syntax / letrec-syntax

(define (generate-pair)
  (let-syntax ((pair-of (lambda (stx)
                          (syntax-case stx ()
                            ((_ type)
                             (syntax (cons (type) (type))))))))
    (pair-of number)))

(let-syntax ((my-let (lambda (stx)
                       (syntax-case stx ()
                         ((_ ((var val) ...) body ...)
                          (syntax ((lambda (var ...) body ...) val ...)))))))
  (my-let ((x 10) (y 20)) (+ x y)))

(letrec-syntax ((or-macro (lambda (stx)
                            (syntax-case stx ()
                              ((_) (syntax #f))
                              ((_ x) (syntax x))
                              ((_ x y ...)
                               (syntax (let ((t x)) (if t t (or-macro y ...)))))))))
  (or-macro #f #f 42))

(let-syntax ((define-inline (lambda (stx)
                              (syntax-case stx ()
                                ((_ (name args ...) body)
                                 (syntax
                                   (define (name args ...) body)))))))
  (define-inline (square x) (* x x))
  (square 5))

(let-syntax ((unless-macro (lambda (stx)
                             (syntax-case stx ()
                               ((_ test body ...)
                                (syntax (if (not test) (begin body ...))))))))
  (define my-val 0)
  (unless-macro (= my-val 1) (set! my-val 1)))

(let-syntax ((define-counter (lambda (stx)
                               (syntax-case stx ()
                                 ((_ name inc reset)
                                  (syntax
                                    (begin
                                      (define name 0)
                                      (define (inc) (set! name (+ name 1)))
                                      (define (reset) (set! name 0)))))))))
  (define-counter count count-inc count-reset)
  (count-inc)
  (count-inc)
  count)

(letrec-syntax ((loop-macro (lambda (stx)
                              (syntax-case stx ()
                                ((_ 0 body ...)
                                 (syntax (begin body ...)))
                                ((_ n body ...)
                                 (syntax (if (> n 0)
                                           (begin body ... (loop-macro (- n 1) body ...))
                                           (void))))))))
  (loop-macro 3 (display "hi ") (newline)))


(display "\n=== 09-procedural.scm ===\n")
;; 09-procedural.scm — 过程式 syntax transformer

(define-syntax identity-macro
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr) (syntax expr)))))

(define-syntax debug-write
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr)
       (begin
         (display ";; expanding: ") (display (syntax->datum #'expr)) (newline)
         (syntax expr))))))

(define-syntax capture-raw
  (lambda (stx)
    (syntax-case stx ()
      ((_ . rest)
       (syntax (quote (syntax->datum #'(rest))))))))

(define-syntax wrap-in-list
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr ...)
       (syntax (quote (expr ...)))))))

(define-syntax eval-at-macro-time
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr)
       (let ((val (eval (syntax->datum #'expr))))
         (syntax (quote val)))))))

(define-syntax make-resolver
  (lambda (stx)
    (syntax-case stx ()
      ((_ name table)
       (let* ((tbl (syntax->datum #'table))
              (keys (map car tbl)))
         (with-syntax (((k ...) (map (lambda (k) (datum->syntax #'name k)) keys)))
           (syntax
             (lambda (key)
               (case key
                 ((k ...)
                  (apply (lambda (x) (error "not found")) '()))
                 (else #f))))))))))

(define-syntax trace-calls
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr)
       (let ((datum (syntax->datum #'expr)))
         (display ";; traced: ") (display datum) (newline)
         (syntax expr))))))

(identity-macro (+ 1 2))
(debug-write (* 3 4))
(capture-raw (+ 1 2 3))
(wrap-in-list a b c)
(eval-at-macro-time (+ 1 2 3))
(trace-calls (list 1 2 3))


(display "\n=== m01-basic.scm ===\n")
;; 01-basic.scm — define-macro 基础用法

(define-macro (twice . body)
  (cons 'begin (append body body)))

(define-macro (when test . body)
  (list 'if test (cons 'begin body) (if #f #f)))

(define-macro (unless test . body)
  (list 'if test (if #f #f) (cons 'begin body)))

(define-macro (swap a b)
  (list 'let (list (list 'tmp a))
    (list 'set! a b)
    (list 'set! b 'tmp)))

(define-macro (defun name args . body)
  (cons 'define (cons (cons name args) body)))

(define-macro (my-let bindings . body)
  (let ((vars (map car bindings))
        (vals (map cadr bindings)))
    (cons (cons 'lambda (cons vars body)) vals)))

(define-macro (my-and . args)
  (cond
    ((null? args) '#t)
    ((null? (cdr args)) (car args))
    (else (list 'if (car args) (cons 'my-and (cdr args)) '#f))))

(twice (display "hello") (newline))
(when (> 3 1) (display "yes") (newline))
(unless (> 1 3) (display "works") (newline))
(define a 1) (define b 2)
(swap a b)
(display a) (newline) (display b) (newline)
(defun greet (name) (display "hello ") (display name) (newline))
(greet "world")
(my-let ((x 10) (y 20)) (+ x y))


(display "\n=== m02-quasiquote.scm ===\n")
;; 02-quasiquote.scm — 反引用/逗号模式构建代码

(define-macro (my-if test then else)
  `(if ,test ,then ,else))

(define-macro (my-cond . clauses)
  (if (null? clauses)
    (if #f #f)
    (let ((first (car clauses)))
      (if (eq? (car first) 'else)
        `(begin ,@(cdr first))
        `(if ,(car first)
           (begin ,@(cdr first))
           (my-cond ,@(cdr clauses)))))))

(define-macro (my-let* bindings . body)
  (if (null? bindings)
    `(begin ,@body)
    (let ((b (car bindings)))
      `(let ((,(car b) ,(cadr b)))
         (my-let* ,(cdr bindings) ,@body)))))

(define-macro (my-lambda args . body)
  `(lambda ,args ,@body))

(define-macro (defun* name (arg . rest) . body)
  `(define ,name
     (lambda (,arg . ,rest) ,@body)))

(define-macro (with-gensyms (syms) . body)
  (let ((news (map (lambda (s) (list s (list 'gensym))) syms)))
    `(let ,news ,@body)))

(define-macro (my-dotimes (var count) . body)
  `(do ((,var 0 (+ ,var 1))) ((>= ,var ,count)) ,@body))

(define-macro (my-do-ec . clauses)
  (let ((header (car clauses))
        (body (cdr clauses)))
    `(begin (do ((,(car header) 0 (+ ,(car header) 1)))
                ((>= ,(car header) ,(cadr header))))
             ,@body)))

(define-macro (my-while test . body)
  `(let loop ()
     (if ,test
       (begin ,@body (loop))
       (if #f #f))))

(define-macro (my-until test . body)
  `(let loop ()
     (begin ,@body
       (if ,test (if #f #f) (loop)))))

(my-if (> 3 1) (display "true") (display "false"))
(newline)
(my-cond ((> 3 1) (display "a")) ((> 1 3) (display "b")) (else (display "c")))
(newline)
(my-let* ((x 1) (y (+ x 1))) (display y)) (newline)
(my-while #f (display "never"))
(define counter 3)
(my-until (= counter 0) (display counter) (set! counter (- counter 1)))
(newline)


(display "\n=== m03-rest-args.scm ===\n")
;; 03-rest-args.scm — 可变参数与解构

(define-macro (define-keyword name (key val) . rest)
  (let ((body (if (null? rest) val (cons 'begin rest))))
    `(begin
       (define ,name (make-hash-table))
       (hash-table-set! ,name (quote ,key) ,body))))

(define-macro (my-match val . clauses)
  (let loop ((cs clauses))
    (if (null? cs)
      (if #f #f)
      (let ((c (car cs)))
        `(if (equal? ,val (quote ,(car c)))
           (begin ,@(cdr c))
           ,(loop (cdr cs)))))))

(define-macro (my-letrec bindings . body)
  (let ((vars (map car bindings))
        (vals (map (lambda (b) (if (pair? (cadr b)) (list 'lambda (cdadr b) (car (cddadr b))) (cadr b))) bindings)))
    (cons (cons 'letrec (cons (map list vars vals) nil)) body)))

(define-macro (my-delay expr)
  `(make-promise (lambda () ,expr)))

(define-macro (my-parameterize ((param val)) . body)
  `(let ((old ,param))
     (set! ,param ,val)
     (let ((result (begin ,@body)))
       (set! ,param old)
       result)))

(define-macro (my-inc! var . rest)
  (let ((amount (if (null? rest) 1 (car rest))))
    `(set! ,var (+ ,var ,amount))))

(define-macro (my-push! val list-var)
  `(set! ,list-var (cons ,val ,list-var)))

(define-macro (my-pop! list-var)
  (let ((tmp (gensym)))
    `(let ((,tmp (car ,list-var)))
       (set! ,list-var (cdr ,list-var))
       ,tmp)))

(define count 0)
(my-inc! count)
(my-inc! count 5)
(display count) (newline)

(define my-list (quote ()))
(my-push! 1 my-list)
(my-push! 2 my-list)
(display my-list) (newline)
(display (my-pop! my-list)) (newline)
(display my-list) (newline)


(display "\n=== m04-gensym.scm ===\n")
;; 04-gensym.scm — 符号生成（避免使用不存在的 gensym）

(define-macro (my-swap-hygienic a b)
  `(let ((tmp ,a))
     (set! ,a ,b)
     (set! ,b tmp)))

(define-macro (my-with-temp expr . body)
  `(let ((tmp ,expr))
     (display "temp: ") (display tmp) (newline)
     ,@body))

(define-macro (my-delay-once val . body)
  `(let ((done #f) (result (if #f #f)))
     (begin
       (set! result ,val)
       ,@body)))

(define-macro (my-valof expr)
  `(let ((v ,expr)) v))

(define x 1) (define y 2)
(my-swap-hygienic x y)
(display x) (display y) (newline)
(my-with-temp (* 2 3))
(display (my-valof (+ 2 3))) (newline)


(display "\n=== m05-recursive.scm ===\n")
;; 05-recursive.scm — 递归宏

(define-macro (my-list . args)
  (if (null? args)
    (quote ())
    `(cons ,(car args) (my-list ,@(cdr args)))))

(define-macro (my-map fn . lists)
  (let ((x (gensym)))
    `(let ((,x ,(car lists)))
       (if (null? ,x)
         (quote ())
         (cons (,fn (car ,x))
               (my-map ,fn ,@(map cdr lists)))))))

(define-macro (my-filter pred lst)
  (let ((x (gensym)))
    `(let ((,x ,lst))
       (if (null? ,x)
         (quote ())
         (if (,pred (car ,x))
           (cons (car ,x) (my-filter ,pred (cdr ,x)))
           (my-filter ,pred (cdr ,x)))))))

(define-macro (my-nth n lst)
  (if (= n 0)
    `(car ,lst)
    `(my-nth ,(- n 1) (cdr ,lst))))

(define-macro (my-take n lst)
  (if (= n 0)
    (quote ())
    `(cons (car ,lst) (my-take ,(- n 1) (cdr ,lst)))))

(display (my-list 1 2 3 4 5)) (newline)
(display (my-nth 2 (quote (a b c d)))) (newline)
(display (my-take 3 (quote (a b c d e)))) (newline)


(display "\n=== m06-macro-compose.scm ===\n")
;; 06-macro-compose.scm — 宏组合

(define-macro (my-let-it val . body)
  `(let ((it ,val)) ,@body))

(define-macro (my-when test . body)
  `(if ,test (begin ,@body) (if #f #f)))

(define-macro (my-aif test then else)
  `(let ((it ,test)) (if it ,then ,else)))

(define-macro (my-awhen test . body)
  `(let ((it ,test)) (if it (begin ,@body) (if #f #f))))

(define-macro (my-define-func name args . body)
  `(define (,name ,@args) ,@body))

(define-macro (my-with-open-file (var filename) . body)
  `(let ((,var (open-input-file ,filename)))
     (let ((result (begin ,@body)))
       (close-port ,var)
       result)))

(define-macro (my-time . body)
  `(let ((start (current-second)))
     (let ((result (begin ,@body)))
       (display "elapsed: ")
       (display (- (current-second) start))
       (newline)
       result)))

(define-macro (my-assert-expr expr)
  `(if (not ,expr)
     (error "assertion failed:" (quote ,expr))
     (if #f #f)))

(define-macro (my-ensure test . body)
  `(if (not ,test)
     (error "ensure failed:" (quote ,test))
     (begin ,@body)))

(define-macro (my-named-let name bindings . body)
  (let ((vars (map car bindings))
        (vals (map cadr bindings)))
    `(letrec ((,name (lambda ,vars ,@body)))
       (,name ,@vals))))

(my-define-func double (x) (* x 2))
(display (double 5)) (newline)
(my-assert-expr (> 3 1))
(my-ensure (= 2 2) (display "ok") (newline))
(my-named-let loop ((i 0) (acc 1)) (if (< i 5) (loop (+ i 1) (* acc 2)) acc))
(display (my-time (* 1 2 3 4 5 6))) (newline)


(display "\n=== m07-destructure.scm ===\n")
;; 07-destructure.scm — 手动解构参数

(define-macro (my-with-car+cdr pair-expr . body)
  `(let ((tmp ,pair-expr))
     (let ((a (car tmp)) (d (cdr tmp)))
       ,@body)))

(define-macro (my-let-values vars expr . body)
  `(call-with-values (lambda () ,expr)
     (lambda ,vars ,@body)))

(my-with-car+cdr (cons 1 2) (display a) (display d) (newline))
(my-let-values (x y) (values 1 2) (display x) (display y) (newline))


(display "\n=== m08-computed.scm ===\n")
;; 08-computed.scm — 展开时计算

(define-macro (my-macro-time-add a b)
  (let ((sum (+ a b)))
    `(quote ,sum)))

(define-macro (my-factorial-computed n)
  (let loop ((i n) (acc 1))
    (if (= i 0)
      `(quote ,acc)
      (loop (- i 1) (* acc i)))))

(define-macro (my-table . pairs)
  `(quote (,@pairs)))

(display (my-macro-time-add 40 2)) (newline)
(display (my-factorial-computed 5)) (newline)
(display (my-table a b c)) (newline)


(display "\n=== m09-call-transformer.scm ===\n")
;; 09-call-transformer.scm — 调用其他宏的宏

(define-macro (my-define-curried name args . body)
  (letrec ((make-curry
             (lambda (args body)
               (if (null? args) body
                 `(lambda (,(car args))
                    ,(make-curry (cdr args) `(begin ,@body)))))))
    `(define ,name ,(make-curry args `(begin ,@body)))))

(define-macro (my-call-with-progress . body)
  `(begin
     (display "starting...") (newline)
     (let ((result (begin ,@body)))
       (display "done") (newline)
       result)))

(define-macro (my-repeat-times n . body)
  `(do ((i 0 (+ i 1))) ((>= i ,n)) ,@body))

(define-macro (my-define-counter name)
  (let ((counter (gensym)))
    `(begin
       (define ,counter 0)
       (define (,name)
         (let ((current ,counter))
           (set! ,counter (+ ,counter 1))
           current)))))

(define-macro (my-thunk . body)
  `(lambda () ,@body))

(my-call-with-progress (display "working") (newline))
(my-repeat-times 3 (display "hi ") (newline))

(my-define-counter next-id)
(display (next-id)) (display (next-id)) (display (next-id)) (newline)

(define my-add2 (my-thunk (+ 1 2)))
(display (my-add2)) (newline)


(display "\n=== m10-comprehensive.scm ===\n")
;; 10-comprehensive.scm — 综合示例

(define-macro (my-define-logged name args . body)
  `(define (,name ,@args)
     (display "calling ") (display (quote ,name)) (display ": ") (newline)
     (let ((result (begin ,@body)))
       (display "result: ") (display result) (newline)
       result)))

(define-macro (my-define-cached name args . body)
  `(begin
     (define ,name (let ((cache (make-hash-table)))
                     (lambda ,args
                       (let ((key (list ,@args)))
                         (or (hash-table-ref/default cache key #f)
                             (let ((val (begin ,@body)))
                               (hash-table-set! cache key val)
                               val))))))))

(my-define-logged greet (name)
  (string-append "hello " name))
(display (greet "world")) (newline)

(my-define-cached fib (n)
  (if (< n 2) n (+ (fib (- n 1)) (fib (- n 2)))))
(display (fib 10)) (newline)



(display "\n=== scheme-macros.scm ===\n")
(define-syntax nth
  (syntax-rules ()
    ((_ n x ...)
     (list-ref (list x ...) n))))

(define-syntax if-not
  (syntax-rules ()
    ((_ cond then else)
     (if cond else then))))

(define-syntax stream-cons
  (syntax-rules ()
    ((_ head tail)
     (cons head (delay tail)))))

(define-syntax fluid-let
  (syntax-rules ()
    ((_ () body ...)
     (begin body ...))
    ((_ ((var val) . rest) body ...)
     (let ((saved var))
       (set! var val)
       (let ((result (fluid-let rest body ...)))
         (set! var saved)
         result)))))

(define-syntax receive
  (syntax-rules ()
    ((_ formals expression body ...)
     (call-with-values
       (lambda () expression)
       (lambda formals body ...)))))

(define-syntax with-values
  (syntax-rules ()
    ((_ producer consumer)
     (call-with-values (lambda () producer) consumer))))

(define-syntax assume
  (syntax-rules ()
    ((_ expr)
     (or expr (error "assume failed:" 'expr)))))

(define-syntax and-let*
  (syntax-rules ()
    ((_) #t)
    ((_ () body ...)
     (begin body ...))
    ((_ ((test) . rest) body ...)
     (if test (and-let* rest body ...) #f))
    ((_ ((var val) . rest) body ...)
     (let ((var val))
       (if var (and-let* rest body ...) #f)))
    ((_ (var . rest) body ...)
     (let ((var var))
       (if var (and-let* rest body ...) #f)))))

(define-syntax rec
  (syntax-rules ()
    ((_ (name . args) body ...)
     (letrec ((name (lambda args body ...))) name))))


(define-syntax do-ec
  (syntax-rules (if for)
    ((_ expr (if test) rest ...)
     (if test (do-ec expr rest ...)))
    ((_ expr (for var lst) rest ...)
     (for-each (lambda (var) (do-ec expr rest ...)) lst))
    ((_ expr (for var lst))
     (for-each (lambda (var) expr) lst))
    ((_ expr)
     expr)))

(define-syntax list-ec
  (syntax-rules (for if)
    ((_ expr)
     (list expr))
    ((_ expr (for var lst))
     (map (lambda (var) expr) lst))
    ((_ expr (if test))
     (if test (list expr) '()))
    ((_ expr (for var lst) (if test) more ...)
     (apply append
       (map (lambda (var)
              (if test
                  (list-ec expr more ...)
                  '()))
            lst)))
    ((_ expr (for var lst) more ...)
     (apply append
       (map (lambda (var)
              (list-ec expr more ...))
            lst)))))

(define-syntax sum-ec
  (syntax-rules (for if)
    ((_ expr (if test) more ...)
     (if test (sum-ec expr more ...) 0))
    ((_ expr (for var lst) more ...)
     (apply + 0 (list-ec expr (for var lst) more ...)))
    ((_ expr)
     expr)))

(define-syntax any?-ec
  (syntax-rules (for if)
    ((_ expr (for var lst) more ...)
     (any (lambda (var) (any?-ec expr more ...)) lst))
    ((_ expr (if test) more ...)
     (if test (any?-ec expr more ...) #f))
    ((_ expr)
     expr)))


(define-syntax every?-ec
  (syntax-rules (for if)
    ((_ expr (for var lst) more ...)
     (every (lambda (var) (every?-ec expr more ...)) lst))
    ((_ expr (if test) more ...)
     (if test (every?-ec expr more ...) #f))
    ((_ expr)
     expr)))


(define-syntax check
  (syntax-rules ()
    ((_ expr expected)
     (let ((actual expr) (exp expected))
       (if (equal? actual exp)
           (begin (display "  [CHECK PASS] ") (display 'expr) (newline))
           (begin (display "  [CHECK FAIL] ") (display 'expr) (newline)
                  (display "    expected: ") (write exp) (newline)
                  (display "    actual:   ") (write actual) (newline)))))))


(define-syntax check-ec
  (syntax-rules (for if)
    ((_ expected (for var lst) expr)
     (every?-ec (equal? expr expected) (for var lst)))
    ((_ expected (for var lst) (if test) expr)
     (every?-ec (equal? expr expected) (for var lst) (if test)))))

(define-syntax aif
  (syntax-rules ()
    ((_ test then else)
     (let ((it test))
       (if it then else)))))


(define-syntax aand
  (syntax-rules ()
    ((_) #t)
    ((_ expr) expr)
    ((_ expr . rest)
     (let ((it expr))
       (if it (aand . rest) it)))))


(define-syntax alet
  (syntax-rules ()
    ((_ ((var val) ...) body ...)
     (let ((var val) ...) body ...))))

(define-syntax test-assert
  (syntax-rules ()
    ((_ name expr)
     (let ((result expr))
       (if result
           (begin (display (string-append "[PASS] " name)) (newline))
           (begin (display (string-append "[FAIL] " name)) (newline)))
       result))))


(define-syntax test-equal
  (syntax-rules ()
    ((_ name expected actual)
     (let ((e expected) (a actual))
       (if (equal? a e)
           (begin (display (string-append "[PASS] " name)) (newline))
           (begin (display (string-append "[FAIL] " name)) (newline)
                  (display (string-append "  expected: " (with-output-to-string (lambda () (write e))))) (newline)
                  (display (string-append "  actual:   " (with-output-to-string (lambda () (write a))))) (newline)))))))


(define-syntax test-approximate
  (syntax-rules ()
    ((_ name expected actual epsilon)
     (let ((e expected) (a actual))
       (if (< (abs (- a e)) epsilon)
           (begin (display (string-append "[PASS] " name)) (newline))
           (begin (display (string-append "[FAIL] " name)) (newline)
                  (display "  expected: ") (display e) (display " ± ") (display epsilon) (newline)
                  (display "  actual:   ") (display a) (newline)))))))

(define-syntax define-immutable
  (syntax-rules ()
    ((_ (name . args) body ...)
     (define name (lambda args body ...)))))


(define-syntax dbind
  (syntax-rules ()
    ((_ () expr body ...)
     (begin body ...))
    ((_ (a) expr body ...)
     (let ((a expr)) body ...))
    ((_ (a b) expr body ...)
     (let ((tmp expr))
       (let ((a (car tmp)) (b (cadr tmp))) body ...)))
    ((_ (a b c) expr body ...)
     (let ((tmp expr))
       (let ((a (car tmp)) (b (cadr tmp)) (c (caddr tmp))) body ...)))
    ((_ (a . b) expr body ...)
     (let ((tmp expr))
       (let ((a (car tmp)) (b (cdr tmp))) body ...)))))
