;; =============================================================================
;; test2.scm — 综合压力测试 & 复杂边缘场景 (Enterprise Scheme)
;; =============================================================================
(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (display expected)
             (display "  actual: ") (display actual) (newline))))


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
(display ";; === 5. Numeric edge cases ===\n")

(check "zero-arg (-)" (-) 0)
(check "zero-arg (+)" (+) 0)
(check "zero-arg (*)" (*) 1)
(check "single-arg (-)" (- 5) -5)
(check "single-arg (+)" (+ 5) 5)
(check "multi-args (+)" (+ 1 2 3 4 5) 15)
(check "multi-args (-)" (- 10 1 2 3) 4)
(check "bignum" (* 123456789 987654321) 121932631112635269)
(check "fraction" (/ 1 3 2) 1/6)

;; 5.1 (-) 歧义
(check "(-) in list context" (list (-) 1 2) '(0 1 2))
(check "(- x) in list"       (list (- 10) 5) '(-10 5))

;; 5.2 变量名 i 解析
(define i 42)
(check "variable i after fix" i 42)
(let ((i 100)) (check "lexical i shadows" i 100))
(check "global i restored" i 42)

;; 5.3 纯复数
;(check "complex" (* 1+2i 3+4i) -5+10i)  ; 如编译器支持


;; =============================================================================
;; 6. 字符串 & 字符边缘场景
;; =============================================================================
(display ";; === 6. String/char edge cases ===\n")

(check "make-string no fill"    (string-length (make-string 5)) 5)
(check "make-string with fill"  (make-string 3 #\*) "***")
(check "make-string 0"          (make-string 0) "")
(check "make-string 1"          (string-length (make-string 1)) 1)
(check "string-ref"             (string-ref "hello" 1) #\e)
(check "string->list"           (string->list "abc") '(#\a #\b #\c))
(check "list->string"           (list->string '(#\x #\y #\z)) "xyz")
(check "string-append"          (string-append "a" "b" "c") "abc")
(check "string-length 0"        (string-length "") 0)
(check "substring"              (substring "hello" 1 4) "ell")

;; 6.1 string-set! 可变性
(let ((s (string-copy "abc")))
  (string-set! s 0 #\z)
  (check "string-set!" s "zbc"))


;; =============================================================================
;; 7. 闭包 & 环境作用域边缘场景
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
(display ";; === 10. List/pair edge cases ===\n")

(check "list head"     (list-head '(1 2 3 4) 2) '(1 2))
(check "list tail"     (list-tail '(1 2 3 4) 2) '(3 4))
(check "list-ref"      (list-ref '(a b c d) 2) 'c)
(check "member"        (member 'b '(a b c)) '(b c))
(check "assoc"         (assoc 'b '((a 1) (b 2) (c 3))) '(b 2))
(check "assq"          (assq 'b '((a 1) (b 2) (c 3))) '(b 2))
(check "append empty"  (append) '())
(check "append single" (append '(1 2)) '(1 2))
(check "append multi"  (append '(1) '(2) '(3)) '(1 2 3))
(check "map"           (map (lambda (x) (* x 2)) '(1 2 3)) '(2 4 6))
(check "filter"        (filter (lambda (x) (> x 2)) '(1 2 3 4)) '(3 4))
(check "fold"          (fold-left (lambda (acc x) (+ acc x)) 0 '(1 2 3)) 6)
(check "reverse"       (reverse '(1 2 3)) '(3 2 1))
(check "iota"          (iota 5) '(0 1 2 3 4))
(check "length 0"      (length '()) 0)
(check "list-copy"     (let ((l '(1 2 3))) (equal? (list-copy l) l)) #t)

;; 10.1 循环列表检测
(define circular (list 1 2 3))
(set-cdr! (cddr circular) circular)
(check "list? detects cycle" (list? circular) #f)

;; 10.2 set-car!/set-cdr!
(let ((p (cons 1 2)))
  (set-car! p 10)
  (set-cdr! p 20)
  (check "set-car!/set-cdr!" p '(10 . 20)))


;; =============================================================================
;; 11. 向量 & 字节向量
;; =============================================================================
(display ";; === 11. Vector/bytevector ===\n")

(check "vector"        (vector 1 2 3) '#(1 2 3))
(check "vector-ref"    (vector-ref '#(a b c) 1) 'b)
(check "vector-length" (vector-length '#(1 2 3 4)) 4)
(check "vector-set!"   (let ((v (vector 1 2 3))) (vector-set! v 1 99) v) '#(1 99 3))
(check "vector->list"  (vector->list '#(x y z)) '(x y z))
(check "list->vector"  (list->vector '(a b c)) '#(a b c))
(check "make-vector"   (vector-length (make-vector 5)) 5)
(check "make-vector fill" (vector-ref (make-vector 3 'x) 1) 'x)
(check "vector-append" (vector-append '#(1 2) '#(3 4)) '#(1 2 3 4))


;; =============================================================================
;; 12. 布尔逻辑边缘场景
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
(display ";; === 16. Mixed operations ===\n")

(define (vector-sum v)
  (let ((n (vector-length v)))
    (let loop ((i 0) (sum 0))
      (if (= i n) sum (loop (+ i 1) (+ sum (vector-ref v i)))))))
(check "vector-sum" (vector-sum '#(10 20 30 40)) 100)

;; 16.1 map + vector
(define (vec-map f v)
  (let* ((n (vector-length v))
         (r (make-vector n)))
    (let loop ((i 0))
      (if (= i n) r (begin (vector-set! r i (f (vector-ref v i))) (loop (+ i 1)))))))
(check "vector map"
       (vector->list (vec-map (lambda (x) (* x x)) '#(1 2 3 4)))
       '(1 4 9 16))


;; =============================================================================
;; 17. 复杂宏 — 模式匹配性能 & 深度测试
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
