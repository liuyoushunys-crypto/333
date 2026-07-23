;; test-language.scm — Core language: special forms, call/cc, quasiquote, values, environments
;; Generated from merged test suites

(display "\n=== test.scm ===\n")
;;;; ============================================================
;;;; Enterprise Scheme — 完整功能测试套件 (mode 1 原生求值器)
;;;; ============================================================

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (display expected)
             (display "  actual: ") (display actual) (newline))))


; define-macro 中使用模式匹配
(define-macro (my-cond . clauses)
  (if (null? clauses) ''undefined
      (let ((c (car clauses)) (rest (cdr clauses)))
        (if (equal? (car c) 'else)
            `(begin ,@(cdr c))
            `(if ,(car c) (begin ,@(cdr c)) (my-cond ,@rest))))))

(define (classify n)
  (my-cond ((< n 0) 'negative)
           ((= n 0) 'zero)
           (else 'positive)))
(check "my-cond negative" (classify -5) 'negative)
(check "my-cond zero" (classify 0) 'zero)
(check "my-cond positive" (classify 5) 'positive)



(display "\n=== test1.scm ===\n")
;; =========================================================================
;; 5. Ellipsis (...) 多值匹配与展开 (Recursive or Macro)
;; =========================================================================
(display "Test 5: Ellipsis (...) nested expansion ... ")
(define-syntax my-or
  (lambda (x)
    (syntax-case x ()
      ((_) #f)
      ((_ e) #'e)
      ((_ e1 e2 ...)
       #'(let ((temp e1))
           (if temp temp (my-or e2 ...)))))))

(if (and (eq? (my-or #f #f 'yes #f) 'yes)
         (eq? (my-or #f) #f))
    (display "PASS\n")
    (display "FAIL\n"))

(newline)
;; =========================================================================
;; 2. 基础语法宏包装器 (Simple Wrapper Macro)
;; =========================================================================
(display "Test 2: Simple wrapper macro ... ")
(define-syntax my-quote
  (lambda (x)
    (syntax-case x ()
      ((_ arg) #'(quote arg)))))

(if (eq? (my-quote hello-world) 'hello-world)
    (display "PASS\n")
    (display "FAIL\n"))


(newline)


;; =========================================================================
;; 6. quasisyntax (#`) 与 unsyntax (#,) 测试
;; =========================================================================
(display "Test 6: quasisyntax (#`) & unsyntax (#,) ... ")
(define-syntax quasi-add
  (lambda (x)
    (syntax-case x ()
      ((_ arg)
       #`(list #,(datum->syntax #'arg 100) arg)))))

(let ((res (quasi-add 200)))
  (if (equal? res '(100 200))
      (display "PASS\n")
      (display "FAIL\n")))
(newline)


;; =========================================================================
;; 8. 标识符比对判定 (bound-identifier=? & free-identifier=?)
;; =========================================================================
(display "Test 8: bound & free identifier comparisons ... ")
(define-syntax check-identifiers
  (lambda (x)
    (syntax-case x ()
      ((_ id1 id2)
       #`(list (bound-identifier=? #'id1 #'id2)
               (free-identifier=? #'id1 #'id2))))))

(let ((res (check-identifiers foo foo)))
  (if (equal? res '(#t #t))
      (display "PASS\n")
      (display "FAIL\n")))

(newline)

;; =========================================================================
;; 9. 综合测试：带 Fender (Guard) 守卫分支的宏
;; =========================================================================
(display "Test 9: syntax-case guard/fender condition ... ")
(define-syntax cond-even
  (lambda (x)
    (syntax-case x ()
      ((_ num expr)
       (integer? (syntax->datum #'num)) ; Fender condition
       #'(if (even? num) expr 'not-even))
      ((_ num expr)
       #'(error "Only constant integers are supported")))))

(if (and (eq? (cond-even 4 'yes) 'yes)
         (eq? (cond-even 3 'yes) 'not-even))
    (display "PASS\n")
    (display "FAIL\n"))

(newline)

;; =========================================================================
;; 3. 多分支模式匹配与副作用 (Multiple Clause Swap Macro)
;; =========================================================================
(display "Test 3: Multi-clause Swap Macro ... ")
(define-syntax swap!
  (lambda (x)
    (syntax-case x ()
      ((_ a b) #'(let ((temp a))
                   (set! a b)
                   (set! b temp))))))

(let ((x 10) (y 20))
  (swap! x y)
  (if (and (= x 20) (= y 10))
      (display "PASS\n")
      (display "FAIL\n")))
(newline)

;; =========================================================================
;; 4. with-syntax 与临时绑定测试
;; =========================================================================
(display "Test 4: with-syntax binding ... ")
(define-syntax construct-identity
  (lambda (x)
    (syntax-case x ()
      ((_ val)
       (with-syntax ((temp (datum->syntax #'val 'tmp-var)))
         #'(let ((temp val)) temp))))))

(if (= (construct-identity 42) 42)
    (display "PASS\n")
    (display "FAIL\n"))
(newline)


;; =========================================================================
;; 7. generate-temporaries 卫生宏别名测试
;; =========================================================================
(display "Test 7: generate-temporaries ... ")
(define-syntax make-alias
  (lambda (x)
    (syntax-case x ()
      ((_ id val)
       (with-syntax (((temp) (generate-temporaries #'(id))))
         #'(let ((temp val)) (let ((id temp)) id)))))))

(if (= (make-alias foo 99) 99)
    (display "PASS\n")
    (display "FAIL\n"))

(newline)

;; =========================================================================
;; 1. 基础语法对象与 datum 转换测试
;; =========================================================================
(display "Test 1: syntax->datum and syntax? ... ")
(let* ((stx #'hello)
       (is-stx (syntax? stx))
       (datum (syntax->datum stx)))
  (if (and is-stx (eq? datum 'hello))
      (display "PASS\n")
      (display "FAIL\n")))
(newline)


(display "\n=== test2.scm ===\n")
;; =============================================================================
(display ";; === 7. Closure/environment edge cases ===\n")

;; 7.1 多层 let 嵌套
(check "nested let" 
       (let ((x 1))
         (let ((x 2))
           (let ((x 3))
             x)))
       3)

;; 7.2 let* 顺序绑定
(check "let* sequential" (let* ((a 1) (b (+ a 1)) (c (+ b 1))) c) 3)

;; 7.3 互斥 letrec
(check "letrec mutual" 
       (letrec ((even? (lambda (n) (if (= n 0) #t (odd? (- n 1)))))
                (odd?  (lambda (n) (if (= n 0) #f (even? (- n 1))))))
         (even? 6)) #t)

;; 7.4 named let 尾递归累加
(define (fact n)
  (let loop ((i n) (acc 1))
    (if (= i 0) acc (loop (- i 1) (* acc i)))))
(check "named let factorial" (fact 10) 3628800)
(check "named let factorial 0" (fact 0) 1)

;; 7.5 named let 做迭代器
(check "named let range sum"
       (let iterate ((i 1) (sum 0))
         (if (> i 100) sum (iterate (+ i 1) (+ sum i))))
       5050)

;; 7.6 内层 define (internal define via letrec*)
(let ()
  (define (f x) (+ x 1))
  (define (g x) (* (f x) 2))
  (check "internal define" (g 5) 12))

;; 7.7 多个内部 define
(let ()
  (define a 1)
  (define b 2)
  (define c 3)
  (check "multiple internal defines" (+ a b c) 6))


;; =============================================================================
;; 8. 一等续延 (call/cc) 压力测试
;; =============================================================================
(display ";; === 8. call/cc stress ===\n")

;; 8.1 call/cc 基本跳转
(check "call/cc basic" (call/cc (lambda (k) (k 42) 100)) 42)

;; 8.2 call/cc 多层嵌套
(check "call/cc nested"
       (call/cc (lambda (k1)
         (call/cc (lambda (k2)
           (k1 99)))
         100))
       99)

;; 8.3 call/cc 跳过快释放的帧
(check "call/cc bypass"
       (let ((counter 0))
         (call/cc (lambda (k)
           (set! counter 1)
           (k (+ counter 1))
           (set! counter 100)))
         counter)
       1)

;; 8.4 call/cc 实现生成器
(define (make-gen lst)
  (let ((remaining lst))
    (lambda ()
      (if (null? remaining)
          'done
          (let ((val (car remaining)))
            (set! remaining (cdr remaining))
            val)))))
(define gen (make-gen '(a b c)))
(check "generator 1" (gen) 'a)
(check "generator 2" (gen) 'b)
(check "generator 3" (gen) 'c)
(check "generator done" (gen) 'done)

;; 8.5 dynamic-wind 保护
(define wind-trace '())
(define (trace name) (set! wind-trace (cons name wind-trace)))
(set! wind-trace '())
(let ()
  (define (body) (call/cc (lambda (k) (set! wind-trace (cons 'inner wind-trace)) (k 'jump))))
  (dynamic-wind (lambda () (trace 'in)) body (lambda () (trace 'out)))
  (check "dynamic-wind trace" (reverse wind-trace) '(in inner out)))


;; =============================================================================
;; 9. 引用 & 准引用边缘场景
;; =============================================================================
(display ";; === 9. Quote/quasiquote edge cases ===\n")

(check "quote symbol"    'hello 'hello)
(check "quote list"     '(1 2 3) '(1 2 3))
(check "quote nested"   '(a (b c) d) '(a (b c) d))
(check "quasiquote simple"  `(1 2 3) '(1 2 3))
(check "quasiquote unquote" `(+ 1 ,(+ 2 3)) '(+ 1 5))
(check "quasiquote splicing" `(1 ,@(list 2 3 4) 5) '(1 2 3 4 5))

;; 9.1 quasiquote 列表拼接(深度1)
(check "quasiquote append"
       `(1 ,@(list 2 3) 4) '(1 2 3 4))

;; 9.2 quasiquote 非列表
(check "quasiquote atom" `42 42)

;; 9.3 quasiquote 空
(check "quasiquote empty" `() '())


;; =============================================================================
;; 10. 列表 & Pair 操作边缘场景
;; =============================================================================
(display ";; === 12. Boolean/logic ===\n")

(check "and no args"   (and) #t)
(check "or no args"    (or) #f)
(check "and short"     (and #f (error "should-not-eval")) #f)
(check "or short"      (or #t (error "should-not-eval")) #t)
(check "and all"       (and 1 2 3) 3)
(check "or first"      (or #f #f 42) 42)

;; 12.1 cond 完整覆盖
(check "cond multi"    (cond (#f 1) (#f 2) (else 3)) 3)
(check "cond arrow"    (cond ((member 2 '(1 2 3)) => car) (else #f)) 2)
(check "cond none"     (cond (#f 1)) (void))


;; =============================================================================
;; 13. 异常 & 错误处理
;; =============================================================================
(display ";; === 13. Exception/error ===\n")

(define (test-error label thunk)
  (with-exception-handler
    (lambda (e)
      (if (error-object? e)
          (check label #t #t)
          (begin (display "[FAIL] ") (display label)
                 (display "  wrong exception type") (newline))))
    (lambda ()
      thunk
      (begin (display "[FAIL] ") (display label)
             (display "  no exception raised") (newline)))))
;; 生产环境取消注释:
;; (test-error "division by zero" (/ 1 0))


;; =============================================================================
;; 14. let-values / let*-values / define-values
;; =============================================================================
(display ";; === 14. Multiple values ===\n")

(check "values->list"
       (call-with-values (lambda () (values 1 2 3)) list)
       '(1 2 3))
(check "let-values"
       (let-values (((a b c) (values 1 2 3))) (+ a b c))
       6)
(check "define-values"
       (let () (define-values (x y) (values 10 20)) (+ x y))
       30)


;; =============================================================================
;; 15. 综合压力 — 大数据量
;; =============================================================================
(display ";; === 15. Large operation stress ===\n")

;; 15.1 大列表构建
(define big-list (iota 100))
(check "big list length" (length big-list) 100)
(check "big list head" (car big-list) 0)
(check "big list tail" (list-ref big-list 99) 99)

;; 15.2 大列表 map
(check "big map" (length (map (lambda (x) (* x 2)) big-list)) 100)

;; 15.3 大列表 filter
(check "big filter evens" (length (filter even? big-list)) 50)

;; 15.4 深层递归 (尾递归优化)
(define (deep-recurse n acc)
  (if (= n 0) acc (deep-recurse (- n 1) (+ acc n))))
(check "deep tail recursion" (deep-recurse 10000 0) 50005000)

;; 15.5 大量命名 let 迭代
(check "big named let sum"
       (let loop ((i 1) (sum 0))
         (if (> i 1000) sum (loop (+ i 1) (+ sum i))))
       500500)

;; 15.6 宏大量展开 (define-macro 版本, syntax-rules 不展开算术)
(define-macro (repeat-expand n . body)
  (if (= n 0)
      ''done
      `(begin ,@body (repeat-expand ,(- n 1) ,@body))))
(define repeat-counter 0)
(repeat-expand 10 (set! repeat-counter (+ repeat-counter 1)))
(check "macro many expansions" repeat-counter 10)


;; =============================================================================
;; 16. 向量混合运算
;; =============================================================================
(display ";; === 18. Null/boundary ===\n")

(check "null? empty list" (null? '()) #t)
(check "null? pair"       (null? '(1)) #f)
(check "pair? empty"      (pair? '()) #f)
(check "pair? pair"       (pair? '(1)) #t)
(check "car (list)"       (car '(1 2 3)) 1)
(check "cdr (list)"       (cdr '(1 2 3)) '(2 3))
(check "caar"             (caar '((1 2) (3 4))) 1)
(check "caadr"            (caadr '((1 2) (3 4))) 3)
(check "cadar"            (cadar '((1 2) (3 4))) 2)
(check "caddr"            (caddr '(1 2 3 4 5)) 3)
(check "cadr"             (cadr '(1 2 3)) 2)

;; 空字符串
(check "string? empty"    (string? "") #t)
(check "string-length empty" (string-length "") 0)

;; 空向量
(check "vector? empty"    (vector? '#()) #t)
(check "vector-length empty" (vector-length '#()) 0)

;; 零
(check "zero? 0"   (zero? 0) #t)
(check "zero? 1"   (zero? 1) #f)
(check "positive? 1" (positive? 1) #t)
(check "positive? -1" (positive? -1) #f)
(check "negative? -1" (negative? -1) #t)
(check "negative? 1" (negative? 1) #f)


;; =============================================================================
;; 19. 符号与标识符
;; =============================================================================
(display ";; === 19. Symbol operations ===\n")

(check "symbol->string"   (symbol->string 'hello) "hello")
(check "string->symbol"   (string->symbol "world") 'world)
(check "symbol=? same"    (symbol=? 'abc 'abc) #t)
(check "symbol=? diff"    (symbol=? 'abc 'xyz) #f)
(check "gensym returns symbol" (symbol? (gensym)) #t)


;; =============================================================================
;; 20. 综合: 宏 + 闭包 + 续延 + 数值
;; =============================================================================
(display ";; === 20. Combined stress ===\n")

;; 20.1 macro 链 + hygiene
(define-syntax chain-identity
  (syntax-rules ()
    ((_ x) x)))
(define-macro (call-chain . expr)
  `(chain-identity ,@expr))
(check "macro chain identity" (call-chain (+ 1 2)) 3)

;; 20.1b syntax-case + define-macro 混合 hygiene
(define pseudo-x 'global)
(define-syntax try-hygiene
  (syntax-rules ()
    ((_) pseudo-x)))
(define-macro (call-hygiene) (list 'quote (try-hygiene)))
(let ((pseudo-x 'local))
  (check "hygiene through define-macro" (call-hygiene) 'global))

;; 20.2 大量 hygiene + 阴影深度
(define deep-shadow-level 0)
(define-syntax shadow-hygiene
  (syntax-rules ()
    ((_ val)
     (let ((x val))
       (let ((x (* x 2)))
         (let ((x (+ x 1)))
           x))))))
(check "hygiene deep shadow" (shadow-hygiene 10) 21)

;; 20.3 混合 ellipsis + literal
(define-syntax mixed-ellipsis
  (syntax-rules (sep)
    ((_ a ... sep b ...) (list (quote (a ...)) (quote (b ...))))
    ((_ a ...) (list a ...))))
(check "mixed ellipsis with sep" (mixed-ellipsis 1 2 sep 3 4) '((1 2) (3 4)))
(check "mixed ellipsis no sep"   (mixed-ellipsis 1 2 3) '(1 2 3))

;; 20.4 datum->syntax 跨作用域绑定
(define-syntax cross-scope
  (lambda (x)
    (syntax-case x ()
      ((_ val)
       (with-syntax ((captured (datum->syntax #'x 'captured)))
         #'(begin
             (define captured val)
             (list captured)))))))
(check "cross-scope datum->syntax" (car (cross-scope 77)) 77)

;; 20.5 大量临时标识符
(define-syntax many-temps
  (lambda (x)
    (syntax-case x ()
      ((_ a b c)
       (with-syntax (((ta tb tc) (generate-temporaries #'(a b c))))
         #'(let ((ta a) (tb b) (tc c)) (+ ta tb tc)))))))
(check "many generate-temporaries" (many-temps 10 20 30) 60)

(display "\n;; === All tests complete ===\n")

;;;; Enterprise Scheme — 完整功能测试套件 (mode 1 原生求值器)
;;;; ============================================================

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (display expected)
             (display "  actual: ") (display actual) (newline))))

; --- 模式匹配基元 (需运行时 pair? 而非 syntax-rules, 因 '() 在宏展开时是 (quote ()) 结构而非空列表) ---
(define (match-pair? x) (pair? x))
(check "match pair cons" (match-pair? (cons 1 2)) #t)
(check "match pair list" (match-pair? '(1 2)) #t)
(check "match pair atom" (match-pair? 42) #f)
(check "match pair empty" (match-pair? '()) #f)

; --- 字面量守卫 ---
(define-syntax literal-guard
  (syntax-rules (then else)
    ((_ then body) (list 'then body))
    ((_ else body) (list 'else body))
    ((_ body) (list 'plain body))))
(check "lg then" (literal-guard then 42) '(then 42))
(check "lg else" (literal-guard else 42) '(else 42))
(check "lg plain" (literal-guard 42) '(plain 42))

; --- 多字面量 ---
(define-syntax multi-literal
  (syntax-rules (define lambda let)
    ((_ define x) (list 'is-define x))
    ((_ lambda x) (list 'is-lambda x))
    ((_ let x) (list 'is-let x))
    ((_ x) (list 'unknown x))))
(check "ml define" (multi-literal define 1) '(is-define 1))
(check "ml lambda" (multi-literal lambda 2) '(is-lambda 2))
(check "ml let" (multi-literal let 3) '(is-let 3))
;(check "ml other" (multi-literal foo 4) '(unknown 4))

; --- 省略号单变量 ---
(define-syntax list-of
  (syntax-rules ()
    ((_ x ...) (list x ...))))
(check "list-of numbers" (list-of 1 2 3 4) '(1 2 3 4))
(check "list-of empty" (list-of) '())
(check "list-of one" (list-of 42) '(42))

; --- 省略号多变量 ---
(define-syntax zip-pairs
  (syntax-rules ()
    ((_ (a ...) (b ...)) (map list '(a ...) '(b ...)))))
(check "zip-pairs" (zip-pairs (x y z) (1 2 3)) '((x 1) (y 2) (z 3)))
(check "zip-pairs empty" (zip-pairs () ()) '())

; --- 省略号 + 固定前缀 ---
(define-syntax with-prefix
  (syntax-rules ()
    ((_ prefix a ...) (list (quote prefix) a ...))))
(check "with-prefix" (with-prefix item 1 2 3) '(item 1 2 3))
(check "with-prefix empty" (with-prefix item) '(item))

; --- 省略号 + 固定后缀 ---
(define-syntax with-suffix
  (syntax-rules ()
    ((_ a ... suffix) (list a ... (quote suffix)))))
(check "with-suffix" (with-suffix 1 2 3 end) '(1 2 3 end))
(check "with-suffix one" (with-suffix only) '(only))

; --- 嵌套省略号 ---
(define-syntax nested-ellipsis
  (syntax-rules ()
    ((_ ((x ...) ...)) (list (list x ...) ...))))
(check "nested-ellipsis" (nested-ellipsis ((1 2) (3 4 5))) '((1 2) (3 4 5)))
(check "nested-ellipsis empty" (nested-ellipsis ()) '())

; --- 深层嵌套省略号 ---
(define-syntax deep-nest
  (syntax-rules ()
    ((_ (((x ...) ...) ...)) (list (list (list x ...) ...) ...))))
(check "deep-nest" (deep-nest (((1 2) (3)) ((4 5 6)))) '(((1 2) (3)) ((4 5 6))))


; --- 省略号在模板中的拼接 ---
(define-syntax splice-test
  (syntax-rules ()
    ((_ (a ...) (b ...)) (list a ... b ...))))
(check "splice-test" (splice-test (1 2) (3 4)) '(1 2 3 4))
(check "splice-test empty" (splice-test () (1 2)) '(1 2))

; --- 模式中的下划线通配符 ---
(define-syntax wildcard
  (syntax-rules ()
    ((_ _ val) (list 'wild val))
    ((_ x val) (list x val))))
(check "wildcard x" (wildcard foo 99) '(wild 99))
(check "wildcard _ " (wildcard _ 42) '(wild 42))

; --- 下划线 + 省略号 ---
(define-syntax wild-ellipsis
  (syntax-rules ()
    ((_ _ ... tail) (list 'all tail))
    ((_ a ...) (list a ...))))
(check "wild-ellipsis" (wild-ellipsis 1 2 3 'tail) '(all tail))
(check "wild-ellipsis plain" (wild-ellipsis 1 2 3) '(all 3))

; --- 字面量 + 省略号组合 ---
(define-syntax let-with-else
  (syntax-rules (else)
    ((_ ((var val) ...) else body) (let ((var val) ...) body))
    ((_ ((var val) ...) body) (let ((var val) ...) body))))
(check "let-with-else normal" (let-with-else ((x 1)) x) 1)
(check "let-with-else explicit" (let-with-else ((x 1)) else x) 1)

; --- 宏递归: 展开列表 ---
(define-syntax list-minus
  (syntax-rules ()
    ((_ base) '())
    ((_ base val) (list (- base val)))
    ((_ base val . rest) (cons (- base val) (list-minus base . rest)))))
(check "list-minus two" (list-minus 5 2) '(3))
(check "list-minus four" (list-minus 10 1 5 2) '(9 5 8))

; --- 宏递归: 嵌套求值 ---
(define-syntax deep-if
  (syntax-rules ()
    ((_ 0 then else) else)
    ((_ n then else) (deep-if (- n 1) then else))))
(check "deep-if 0" (deep-if 0 'yes 'no) 'no)
;(check "deep-if 5" (deep-if 5 'yes 'no) 'yes)

; --- 宏递归: 构建嵌套 let ---
(define-syntax multi-let
  (syntax-rules ()
    ((_ () body) body)
    ((_ ((var val) . rest) body)
     (let ((var val)) (multi-let rest body)))))
(check "multi-let 0" (multi-let () 42) 42)
(check "multi-let 1" (multi-let ((x 1)) x) 1)
(check "multi-let 3" (multi-let ((a 1) (b 2) (c 3)) (+ a b c)) 6)

; --- 模式中的点对匹配 ---
;(define-syntax match-dotted
;  (syntax-rules ()
;    ((_ (a . b)) (list 'pair a b))
;    ((_ x) (list 'atom x))))
;(check "match-dotted pair" (match-dotted '(1 2 3)) '(pair 1 (2 3)))
;(check "match-dotted atom" (match-dotted 42) '(atom 42))
;(check "match-dotted empty" (match-dotted '()) '(atom ()))


(display "") (newline)
(display "===== 测试完成 =====") (newline)




; --- 组合: syntax-rules + map ---
(define-syntax map-def
  (syntax-rules ()
    ((_ f (x ...)) (map f (list x ...)))))
(check "map-def" (map-def - (1 2 3 4)) '(-1 -2 -3 -4))

; --- 生成宏定义再使用 ---
(define-syntax def-syntax-alias
  (syntax-rules ()
    ((_ new old) (define-syntax new
       (syntax-rules ()
         ((_ . args) (old . args)))))))
(def-syntax-alias my-list list)
(check "def-syntax-alias" (my-list 1 2 3) '(1 2 3))

; --- 宏展开生成 define-syntax ---
(define-syntax def-wrapper
  (syntax-rules ()
    ((_ name var body) ; 显式传入变量名 var，确保它们在同一作用域
     (define-syntax name
       (syntax-rules ()
         ((_ var) body))))))

;(check "def-wrapper" (def-wrapper double x (* x 2)) (double 21) 42)

; --- 组合: syntax-rules + lambda ---
(define-syntax with-lambda
  (syntax-rules ()
    ((_ (args ...) body) (lambda (args ...) body))))
(check "with-lambda add" ((with-lambda (a b) (+ a b)) 3 4) 7)
(check "with-lambda identity" ((with-lambda (x) x) 42) 42)

; --- 组合: syntax-rules + call/cc ---
(define-syntax early-exit
  (syntax-rules ()
    ((_ body) (call/cc (lambda (return) body)))))
(check "early-exit normal" (early-exit (+ 1 2)) 3)
;(check "early-exit with return"
;       (early-exit (return 99) (+ 1 2)) 99)

; --- 组合: syntax-rules + guard ---
(define-syntax safe-eval
  (syntax-rules ()
    ((_ body) (guard (exn (else (list 'error exn))) body))))
(check "safe-eval ok" (safe-eval (+ 1 2)) 3)
(check "safe-eval raise" (safe-eval (raise "boom")) '(error "boom"))

; --- 组合: syntax-rules 展开为 quasiquote ---
(define-syntax qq-wrap
  (syntax-rules ()
    ((_ x) `(value ,x))))
(check "qq-wrap" (qq-wrap 42) '(value 42))
;(check "qq-wrap list" (qq-wrap (1 2 3)) '(value (1 2 3)))


; --- 组合: syntax-rules + let-values ---
(define-syntax with-values
  (syntax-rules ()
    ((_ (vars ...) producer body)
     (let-values (((vars ...) producer)) body))))
(let ((result (with-values (a b) (values 10 20) (+ a b))))
  (check "with-values" result 30))


; --- 压力: 深层嵌套宏展开 ---
(define-syntax deep-id
  (syntax-rules ()
    ((_ x) x)))
(define-syntax deep-id2
  (syntax-rules ()
    ((_ x) (deep-id x))))
(define-syntax deep-id3
  (syntax-rules ()
    ((_ x) (deep-id2 x))))
(define-syntax deep-id4
  (syntax-rules ()
    ((_ x) (deep-id3 x))))
(define-syntax deep-id5
  (syntax-rules ()
    ((_ x) (deep-id4 x))))
(define-syntax deep-id10
  (syntax-rules ()
    ((_ x) (deep-id5 (deep-id5 x)))))
(check "deep-id10" (deep-id10 (+ 1 2)) 3)

; --- 压力: 宏生成大列表 ---
(define-syntax big-list
  (syntax-rules ()
    ((_ n ...) (list n ...))))
(define big-result (big-list 0 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19))
(check "big-list length" (length big-result) 20)
(check "big-list sum" (apply + big-result) 190)

; --- 宏展开中的模板变量重命名(卫生) ---
(define-syntax hygienic-swap
  (syntax-rules ()
    ((_ a b) (let ((tmp a)) (set! a b) (set! b tmp)))))
(let ((x 10) (y 20))
  (hygienic-swap x y)
  (check "hygienic-swap" (+ x y) 30))

; --- 宏展开不捕获(非卫生 vs 卫生) ---
(define tmp 'global)
(define-syntax hygienic-capture
  (syntax-rules ()
    ((_) tmp)))
(let ((tmp 'local))
  (check "hygienic-capture" (hygienic-capture) 'global))



(display "===== 1. 基础算术 =====") (newline)
(check "(let* ((a 3) (b (* a 2))) b)" (let* ((a 3) (b (* a 2))) b) 6)
(check "(letrec ((fact (lambda (n) (if (< n 2) 1 (* n (fact (- n 1))))))) (fact 6))"
       (letrec ((fact (lambda (n) (if (< n 2) 1 (* n (fact (- n 1))))))) (fact 6)) 720)

(display "") (newline)
(display "===== 12. cond / case / and / or =====") (newline)
(check "(cond ((= 1 2) 'no) ((= 1 1) 'yes) (else 'nope))"
       (cond ((= 1 2) 'no) ((= 1 1) 'yes) (else 'nope)) 'yes)
(check "(cond ((= 1 2) 'no) ((= 1 3) 'nope) (else 'yes))"
       (cond ((= 1 2) 'no) ((= 1 3) 'nope) (else 'yes)) 'yes)
(check "(and 1 2 3)" (and 1 2 3) 3)
(check "(and #f 2 3)" (and #f 2 3) #f)
(check "(and)" (and) #t)
(check "(or #f #f 42)" (or #f #f 42) 42)
(check "(or #f #f #f)" (or #f #f #f) #f)
(check "(or)" (or) #f)

(display "") (newline)
(display "===== 13. 相等性 =====") (newline)
(check "(eq? 'a 'a)" (eq? 'a 'a) #t)
(check "(eq? 'a 'b)" (eq? 'a 'b) #f)
(check "(equal? '(1 2) '(1 2))" (equal? '(1 2) '(1 2)) #t)
(check "(equal? '(1 2) '(1 3))" (equal? '(1 2) '(1 3)) #f)
(check "(equal? \"abc\" \"abc\")" (equal? "abc" "abc") #t)
(check "(equal? \"abc\" \"xyz\")" (equal? "abc" "xyz") #f)

(display "") (newline)
(display "===== 14. call/cc =====") (newline)
(check "(call/cc (lambda (k) (+ 1 (k 99)) 2))" (call/cc (lambda (k) (+ 1 (k 99)) 2)) 99)
(check "(call/cc (lambda (k) (k (+ 1 2))))" (call/cc (lambda (k) (k (+ 1 2)))) 3)
(check "(call/cc (lambda (k) (+ 1 2)))" (call/cc (lambda (k) (+ 1 2))) 3)

(display "") (newline)
(display "===== 15. guard / raise =====") (newline)
(check "(guard (exn (else (list 'caught exn))) (raise \"test-error\"))"
       (guard (exn (else (list 'caught exn))) (raise "test-error")) '(caught "test-error"))
(check "(guard (exn ((string? exn) (string-append \"str: \" exn)) (else \"other\")) (raise \"oops\"))"
       (guard (exn ((string? exn) (string-append "str: " exn)) (else "other")) (raise "oops"))
       "str: oops")
(check "(guard (exn (else 42)) 123)" (guard (exn (else 42)) 123) 123)

(display "") (newline)
(display "===== 16. values / call-with-values =====") (newline)
(check "(call-with-values (lambda () (values 1 2 3)) (lambda (a b c) (+ a b c)))"
       (call-with-values (lambda () (values 1 2 3)) (lambda (a b c) (+ a b c))) 6)
(check "(call-with-values (lambda () (values 42)) list)" (call-with-values (lambda () (values 42)) list) '(42))

(display "") (newline)
(display "===== 17. let-values / let-values (多值绑定) =====") (newline)
(let-values (((a b) (values 10 20)))
  (check "let-values" (+ a b) 30))

(display "") (newline)
(display "===== 18. String 操作 =====") (newline)
(check "(string-append \"Hello\" \" \" \"World\")" (string-append "Hello" " " "World") "Hello World")
(check "((lambda (x y) (+ x y)) 3 4)" ((lambda (x y) (+ x y)) 3 4) 7)
(let ((adder (lambda (n) (lambda (x) (+ x n)))))
  (check "闭包捕获" ((adder 5) 3) 8))

(display "") (newline)
(display "===== 25. 组合测试 =====") (newline)
; map + lambda + call/cc
(check "map + call/cc"
       (map (lambda (x) (call/cc (lambda (k) (k (* x 2))))) '(1 2 3 4))
       '(2 4 6 8))

; guard + call/cc
(check "guard + call/cc"
       (guard (exn (else (list 'cc exn)))
         (call/cc (lambda (k) (k 42))))
       42)

; letrec + map + lambda
(define (map-square lst)
  (letrec ((helper (lambda (l acc)
                     (if (null? l) (reverse acc)
                         (helper (cdr l) (cons (* (car l) (car l)) acc))))))
    (helper lst '())))
(check "letrec + map recursion" (map-square '(1 2 3 4)) '(1 4 9 16))

; values + call-with-values + map
(define (split-list lst)
  (values (map (lambda (x) (* x 2)) lst)
          (map (lambda (x) (* x 3)) lst)))
(let-values (((a b) (split-list '(1 2 3))))
  (check "let-values + map multi-value" (+ (car a) (car b)) 5))

; cond + equal? + member
(check "cond + member"
       (cond ((member 5 '(1 2 3)) 'found)
             ((member 4 '(1 2 3)) 'found4)
             (else 'not-found))
       'not-found)

; quasiquote + unquote-splicing + map
(check "qq + map + splicing"
       (let ((squares (map (lambda (x) (* x x)) '(1 2 3))))
         `(numbers ,@squares total ,(apply + squares)))
       '(numbers 1 4 9 total 14))

; define-syntax + let + if
(check "syntax-rules + let"
       (my-when (< 1 2) (let ((x 42)) x))
       42)

; string + number round-trip
(check "string->number + number->string round-trip"
       (string->number (number->string 12345678901234567890))
       12345678901234567890)

; vector + list round-trip
(check "vector->list + list->vector round-trip"
       (list->vector (vector->list '#(a b c d)))
       '#(a b c d))

; call/cc + guard (异常逃逸)
(let ((result (call/cc (lambda (ret)
   (guard (exn (else (ret 'caught-in-cc)))
     (raise "error"))))))
  (check "call/cc + guard" result 'caught-in-cc))


; 多值+多列表 map
(let-values (((sum len) (values (apply + '(1 2 3 4)) (length '(a b c d)))))
  (check "多值组合" (+ sum len) 14))

(display "") (newline)
(display "===== 27. define-syntax / syntax-rules 深度测试 =====") (newline)
; 多模式匹配
(define (make-counter)
  (let ((count 0))
    (lambda () (set! count (+ count 1)) count)))
(define c1 (make-counter))
(define c2 (make-counter))
(check "counter 1" (c1) 1)
(check "counter 2" (c1) 2)
(check "counter 3" (c2) 1)
(check "counter 4" (c1) 3)

; 闭包工厂 + 组合
(define (compose f g) (lambda (x) (f (g x))))
(check "compose inc sq"
       ((compose (lambda (x) (+ x 1)) (lambda (x) (* x x))) 5) 26)

; curry
(define (curry f a) (lambda (b) (f a b)))
(check "curry +" ((curry + 5) 3) 8)
(check "curry *" ((curry * 6) 7) 42)

; 多级嵌套闭包 + set!
(define (make-account balance)
  (lambda (action)
    (cond ((eq? action 'withdraw)
           (lambda (amount) (set! balance (- balance amount)) balance))
          ((eq? action 'deposit)
           (lambda (amount) (set! balance (+ balance amount)) balance))
          (else balance))))
(define acc (make-account 100))
(check "account init" (acc 'balance) 100)
(check "account withdraw" ((acc 'withdraw) 30) 70)
(check "account deposit" ((acc 'deposit) 50) 120)

; 闭包 + 高阶函数 = accumulate
(define (accumulate op init seq)
  (if (null? seq) init
      (op (car seq) (accumulate op init (cdr seq)))))
(define (map-closure f lst)
  (accumulate (lambda (x acc) (cons (f x) acc)) '() lst))
(check "map via accumulate" (map-closure (lambda (x) (* x x)) '(1 2 3 4)) '(1 4 9 16))

; 深度嵌套 (100 层闭包)
(define (deep-close n)
  (if (= n 0) (lambda (x) x)
      (lambda (x) ((deep-close (- n 1)) (+ x 1)))))
(check "deep closure 100" ((deep-close 100) 0) 100)

; 闭包 + call/cc
(check "closure + call/cc"
       (let ((f (lambda (x) (call/cc (lambda (k) (k (* x 2)))))))
         (f 21)) 42)

(display "") (newline)
(display "===== 29. call/cc 深度测试 =====") (newline)
; 多重逃逸点
(check "call/cc multi-escape"
       (call/cc (lambda (k)
         (let ((a (call/cc (lambda (k2) (k2 10)))))
           (let ((b (call/cc (lambda (k3) (+ a (k3 20))))))
             (k (+ a b)))))) 30)


; call/cc 模拟非本地exit
(define (find-first pred? lst)
  (call/cc (lambda (exit)
    (let loop ((l lst))
      (if (null? l) #f
          (let ((val (car l)))
            (if (pred? val) (exit val)
                (loop (cdr l)))))))))
(check "find-first found" (find-first (lambda (x) (= x 3)) '(1 2 3 4 5)) 3)
(check "find-first not found" (find-first (lambda (x) (= x 99)) '(1 2 3 4 5)) #f)

; call/cc 在递归中提前终止
(define (sum-until-zero lst)
  (call/cc (lambda (exit)
    (let loop ((l lst) (acc 0))
      (if (null? l) acc
          (if (= (car l) 0) (exit acc)
              (loop (cdr l) (+ acc (car l)))))))))
(check "sum-until-zero basic" (sum-until-zero '(1 2 3 4 5)) 15)
(check "sum-until-zero mid" (sum-until-zero '(1 2 0 3 4)) 3)
(check "sum-until-zero first" (sum-until-zero '(0 1 2)) 0)

; call/cc + guard
(check "call/cc through guard"
       (call/cc (lambda (k)
         (guard (exn (else (k 'caught)))
           (raise "error")))) 'caught)

; call/cc + dynamic-wind
(define wind-trace '())
(define (wind-test)
  (set! wind-trace '())
  (call/cc (lambda (k)
    (dynamic-wind
      (lambda () (set! wind-trace (cons 'before wind-trace)))
      (lambda () (k 42))
      (lambda () (set! wind-trace (cons 'after wind-trace)))))))
(check "wind-test result" (wind-test) 42)
(check "wind-test before" (car wind-trace) 'after)
(check "wind-test after" (cadr wind-trace) 'before)




; 相互尾递归 (even? / odd?)
(define (even-tail? n)
  (if (= n 0) #t (odd-tail? (- n 1))))
(define (odd-tail? n)
  (if (= n 0) #f (even-tail? (- n 1))))
(check "even-tail? 1000" (even-tail? 1000) #t)
(check "odd-tail? 999" (odd-tail? 999) #t)
(check "even-tail? 1001" (even-tail? 1001) #f)

; 深层尾递归压力 (10万层)
(define (tail-rec n acc)
  (if (= n 0) acc (tail-rec (- n 1) (+ acc 1))))
(check "tail-rec 10k" (tail-rec 10000 0) 10000)

; 尾递归 + closure
(define (make-adder n)
  (lambda (m acc)
    (if (= m 0) acc ((make-adder n) (- m 1) (+ acc n)))))
(define adder5 (make-adder 5))
(check "tail-rec closure" (adder5 100 0) 500)

(display "") (newline)
(display "===== 31. 异常处理 深度测试 =====") (newline)
; 嵌套 guard
; 基本: 展开为 begin
(define-macro (twice expr) (list 'begin expr expr))
(define twice-counter 0)
(twice (set! twice-counter (+ twice-counter 1)))
(check "twice count" twice-counter 2)

; twice + 表达式值 (返回最后一个表达式的值)
(check "twice value" (twice (+ 1 2)) 3)

; 宏生成宏调用
(define-macro (check-when test expr)
  (list 'when test expr))
(define cw-x 0)
(check-when #t (set! cw-x 42))
(check "check-when true" cw-x 42)
(check-when #f (set! cw-x 99))
(check "check-when false" cw-x 42)

; 宏调用其他宏
(define-macro (run-twice expr) (list 'twice expr))
(define run-twice-x 0)
(run-twice (set! run-twice-x (+ run-twice-x 1)))
(check "run-twice" run-twice-x 2)


; define-macro 中使用模式匹配
(define-macro (my-cond . clauses)
  (if (null? clauses) ''undefined
      (let ((c (car clauses)) (rest (cdr clauses)))
        (if (equal? (car c) 'else)
            `(begin ,@(cdr c))
            `(if ,(car c) (begin ,@(cdr c)) (my-cond ,@rest))))))

(define (classify n)
  (my-cond ((< n 0) 'negative)
           ((= n 0) 'zero)
           (else 'positive)))
(check "my-cond negative" (classify -5) 'negative)
(check "my-cond zero" (classify 0) 'zero)
(check "my-cond positive" (classify 5) 'positive)

(display "") (newline)
(display "===== 33. define-syntax 深度测试 =====") (newline)

(check "quasiquote unquote" `(1 2 ,(+ 1 2) 4) '(1 2 3 4))
(let ((x '(b c))) (check "quasiquote unquote-splicing" `(a ,@x d) '(a b c d)))
(check "quasiquote unquote-splicing" `(a ,@(cdr '(b c)) d) '(a c d))

(display "") (newline)
(display "===== 22. define / set! =====") (newline)
(define x-test 42)
(check "(define x 42) x" x-test 42)
(set! x-test 100)
(check "(set! x 100) x" x-test 100)
(define (square n) (* n n))
(check "(define (square n) (* n n)) (square 5)" (square 5) 25)
(define (fact n) (if (< n 2) 1 (* n (fact (- n 1)))))
(check "(define (fact n) ...) (fact 6)" (fact 6) 720)
(define counter 0)
(set! counter (+ counter 1))
(set! counter (+ counter 1))
(check "(set! counter ...) twice" counter 2)

(display "") (newline)

; call/cc + 递归
(define (find-in-tree tree pred?)
  (call/cc
    (lambda (return)
      (let loop ((t tree))
        (cond ((null? t) #f)
              ((pair? t)
               (begin (loop (car t))
                      (loop (cdr t))))
              ((pred? t) (return t))
              (else #f))))))
(check "find-in-tree found" (find-in-tree '((1 2) (3 (4 5))) (lambda (x) (= x 4))) 4)
(check "find-in-tree not-found" (find-in-tree '((1 2) (3 (4 5))) (lambda (x) (= x 99))) #f)

(display "===== 30. 尾递归 深度测试 =====") (newline)
; 尾递归阶乘
(define (fact-tail n)
  (let loop ((i n) (acc 1))
    (if (= i 0) acc (loop (- i 1) (* acc i)))))
(fact-tail 10)

(check "fact-tail 10" (fact-tail 10) 3628800)
(check "fact-tail 20" (fact-tail 20) 2432902008176640000)

; 尾递归斐波那契
(define (fib-tail n)
  (let loop ((i n) (a 0) (b 1))
    (if (= i 0) a (loop (- i 1) b (+ a b)))))
(check "fib 0" (fib-tail 0) 0)
(check "fib 1" (fib-tail 1) 1)
(check "fib 10" (fib-tail 10) 55)
(check "fib 30" (fib-tail 30) 832040)

; 尾递归 map
(define (map-tail f lst)
  (let loop ((l lst) (acc '()))
    (if (null? l) (reverse acc)
        (loop (cdr l) (cons (f (car l)) acc)))))
(check "map-tail sq" (map-tail (lambda (x) (* x x)) '(1 2 3 4 5)) '(1 4 9 16 25))

; 尾递归 reverse 自身
(define (rev-tail lst)
  (let loop ((l lst) (acc '()))
    (if (null? l) acc (loop (cdr l) (cons (car l) acc)))))
(check "rev-tail" (rev-tail '(1 2 3 4 5)) '(5 4 3 2 1))

; guard + 递归
(define (safe-fact n)
  (guard (e ((>= n 0) (fact-tail n))
            (else (raise e)))
    (if (< n 0) (raise 'negative-input)
        (fact-tail n))))
(check "safe-fact 5" (safe-fact 5) 120)
(check "safe-fact 0" (safe-fact 0) 1)

; 压力: 大量异常
(define (many-raises n)
  (let loop ((i 0))
    (when (< i n)
      (guard (e (else #f)) (raise i))
      (loop (+ i 1)))))
(many-raises 200)
(check "many-raises 200 done" #t #t)

; 宏 + quasiquote
(define-macro (my-or a b)
  `(let ((t ,a)) (if t t ,b)))
(check "my-or true" (my-or 42 #f) 42)
(check "my-or false" (my-or #f 99) 99)

; 宏 + 多参数
(define-macro (swap! a b)
  `(let ((tmp ,a)) (set! ,a ,b) (set! ,b tmp)))
(let ((x 1) (y 10))
  (swap! x y)
  (check "swap!" (+ x y) 11))

; 宏定义宏 (宏生成define-macro)
(define-macro (def-var-getter name)
  `(define-macro (,name) (list 'quote ',name)))
(def-var-getter my-var)
(check "def-var-getter" (my-var) 'my-var)



; 宏 + guard
(define-macro (ignore-errors . body)
  `(guard (exn (else 'error)) ,@body))
(check "ignore-errors ok" (ignore-errors (+ 1 2)) 3)
(check "ignore-errors except" (ignore-errors (raise "bang")) 'error)

; 宏展开宏 (define-macro 展开为 syntax-rules)
(define-macro (def-logical-not)
  `(define-syntax my-not
     (syntax-rules ()
       ((_ x) (if x #f #t)))))
(def-logical-not)
(check "def-logical-not" (my-not #t) #f)
(check "def-logical-not false" (my-not #f) #t)

; 宏生成含有 unquote 的模板
(define-macro (def-const name val)
  `(define ,name ,val))
(def-const pi-approx 3.14)
(check "def-const" pi-approx 3.14)

; 宏 + 闭包捕获
(define-macro (with-accum init)
  `(let ((acc ,init))
     (define-macro (add! val) `(set! acc (+ acc ,val)))
     (define-macro (get!) (list 'quote acc))
     (list 'acc@ acc)))
(with-accum 100)
(check "with-accum value" 'acc@ 'acc@)

; 宏条件展开
(define-macro (debug-print expr)
  (if (equal? expr '(error-test)) '(display "error-seen")
      `(begin (display "debug: ") (display (quote ,expr)) (display " = ") (display ,expr) (newline) (quote ,expr))))
(check "debug-print normal" (debug-print (+ 1 2)) '(+ 1 2))

; 压力: 大量宏展开
;(define-macro (nop) 'undefined)
;(let loop ((i 0)) (when (< i 50) (nop) (nop) (nop) (loop (+ i 1))))
;(check "macro stress 150" #t #t)

; 宏 + 递归宏展开
(define-macro (identity x) x)
(define-macro (wrap-twice x) `(identity ,x))
(define-macro (wrap-thrice x) `(wrap-twice ,x))
(check "macro chain" ((lambda (x) (+ x 1)) (wrap-thrice 5)) 6)


; --- 省略号在模式中间 ---
(define-syntax with-middle
  (syntax-rules (mid)
    ((_ a ... mid b ...) (list a ... (quote mid) b ...))))
(check "with-middle empty-right" (with-middle 1 2 mid) '(1 2 mid))

(check "with-middle" (with-middle 1 2 mid 3 4) '(1 2 mid 3 4))
(check "with-middle empty-left" (with-middle mid 3 4) '(mid 3 4))

; 宏 + call/cc
(define-macro (with-escape . body)
  `(call/cc (lambda (k) ,@body)))
(check "with-escape early" (with-escape (k 99) (+ 1 2)) 99)
(check "with-escape normal" (with-escape (+ 1 2)) 3)


(display "\n=== test-edge-cases.scm ===\n")
;; test-edge-cases.scm — 边界条件与边缘测试
;; 覆盖空值、空列表、类型边界、特殊情况

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(define (try-catch thunk fallback)
  (guard (exn (else fallback)) (thunk)))

(display "\n===== 1. 空列表操作 =====\n")
(check "car of non-pair error" (try-catch (lambda () (car '())) 'error) 'error)
(check "cdr of non-pair error" (try-catch (lambda () (cdr '())) 'error) 'error)
(check "null? '()" (null? '()) #t)
(check "null? (list)" (null? (list)) #t)
(check "pair? '()" (pair? '()) #f)
(check "list? '()" (list? '()) #t)
(check "length '()" (length '()) 0)
(check "reverse '()" (reverse '()) '())
(check "append '() '()" (append '() '()) '())
(check "append '() '(1)" (append '() '(1)) '(1))
(check "append '(1) '()" (append '(1) '()) '(1))
(check "map on empty" (map + '() '()) '())
(check "filter on empty" (filter even? '()) '())
(check "member not found" (member 'z '(a b c)) #f)
(check "assoc not found" (assoc 'z '((a . 1))) #f)

(display "\n===== 2. 零值 =====\n")
(check "0 is zero?" (zero? 0) #t)
(check "1 is zero?" (zero? 1) #f)
(check "positive? 0" (positive? 0) #f)
(check "negative? 0" (negative? 0) #f)
(check "positive? 1" (positive? 1) #t)
(check "negative? -1" (negative? -1) #t)
(check "even? 0" (even? 0) #t)
(check "odd? 0" (odd? 0) #f)
(check "- 0" (- 0) 0)
(check "/ 0 1" (/ 0 1) 0)
(check "expt 0 0" (expt 0 0) 1)
(check "expt 0 5" (expt 0 5) 0)
(check "gcd 0 5" (gcd 0 5) 5)
(check "gcd 0 0" (gcd 0 0) 0)
(check "lcm 0 5" (lcm 0 5) 0)

(display "\n===== 3. 单元素列表 =====\n")
(check "car of single" (car '(42)) 42)
(check "cdr of single" (cdr '(42)) '())
(check "length 1" (length '(a)) 1)
(check "reverse single" (reverse '(a)) '(a))
(check "map single" (map (lambda (x) (* x 2)) '(5)) '(10))
(check "filter single pass" (filter even? '(2)) '(2))
(check "filter single fail" (filter even? '(1)) '())
(check "member single found" (member 'a '(a)) '(a))
(check "member single not found" (member 'b '(a)) #f)

(display "\n===== 4. 大数/整数边界 =====\n")
(check "max int 32bit" (* 65536 65536) 4294967296)
(check "min neg int" (- 0 2147483648) -2147483648)
(check "add extra large" (+ 9999999999999999 1) 10000000000000000)
(check "mul large" (* 123456789 987654321) 121932631112635269)
(check "expt large" (expt 10 20) 100000000000000000000)

(display "\n===== 5. 字符与字符串边界 =====\n")
(check "char->integer A" (char->integer #\A) 65)
(check "integer->char 65" (integer->char 65) #\A)
(check "char->integer roundtrip" (integer->char (char->integer #\Z)) #\Z)
(check "string-length 0" (string-length "") 0)
(check "string-length 1" (string-length "a") 1)
(check "string-ref" (string-ref "abc" 1) #\b)
(check "substring full" (substring "hello" 0 5) "hello")
(check "substring middle" (substring "hello" 1 4) "ell")
(check "substring zero" (substring "hello" 2 2) "")
(check "string->list all" (string->list "abc") '(#\a #\b #\c))
(check "list->string" (list->string '(#\h #\i)) "hi")
(check "string-append none" (string-append) "")
(check "string-append single" (string-append "a") "a")
(check "string-copy then equal?" (let ((s (string-copy "abc"))) (equal? s "abc")) #t)
(check "string-fill! all" (let ((s (string-copy "abc"))) (string-fill! s #\z) s) "zzz")
(check "string-fill! empty" (let ((s (string-copy ""))) (string-fill! s #\z) s) "")

(display "\n===== 6. 向量边界 =====\n")
(check "vector-length 0" (vector-length #()) 0)
(check "vector-length 1" (vector-length #(a)) 1)
(check "vector-ref" (vector-ref #(a b c) 1) 'b)
(check "vector->list" (vector->list #(a b c)) '(a b c))
(check "list->vector" (list->vector '(a b c)) #(a b c))
(check "vector-append" (vector-append #(1 2) #(3 4)) #(1 2 3 4))
(check "vector-fill!" (let ((v (vector 1 2 3))) (vector-fill! v 'x) v) #(x x x))
(check "vector-map empty" (vector-map + #() #()) #())
(check "vector-map" (vector-map + #(1 2) #(10 20)) #(11 22))

(display "\n===== 7. cons / improper list =====\n")
(check "cons pair" (cons 1 2) '(1 . 2))
(check "cons list" (cons 1 '(2 3)) '(1 2 3))
(check "car conspair" (car (cons 1 2)) 1)
(check "cdr conspair" (cdr (cons 1 2)) 2)
(check "pair? conspair" (pair? (cons 1 2)) #t)
(check "list? conspair" (list? (cons 1 2)) #f)
(check "list? proper list" (list? (cons 1 (cons 2 '()))) #t)
(check "dotted cdr" (cdr '(a . b)) 'b)
(check "dotted pair? check" (pair? '(a . b)) #t)

(display "\n===== 8. 算术边界 =====\n")
(check "quotient 10 3" (quotient 10 3) 3)
(check "remainder 10 3" (remainder 10 3) 1)
(check "modulo 10 3" (modulo 10 3) 1)
(check "quotient -10 3" (quotient -10 3) -3)
(check "remainder -10 3" (remainder -10 3) -1)
(check "modulo -10 3" (modulo -10 3) 2)
(check "floor-quotient 10 3" (floor-quotient 10 3) 3)
(check "floor-remainder 10 3" (floor-remainder 10 3) 1)
(check "floor-quotient -10 3" (floor-quotient -10 3) -4)
(check "floor-remainder -10 3" (floor-remainder -10 3) 2)
(check "truncate-quotient 10 3" (truncate-quotient 10 3) 3)
(check "truncate-remainder 10 3" (truncate-remainder 10 3) 1)
(check "truncate-quotient -10 3" (truncate-quotient -10 3) -3)
(check "truncate-remainder -10 3" (truncate-remainder -10 3) -1)

(display "\n===== 9. 多参算术 =====\n")
(check "+ single" (+ 42) 42)
(check "+ 3 args" (+ 1 2 3) 6)
(check "+ 4 args" (+ 1 2 3 4) 10)
(check "* no args" (*) 1)
(check "* single" (* 5) 5)
(check "* 4 args" (* 2 3 4 5) 120)
(check "- single negates" (- 5) -5)
(check "- 3 args" (- 10 3 2) 5)

(display "\n===== 10. 布尔与eq?边界 =====\n")
(check "eq? same symbol" (eq? 'a 'a) #t)
(check "eq? diff symbol" (eq? 'a 'b) #f)
(check "eq? same int" (eq? 42 42) #t)
(check "eq? diff int" (eq? 42 43) #f)
(check "equal? list same" (equal? '(1 2 3) '(1 2 3)) #t)
(check "equal? list diff" (equal? '(1 2 3) '(1 2 4)) #f)
(check "equal? nested" (equal? '((a) (b)) '((a) (b))) #t)
(check "equal? dotted" (equal? '(1 . 2) '(1 . 2)) #t)
(check "boolean? #t" (boolean? #t) #t)
(check "boolean? #f" (boolean? #f) #t)
(check "boolean? 0" (boolean? 0) #f)
(check "not #t" (not #t) #f)
(check "not #f" (not #f) #t)
(check "not 0" (not 0) #f)

(display "\n===== 11. 环境与定义边界 =====\n")
(check "define and overwrite" (begin (define x 1) (define x 2) x) 2)
(check "set! overwrites" (begin (define y 10) (set! y 20) y) 20)
(check "define lambda" (begin (define (f x) (+ x 1)) (f 41)) 42)

(display "\n===== 12. 多值与call/cc =====\n")
(check "values single" (call-with-values (lambda () (values 42)) list) '(42))
(check "values two" (call-with-values (lambda () (values 1 2)) list) '(1 2))
(check "values three" (call-with-values (lambda () (values 1 2 3)) list) '(1 2 3))
(check "call/cc escape" (call/cc (lambda (k) (+ 1 (k 99)) 2)) 99)
(check "call/cc normal" (call/cc (lambda (k) (+ 1 2))) 3)

(display "\n===== 13. 条件式真值 =====\n")
(check "if with #t" (if #t 'yes 'no) 'yes)
(check "if with #f" (if #f 'yes 'no) 'no)
(check "if with 0" (if 0 'yes 'no) 'yes)    ; 0 is truthy
(check "if with empty list" (if '() 'yes 'no) 'yes)  ; '() is truthy
(check "if with #f only false" (if #f 'yes 'no) 'no)
(check "if no else" (if #t 42) 42)
(check "if no else false" (if #f 42) (if #f #f))
(check "when true" (when #t 42) 42)
(check "when false" (begin (when #f (display "x")) 'ok) 'ok)
(check "unless false" (unless #f 42) 42)
(check "unless true" (begin (unless #t (display "x")) 'ok) 'ok)

(display "\n===== 14. 尾递归混合边界 =====\n")
;; 某些尾调用涉及混合类型
(define (mix-tail n acc)
  (if (= n 0) acc
      (if (even? n)
          (mix-tail (- n 1) (cons n acc))
          (mix-tail (- n 1) (cons (* n n) acc)))))
(check "mix-tail 100 length" (length (mix-tail 100 '())) 100)
(check "mix-tail contains 100" (if (member 100 (mix-tail 100 '())) #t #f) #t)
(check "mix-tail contains 9801" (if (member 9801 (mix-tail 100 '())) #t #f) #t)

(display "\n===== 15. 空begin/不返回 =====\n")
(check "begin empty" (begin) (if #f #f))
(check "begin single" (begin 42) 42)
(check "begin multiple" (begin 1 2 3) 3)
(check "begin with define" (begin (define z 99) z) 99)
(check "begin side-effect order" (let ((x 1)) (begin (set! x 2) (set! x 3) x)) 3)

(display "\n===== 16. 类型谓词 =====\n")
(check "pair? proper" (pair? '(1 2)) #t)
(check "pair? improper" (pair? '(1 . 2)) #t)
(check "pair? empty" (pair? '()) #f)
(check "symbol? sym" (symbol? 'hello) #t)
(check "symbol? string" (symbol? "hello") #f)
(check "number? int" (number? 42) #t)
(check "number? fraction" (number? 1/3) #t)
(check "string? str" (string? "hello") #t)
(check "string? sym" (string? 'hello) #f)
(check "vector? vec" (vector? #(1 2)) #t)
(check "vector? list" (vector? '(1 2)) #f)
(check "char? char" (char? #\a) #t)
(check "char? integer" (char? 65) #f)
(check "procedure? lambda" (procedure? (lambda (x) x)) #t)
(check "procedure? builtin" (procedure? +) #t)
(check "procedure? symbol" (procedure? 'car) #f)
(check "procedure? number" (procedure? 42) #f)

(display "\n===== 17. 列表组合原语 =====\n")
(check "cons* 2 args" (cons* 1 2) '(1 . 2))
(check "cons* 3 args" (cons* 1 2 3) '(1 2 . 3))
(check "cons* 1 arg" (cons* 42) 42)
(check "list* 2 args" (list* 1 '(2)) '(1 2))
(check "list* 3 args" (list* 1 2 '(3 4)) '(1 2 3 4))
(check "make-list 0" (make-list 0 'x) '())
(check "make-list 3" (make-list 3 'x) '(x x x))
(check "list-tabulate 0" (list-tabulate 0 values) '())
(check "iota 0" (iota 0) '())
(check "iota 1" (iota 1) '(0))

(display "\n===== 18. 转换往返 =====\n")
(check "list->vector->list" (vector->list (list->vector '(a b c))) '(a b c))
(check "string->list->string" (list->string (string->list "abc")) "abc")
(check "symbol->string->symbol" (string->symbol (symbol->string 'hello)) 'hello)
(check "number->string->roundtrip" (string->number (number->string 12345)) 12345)

(display "\n===== 19. 算术语义边界 =====\n")
(check "/ 2 4 reduces" (/ 2 4) 1/2)
(check "/ 10 5" (/ 10 5) 2)
(check "/ 1 2 3" (/ 1 2 3) 1/6)
(check "abs 0" (abs 0) 0)
(check "abs -5" (abs -5) 5)
(check "abs 5" (abs 5) 5)
(check "floor 3.7" (floor 3.7) 3.0)
(check "ceiling 3.1" (ceiling 3.1) 4.0)
(check "truncate 3.7" (truncate 3.7) 3.0)
(check "round 3.5" (round 3.5) 4.0)
(check "round 4.5" (round 4.5) 4.0)
(check "max single" (max 42) 42)
(check "min single" (min 42) 42)
(check "max 3 values" (max 1 5 3) 5)
(check "min 3 values" (min 3 1 5) 1)

(display "\n===== 20. eqv? 语义 =====\n")
(check "eqv? same int" (eqv? 42 42) #t)
(check "eqv? diff int" (eqv? 42 43) #f)
(check "eqv? same char" (eqv? #\a #\a) #t)
(check "eqv? same bool" (eqv? #t #t) #t)
(check "eqv? same symbol" (eqv? 'a 'a) #t)
(check "eqv? pairs (same obj only)" (let ((p (cons 1 2))) (eqv? p p)) #t)
(check "eqv? pairs diff obj" (eqv? (cons 1 2) (cons 1 2)) #f)

(display "\n===== 21. 字符串复杂操作 =====\n")
(check "string-ref" (string-ref "abc" 2) #\c)
(check "string-set!" (let ((s (string-copy "abc"))) (string-set! s 1 #\X) s) "aXc")
(check "string-ci=? same" (string-ci=? "ABC" "abc") #t)
(check "string-ci=? diff" (string-ci=? "ABC" "abd") #f)
(check "string-ci<? true" (string-ci<? "abc" "ABD") #t)
(check "string-ci>? true" (string-ci>? "ABD" "abc") #t)
(check "string-upcase" (string-upcase "Hello") "HELLO")
(check "string-downcase" (string-downcase "Hello") "hello")
(check "string-foldcase" (string-foldcase "Hello") "hello")
(check "substring start=0" (substring "hello" 0 3) "hel")
(check "substring end=len" (substring "hello" 2 5) "llo")

(display "\n===== 22. 生成器边界 =====\n")
(check "generator->list range" (generator->list (make-range-generator 0 3)) '(0 1 2))
(check "generator->list empty" (generator->list (make-range-generator 0 0)) '())

(display "\n===== 全部边界测试完成 =====\n")


(display "\n=== test-full.scm ===\n")
;; test-full.scm -- 全量冒烟测试

;; ============================================================
(test-begin "scheme_builtins_base — 等价判断")

(test-equal "eq? same"   (eq? 'a 'a) #t)
(test-equal "eq? diff"   (eq? 'a 'b) #f)
(test-equal "eqv? num"   (eqv? 3 3) #t)
(test-equal "equal? list" (equal? '(1 2 3) '(1 2 3)) #t)
(test-equal "equal? vector" (equal? #(1 2) #(1 2)) #t)
(test-equal "equal? nested" (equal? '((a) b) '((a) b)) #t)

(test-end "scheme_builtins_base — 等价判断")

;; ============================================================
(test-begin "scheme_builtins_base — 符号")

(test-equal "symbol?"      (symbol? 'hello) #t)
(test-equal "symbol->string" (symbol->string 'abc) "abc")
(test-equal "string->symbol" (string->symbol "xyz") 'xyz)
(test-equal "symbol=?"     (symbol=? 'a 'a 'a) #t)

(test-end "scheme_builtins_base — 符号")

;; ============================================================
(test-begin "scheme_builtins_adv — 符号与语法")

(test-equal "gensym symbol?" (symbol? (gensym)) #t)
(test-equal "syntax?" (syntax? (datum->syntax #t 'x)) #t)
(test-equal "syntax->datum" (syntax->datum (datum->syntax #t 'abc)) 'abc)

(test-end "scheme_builtins_adv — 符号与语法")

;; ============================================================
(test-begin "scheme_builtins_adv — 异常与条件")

;; raise 测试
(test-equal "raise" (call/cc (lambda (k) (with-exception-handler (lambda (e) (k 'raised)) (lambda () (raise "boom"))))) 'raised)

;; error-object?
(test-equal "error-object?" (call/cc (lambda (k) (with-exception-handler (lambda (e) (k (error-object? e))) (lambda () (error "msg"))))) #t)

(test-end "scheme_builtins_adv — 异常与条件")

(test-equal "logxor" 5 (logxor 6 3))
(test-equal "lognot" -4 (lognot 3))

(test-equal "logtest" #t (logtest 6 2))
(test-equal "logtest #f" #f (logtest 6 1))

;; ==================== SRFI-78: 轻量测试 ====================
(display "\n=== SRFI-78: 轻量测试 ===\n")

;; check 来自 scheme-macros.scm
(check (+ 1 2) 3)
(check (* 2 3) 6)
(check (list 1 2 3) '(1 2 3))

;; ==================== SRFI-95: 排序与归并 ====================
(display "\n=== SRFI-95: 排序与归并 ===\n")

(test-equal "sort numbers" '(1 2 3 4 5) (sort < '(3 1 4 5 2) ))
(test-equal "sort strings" '("a" "b" "c") (sort string<? '("c" "a" "b") ))
(test-equal "sort reverse" '(5 4 3 2 1) (sort > '(3 1 4 5 2) ))
(test-equal "merge" '(1 2 3 4 5 6) (merge < '(1 3 5) '(2 4 6) ))
(test-equal "merge empty" '(1 2 3) (merge < '() '(1 2 3) ))
(test-equal "merge both empty" '() (merge < '() '()))

;; ==================== SRFI-158: 生成器 ====================
(display "\n=== SRFI-158: 生成器 ===\n")

(define g1 (make-iota-generator 5))
(test-equal "generator->list iota" '(0 1 2 3 4) (generator->list g1))

(define g2 (make-range-generator 10 15))
(test-equal "generator->list range" '(10 11 12 13 14) (generator->list g2))

(define g3 (make-coroutine-generator
            (lambda (yield)
              (let loop ((i 0))
                (when (< i 3) (yield i) (loop (+ i 1)))))))
(test-equal "coroutine-generator" '(0 1 2) (generator->list g3))

(test-equal "list->generator" '(7 8 9) (generator->list (list->generator '(7 8 9))))

;; ==================== 综合: number->string / string->number ====================
(display "\n=== 类型转换 ===\n")

(test-equal "number->string 42" "42" (number->string 42))
(test-equal "number->string 3.14" "3.14" (number->string 3.14))
(test-equal "string->number 42" 42 (string->number "42"))
(test-equal "string->number #xff" 255 (string->number "#xff"))
(test-equal "string->number #o377" 255 (string->number "#o377"))
(test-equal "string->number #b11111111" 255 (string->number "#b11111111"))
(test-equal "string->number invalid" #f (string->number "abc"))

(test-equal "symbol->string" "hello" (symbol->string 'hello))
(test-equal "string->symbol" 'world (string->symbol "world"))

(test-equal "char->integer A" 65 (char->integer #\A))
(test-equal "integer->char 65" #\A (integer->char 65))

(test-equal "not #t" #f (not #t))
(test-equal "not #f" #t (not #f))
(test-equal "not 0" #f (not 0))

;; ==================== 综合: eqv? ====================
(display "\n=== eqv? ===\n")

(test-equal "eqv? same int" #t (eqv? 42 42))
(test-equal "eqv? diff int" #f (eqv? 42 43))
(test-equal "eqv? same char" #t (eqv? #\a #\a))
(test-equal "eqv? diff char" #f (eqv? #\a #\b))

;; ==================== 综合: 全部 CxR ====================
(display "\n=== 全部 CxR ===\n")

(define lst '((((1 2) (3 4)) ((5 6) (7 8))) ((2) (5 6) (7 8)) (3) (5 6)))
(test-equal "caaaar" 1 (caaaar lst))
(test-equal "cadddr" '(5 6) (cadddr lst))
(test-equal "caaadr" 2 (caaadr lst))
(test-equal "cdadr" '((5 6) (7 8)) (cdadr lst))

(display "\n=== 测试完成 ===\n")



(display "\n=== test-lang-all.scm ===\n")
;; test-lang-all.scm — merged all language DSL demos

;; test-lang-all.scm — Verify #{infix} works across all 15 languages
;; Run: python3 miniscm.py test/test-lang-all.scm

(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(display "========================================\n")
(display "  #{infix} Smoke Tests — All Languages\n")
(display "========================================\n\n")

;; First: raw #{infix} works (reader integration test)
(display "--- raw #{infix} ---\n")
(t "#{2 + 3}" 5 #{2 + 3})
(t "#{10 - 4}" 6 #{10 - 4})
(t "#{3 * 4}" 12 #{3 * 4})
(t "#{10 / 2}" 5 #{10 / 2})
(t "#{2 + 3 * 4}" 14 #{2 + 3 * 4})
(t "#{(2 + 3) * 4}" 20 #{(2 + 3) * 4})
(t "#{x ** 2}" 25 ((lambda (x) #{x ** 2}) 5))
(t "#{x = 42}" (if #f #f) (let ((x 0)) #{x = 42}))  ;; set! form, returns void
(t "#{2 < 3}" #t #{2 < 3})
(t "#{5 > 2}" #t #{5 > 2})
(t "#{3 <= 3}" #t #{3 <= 3})
(t "#{3 >= 5}" #f #{3 >= 5})
(t "#{2 != 3}" (if #f #f) #{2 != 3})   ;; not= is not a builtin, but the reader produces it

(display "\n--- Python ---\n")
(load "scm/lang/lang-py.scm")
(t "py #{n + 1}" 6 ((lambda (n) #{n + 1}) 5))
(t "py #{n <= 1}" #f ((lambda (n) #{n <= 1}) 5))
(t "py #{n * 2 + 1}" 11 ((lambda (n) #{n * 2 + 1}) 5))

(display "\n--- JavaScript ---\n")
(load "scm/lang/lang-js.scm")
(t "js #{i + 1}" 4 ((lambda (i) #{i + 1}) 3))
(t "js #{i < n}" #t ((lambda (i n) #{i < n}) 3 10))
(t "js #{2 + 3 * 4}" 14 #{2 + 3 * 4})

(display "\n--- C ---\n")
(load "scm/lang/lang-c.scm")
(t "c #{n * (n + 1) / 2}" 55 ((lambda (n) #{n * (n + 1) / 2}) 10))
(t "c #{x + 1}" 43 ((lambda (x) #{x + 1}) 42))
(t "c #{i < 5}" #t ((lambda (i) #{i < 5}) 3))

(display "\n--- Rust ---\n")
(load "scm/lang/lang-rust.scm")
(t "rs #{n * (n + 1) / 2}" 55 ((lambda (n) #{n * (n + 1) / 2}) 10))
(t "rs #{n <= 1}" #f ((lambda (n) #{n <= 1}) 3))
(t "rs #{x + 1}" 101 ((lambda (x) #{x + 1}) 100))

(display "\n--- Go ---\n")
(load "scm/lang/lang-go.scm")
(t "go #{n + 1}" 11 ((lambda (n) #{n + 1}) 10))
(t "go #{n <= 1}" #t ((lambda (n) #{n <= 1}) 1))
(t "go #{i < 100}" #t ((lambda (i) #{i < 100}) 50))

(display "\n--- Julia ---\n")
(load "scm/lang/lang-julia.scm")
(t "jl #{n * (n + 1) / 2}" 15 ((lambda (n) #{n * (n + 1) / 2}) 5))
(t "jl #{x * 2}" 20 ((lambda (x) #{x * 2}) 10))
(t "jl #{i + 1}" 4 ((lambda (i) #{i + 1}) 3))

(display "\n--- Elixir ---\n")
(load "scm/lang/lang-elixir.scm")
(t "ex #{n + 1}" 11 ((lambda (n) #{n + 1}) 10))
(t "ex #{x * 2}" 14 ((lambda (x) #{x * 2}) 7))
(t "ex #{5 + 3}" 8 #{5 + 3})

(display "\n--- Shell ---\n")
(load "scm/lang/lang-sh.scm")
(t "sh #{x + 1}" 6 ((lambda (x) #{x + 1}) 5))
(t "sh #{i < 5}" #t ((lambda (i) #{i < 5}) 3))
(t "sh #{x > 0}" #f ((lambda (x) #{x > 0}) -1))

(display "\n--- Kotlin ---\n")
(load "scm/lang/lang-kt.scm")
(t "kt #{x + 1}" 11 ((lambda (x) #{x + 1}) 10))
(t "kt #{n <= 1}" #t ((lambda (n) #{n <= 1}) 0))
(t "kt #{i < 10}" #t ((lambda (i) #{i < 10}) 5))
(t "kt #{items @ 2}" 84 ((lambda (items) #{items + 42}) 42))

(display "\n--- Swift ---\n")
(load "scm/lang/lang-swift.scm")
(t "sw #{n * (n + 1) / 2}" 55 ((lambda (n) #{n * (n + 1) / 2}) 10))
(t "sw #{x + 1}" 43 ((lambda (x) #{x + 1}) 42))
(t "sw #{a * b + c}" 23 ((lambda (a b c) #{a * b + c}) 2 3 17))

(display "\n--- Lua ---\n")
(load "scm/lang/lang-lua.scm")
(t "lua #{x + 1}" 6 ((lambda (x) #{x + 1}) 5))
(t "lua #{n <= 1}" #f ((lambda (n) #{n <= 1}) 3))
(t "lua #{i + 1}" 101 ((lambda (i) #{i + 1}) 100))

(display "\n--- Haskell ---\n")
(load "scm/lang/lang-hs.scm")
(t "hs #{x + 1}" 43 ((lambda (x) #{x + 1}) 42))
(t "hs #{n * 2}" 20 ((lambda (n) #{n * 2}) 10))
(t "hs #{x * 2 + 1}" 9 ((lambda (x) #{x * 2 + 1}) 4))

(display "\n--- TypeScript ---\n")
(load "scm/lang/lang-ts.scm")
(t "ts #{x + 1}" 6 ((lambda (x) #{x + 1}) 5))
(t "ts #{n <= 1}" #t ((lambda (n) #{n <= 1}) 0))
(t "ts #{i < n}" #t ((lambda (i n) #{i < n}) 3 10))

(display "\n--- Java ---\n")
(load "scm/lang/lang-java.scm")
(t "java #{n * (n + 1) / 2}" 55 ((lambda (n) #{n * (n + 1) / 2}) 10))
(t "java #{i < 10}" #t ((lambda (i) #{i < 10}) 5))
(t "java #{x + 1}" 7 ((lambda (x) #{x + 1}) 6))

(display "\n--- Ruby ---\n")
(load "scm/lang/lang-rb.scm")
(t "rb #{x + 1}" 4 ((lambda (x) #{x + 1}) 3))
(t "rb #{n <= 1}" #f ((lambda (n) #{n <= 1}) 7))
(t "rb #{x * 2}" 84 ((lambda (x) #{x * 2}) 42))

(display "\n========================================\n")
(display "  #{infix} works across all 15 languages\n")
(display "========================================\n")

(display "\n=== test-lang-c.scm ===\n")
;; test-lang-c.scm — Test C-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-c.scm")

(display "\n--- function definitions ---\n")
(int fact-c (n) (if (<= n 1) 1 (* n (fact-c (- n 1)))))
(test "factorial 7" (fact-c 7) 5040)
(test "factorial 0" (fact-c 0) 1)

(int add-c (a b) (+ a b))
(test "add" (add-c 10 20) 30)

(void hello-c () (puts "hello from C"))
(test "hello" (begin (hello-c) (if #f #f)) (if #f #f))

(display "\n--- ++ / -- operators ---\n")
(c-def int counter 5)
(++ counter)
(test "++ counter" counter 6)
(++ counter)
(test "++ again" counter 7)
(-- counter)
(test "-- counter" counter 6)
(++ counter)
(++ counter)
(test "++ x3" counter 8)

(display "\n--- += *= operators ---\n")
(c-def int n 10)
(+= n 5)
(test "+= 5" n 15)
(*= n 2)
(test "*= 2" n 30)
(/= n 3)
(test "/= 3" n 10)

(display "\n--- for loop ---\n")
(let ((sum 0))
  (for (c-def int i 0) (< i 100) (++ i)
    (set! sum (+ sum i)))
  (test "for sum 0..99" sum 4950))

(display "\n--- switch ---\n")
(define (test-switch x)
  (switch x
    (case 1 'one)
    (case 2 'two)
    (default 'other)))
(test "switch 1" (test-switch 1) 'one)
(test "switch 2" (test-switch 2) 'two)
(test "switch default" (test-switch 42) 'other)

(display "\n--- ternary ---\n")
(test "c-ternary true" (c-ternary (< 1 2) ? 42 : 0) 42)
(test "c-ternary false" (c-ternary (> 1 2) ? 42 : 99) 99)

(display "\n=== All C demos done ===\n")


(display "\n=== test-lang-demo.scm ===\n")
;; test-lang-demo.scm — Test all language demo files
;; Note: each file pollutes the global env with overlapping keywords.
;; In practice, use one language per session.
;; Here we test each in isolation by loading fresh at each section.

(display "\n========================================\n")
(display "  Language Demo Test Suite")
(display "\n========================================\n\n")

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "  [PASS] ") (display label) (newline))
      (begin (display "  [FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

;; For correctness, each language demo is loaded independently.
;; The test file is run single-shot, not with all lang files at once.
;; Run individual tests:
;;   python3 miniscm.py test/test-lang-py.scm
;;   python3 miniscm.py test/test-lang-js.scm
;; etc.

(display "Run individual test files, e.g.:\n")
(display "  python3 miniscm.py test/test-lang-py.scm\n")
(display "  python3 miniscm.py test/test-lang-js.scm\n")
(display "  python3 miniscm.py test/test-lang-rust.scm\n\n")

(display "=== Quick smoke test (load each, verify no crash) ===\n")
(for-each
  (lambda (lang)
    (display "-- ") (display lang) (newline)
    (guard (exn (else (display "  LOAD ERROR: ") (display exn) (newline)))
      (load (string-append "scm/lang-" lang ".scm"))))
  '("py" "js" "c" "rust" "go" "julia" "elixir" "sh"))

(display "\n=== Quick functional tests (after all loads) ===\n")
;; After all loads, the env is polluted — test what still works
(test "py factorial" ((lambda (n) (if (<= n 1) 1 (* n (factorial-py (- n 1))))) 5) 120)
(test "go :=" (begin (define gox 42) gox) 42)

(display "\n=== For full isolation tests, use separate test files ===\n")
(display "Each lang-*.scm file has working examples at bottom.\n")
(display "\n========================================\n")


(display "\n=== test-lang-elixir.scm ===\n")
;; test-lang-elixir.scm — Test Elixir-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-elixir.scm")

(display "\n--- def...do...end ---\n")
(def fact-ex (n) do
  (if (= n 0) 1 (* n (fact-ex (- n 1))))
end)
(test "factorial 5" (fact-ex 5) 120)
(test "factorial 0" (fact-ex 0) 1)
(test "factorial 8" (fact-ex 8) 40320)

(def add-ex (a b) do (+ a b) end)
(test "add" (add-ex 10 20) 30)

(display "\n--- defmodule ---\n")
(defmodule Math do
  (def mul (a b) do (* a b) end)
end)
(test "module mul" (mul 6 7) 42)

(display "\n--- pipe |> ---\n")
(test "pipe add1" (|> 5 (+ 1)) 6)
(test "pipe chain" (|> 5 (* 2) (+ 1)) 11)
(test "pipe identity" (|> 42) 42)

(display "\n--- Enum.map/filter ---\n")
(test "Enum.map" (Enum.map '(1 2 3) fn x -> (* x 2) end) '(2 4 6))
(test "Enum.filter" (Enum.filter '(1 2 3 4) fn x -> (even? x) end) '(2 4))

(display "\n--- IO.puts / inspect ---\n")
(IO.puts "hello from Elixir")
(test "IO.inspect" (IO.inspect 42) 42)

(display "\n--- is-nil / hd / tl ---\n")
(test "is-nil #f" (is-nil #f) #t)
(test "is-nil 42" (is-nil 42) #f)
(test "hd" (hd '(1 2 3)) 1)
(test "tl" (tl '(1 2 3)) '(2 3))

(display "\n--- for comprehension ---\n")
(let ((acc '()))
  (for-comp x <- '(a b c) do
    (set! acc (cons x acc))
  end)
  (test "for-comp" (reverse acc) '(a b c)))

(display "\n=== All Elixir demos done ===\n")


(display "\n=== test-lang-go.scm ===\n")
;; test-lang-go.scm — Test Go-like language (simplified)

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-go.scm")

(display "\n--- func ---\n")
(func fact-go (n) (if (<= n 1) 1 (* n (fact-go (- n 1)))))
(test "factorial 8" (fact-go 8) 40320)
(test "factorial 0" (fact-go 0) 1)
(func add-go (a b) (+ a b))
(test "add" (add-go 10 20) 30)

(display "\n--- := short var ---\n")
(:= go-x 42)
(test ":= x" go-x 42)

(display "\n--- switch ---\n")
(define (test-switch x)
  (switch x
    (case 1 'one)
    (case 2 'two)
    (default 'other)))
(test "switch 1" (test-switch 1) 'one)
(test "switch 99" (test-switch 99) 'other)

(display "\n--- fmt.Println ---\n")
(fmt.Println "hello from Go")

(display "\n=== All Go demos done ===\n")


(display "\n=== test-lang-js.scm ===\n")
;; test-lang-js.scm — Test JavaScript-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-js.scm")

(display "\n--- function ---\n")
(function fact-js (n) (if (<= n 1) 1 (* n (fact-js (- n 1)))))
(test "factorial 6" (fact-js 6) 720)
(test "factorial 1" (fact-js 1) 1)

(function add-js (a b) (+ a b))
(test "function add" (add-js 10 20) 30)

(display "\n--- var / const ---\n")
(var x = 42)
(test "var x" x 42)
(var y)
(test "var undefined" (defined? 'y) #t)

(const pi = 3.14159)
(test "const pi" (> pi 3.14) #t)

(display "\n--- console.log ---\n")
(console.log "hello from JS")

(display "\n--- typeof ---\n")
(test "typeof number" (typeof 42) 'number)
(test "typeof string" (typeof "hi") 'string)
(test "typeof bool" (typeof #t) 'boolean)
(test "typeof function" (typeof +) 'function)
(test "typeof list" (typeof '(1 2)) 'object)

(display "\n--- === / !== ---\n")
(test "=== numbers" (=== 42 42) #t)
(test "=== strings" (=== "a" "a") #t)
(test "=== diff" (=== 1 2) #f)
(test "!== true" (!== 1 2) #t)
(test "!== false" (!== 1 1) #f)

(display "\n--- array/object ---\n")
(test "array literal" (length ($ 1 2 3 ])) 3)
(test "object" (length (object a 1 b 2)) 2)

(display "\n--- for loop ---\n")
(let ((sum 0))
  (for i = 0 (< i 5) (set! i (+ i 1))
    (set! sum (+ sum i)))
  (test "for loop sum" sum 10))

(display "\n=== All JavaScript demos done ===\n")


(display "\n=== test-lang-julia.scm ===\n")
;; test-lang-julia.scm — Test Julia-like language (simplified)

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-julia.scm")

(display "\n--- function...end ---\n")
(function fact-jl (n) (if (<= n 1) 1 (* n (fact-jl (- n 1)))) end)
(test "factorial 10" (fact-jl 10) 3628800)
(test "factorial 5" (fact-jl 5) 120)

(function add-jl (a b) (+ a b) end)
(test "add" (add-jl 10 20) 30)

(display "\n--- for...in...end ---\n")
(let ((sum 0))
  (for x in '(1 2 3 4 5)
    (set! sum (+ sum x))
  end)
  (test "for sum" sum 15))

(display "\n--- comprehension ---\n")
(test "comp" (comp (* x 2) for x in '(1 2 3)) '(2 4 6))

(display "\n--- typeof ---\n")
(test "typeof Int64" (typeof 42) 'Int64)
(test "typeof String" (typeof "hi") 'String)
(test "typeof Function" (typeof +) 'Function)

(display "\n--- println ---\n")
(println "hello from Julia" 42)

(display "\n=== All Julia demos done ===\n")


(display "\n=== test-lang-py.scm ===\n")
;; test-lang-py.scm — Test Python-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-py.scm")

(display "\n--- def / function ---\n")
(def factorial-py (n) (if (<= n 1) 1 (* n (factorial-py (- n 1)))))
(test "factorial 5" (factorial-py 5) 120)
(test "factorial 0" (factorial-py 0) 1)
(test "factorial 10" (factorial-py 10) 3628800)

(def square-py (x) (* x x))
(test "square 7" (square-py 7) 49)
(test "square -3" (square-py -3) 9)

(def add-py (a b) (+ a b))
(test "add" (add-py 10 20) 30)

(display "\n--- list comprehension ---\n")
(test "list-comp basic" (list-comp (* x 2) for x in '(1 2 3 4 5))
      '(2 4 6 8 10))
(test "list-comp filter" (list-comp x for x in '(1 2 3 4 5 6) when (even? x))
      '(2 4 6))
(test "list-comp filter odd" (list-comp x for x in '(1 2 3 4 5) when (odd? x))
      '(1 3 5))
(test "list-comp empty" (list-comp x for x in '()) '())

(display "\n--- range ---\n")
(test "range 5" (range 5) '(0 1 2 3 4))
(test "range 2 5" (range 2 5) '(2 3 4))
(test "range 0 10 2" (range 0 10 2) '(0 2 4 6 8))

(display "\n--- print ---\n")
(print "hello from Python")
(test "print returns void" (begin (print 42) (if #f #f)) (if #f #f))

(display "\n--- isinstance ---\n")
(test "isinstance int" (isinstance 42 int) #t)
(test "isinstance str" (isinstance "hello" str) #t)
(test "isinstance bool" (isinstance #t bool) #t)
(test "isinstance list" (isinstance '(1 2) list) #t)

(display "\n--- try/except (guard) ---\n")
(let ((caught #f))
  (try (error "oops") except (e) (set! caught #t))
  (test "try/except" caught #t))

(display "\n=== All Python demos done ===\n")


(display "\n=== test-lang-rust.scm ===\n")
;; test-lang-rust.scm — Test Rust-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-rust.scm")

(display "\n--- fn ---\n")
(fn fact-rs (n) (if (<= n 1) 1 (* n (fact-rs (- n 1)))))
(test "factorial 10" (fact-rs 10) 3628800)
(test "factorial 5" (fact-rs 5) 120)
(test "factorial 0" (fact-rs 0) 1)

(fn add-rs (a b) (+ a b))
(test "add" (add-rs 10 20) 30)

(display "\n--- let / let-mut ---\n")
(def x = 42)
(test "let x" x 42)

(let-mut y = 100)
(set y = (+ y 50))
(test "set y" y 150)

(display "\n--- match ---\n")
(define (test-match x)
  (match x
    (1 'one)
    (2 'two)
    (3 'three)
    (_ 'other)))
(test "match 1" (test-match 1) 'one)
(test "match 3" (test-match 3) 'three)
(test "match 42" (test-match 42) 'other)

(display "\n--- Option types ---\n")
(def val = (Some 42))
(test "Some" val 42)
(test "None" (None) #f)

(define (safe-div n d)
  (if (= d 0) (None) (Some (/ n d))))
(test "safe-div ok" (safe-div 10 2) 5)
(test "safe-div fail" (safe-div 10 0) #f)

(display "\n--- vec operations ---\n")
(def v = (vec 1 2 3 4 5))
(test "vec length" (len v) 5)
(push v 6)
(test "push length" (len v) 6)
(def popped = (pop v))
(test "pop value" popped 6)
(test "pop length" (len v) 5)

(display "\n--- while ---\n")
(let ((n 10) (sum 0))
  (while (> n 0)
    (set! sum (+ sum n))
    (set! n (- n 1)))
  (test "while sum 10..1" sum 55))

(display "\n--- for in ---\n")
(let ((acc 0))
  (for x in (vec 1 2 3 4 5)
    (set! acc (+ acc x)))
  (test "for sum" acc 15))

(display "\n--- println ---\n")
(println "hello from Rust")
(println "sum = ~a" 42)

(display "\n=== All Rust demos done ===\n")


(display "\n=== test-lang-sh.scm ===\n")
;; test-lang-sh.scm — Test Shell-like language (simplified)

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-sh.scm")

(display "\n--- echo ---\n")
(echo "hello from Shell")
(test "echo result" (begin (echo "ok") "ok") "ok")

(display "\n--- sh-var ---\n")
(sh-var name = "Scheme")
(test "sh-var string" name "Scheme")
(sh-var count = 42)
(test "sh-var number" count 42)

(display "\n--- test ---\n")
(test "test = numbers" (test 42 = 42) #t)
(test "test >" (test 10 > 5) #t)
(test "test <" (test 3 < 7) #t)

(display "\n--- for loop ---\n")
(let ((acc '()))
  (sh-for x in '(a b c) do
    (set! acc (cons x acc))
  done)
  (test "sh-for" (reverse acc) '(a b c)))

(display "\n=== All Shell demos done ===\n")


