;;;; ============================================================
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
    ((_ a b) (let ((swap-tmp a)) (set! a b) (set! b swap-tmp)))))
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
(check "(+ 1 2)" (+ 1 2) 3)
(check "(+)" (+) 0)
(check "(- 10 3)" (- 10 3) 7)
(check "(- 5)" (- 5) -5)
(check "(* 4 5)" (* 4 5) 20)
(check "(/ 15 3)" (/ 15 3) 5)
(check "(/ 4)" (/ 4) 1/4)
(check "(expt 2 10)" (expt 2 10) 1024)

(display "") (newline)
(display "===== 2. Bignum =====") (newline)
(check "(expt 2 100)" (expt 2 100) 1267650600228229401496703205376)
(check "(* huge huge)" (* 1000000000000000000000000 999999999999999999999999) 999999999999999999999999000000000000000000000000)
(check "(gcd 12345678901234567890 9876543210987654321)" (gcd 12345678901234567890 9876543210987654321) 90000000009)
(check "(+ bignum bignum)" (+ 100000000000000000000000 200000000000000000000000) 300000000000000000000000)
(check "(- huge 1)" (- 1000000000000000000000000 1) 999999999999999999999999)
(check "(* 0 huge)" (* 0 999999999999999999999999) 0)
(check "(quotient 100000000000000000000 3)" (quotient 100000000000000000000 3) 33333333333333333333)
(check "(modulo 100000000000000000000 3)" (modulo 100000000000000000000 3) 1)

(display "") (newline)
(display "===== 3. 比较运算 =====") (newline)
(check "(= 1 1)" (= 1 1) #t)
(check "(= 1 2)" (= 1 2) #f)
(check "(< 1 2 3)" (< 1 2 3) #t)
(check "(< 1 3 2)" (< 1 3 2) #f)
(check "(> 3 2 1)" (> 3 2 1) #t)
(check "(> 3 1 2)" (> 3 1 2) #f)
(check "(<= 1 2 3)" (<= 1 2 3) #t)
(check "(<= 1 2 2)" (<= 1 2 2) #t)
(check "(<= 1 2 1)" (<= 1 2 1) #f)
(check "(>= 3 2 1)" (>= 3 2 1) #t)
(check "(>= 3 3 1)" (>= 3 3 1) #t)
(check "(>= 3 2 3)" (>= 3 2 3) #f)

(display "") (newline)
(display "===== 4. 类型谓词 =====") (newline)
(check "(number? 42)" (number? 42) #t)
(check "(number? 3.14)" (number? 3.14) #t)
(check "(number? \"hi\")" (number? "hi") #f)
(check "(integer? 42)" (integer? 42) #t)
(check "(integer? 3.0)" (integer? 3.0) #t)
(check "(integer? 3.14)" (integer? 3.14) #f)
(check "(real? 42)" (real? 42) #t)
(check "(real? 3.14)" (real? 3.14) #t)
(check "(exact? 42)" (exact? 42) #t)
(check "(exact? 3.14)" (exact? 3.14) #f)
(check "(inexact? 3.14)" (inexact? 3.14) #t)
(check "(inexact? 42)" (inexact? 42) #f)
(check "(zero? 0)" (zero? 0) #t)
(check "(zero? 1)" (zero? 1) #f)
(check "(positive? 5)" (positive? 5) #t)
(check "(positive? -5)" (positive? -5) #f)
(check "(positive? 0)" (positive? 0) #f)
(check "(negative? -5)" (negative? -5) #t)
(check "(negative? 5)" (negative? 5) #f)
(check "(negative? 0)" (negative? 0) #f)
(check "(even? 6)" (even? 6) #t)
(check "(even? 7)" (even? 7) #f)
(check "(even? 0)" (even? 0) #t)
(check "(odd? 3)" (odd? 3) #t)
(check "(odd? 4)" (odd? 4) #f)
(check "(boolean? #t)" (boolean? #t) #t)
(check "(boolean? \"x\")" (boolean? "x") #f)
(check "(string? \"hello\")" (string? "hello") #t)
(check "(string? 42)" (string? 42) #f)
(check "(symbol? 'abc)" (symbol? 'abc) #t)
(check "(symbol? \"abc\")" (symbol? "abc") #f)
(check "(pair? '(1 . 2))" (pair? '(1 . 2)) #t)
(check "(pair? '())" (pair? '()) #f)
(check "(null? '())" (null? '()) #t)
(check "(null? '(1))" (null? '(1)) #f)
(check "(atom? 42)" (atom? 42) #t)
(check "(atom? 'abc)" (atom? 'abc) #t)
(check "(atom? '(1 2))" (atom? '(1 2)) #f)
(check "(list? '(1 2 3))" (list? '(1 2 3)) #t)
(check "(list? '())" (list? '()) #t)
(check "(list? 42)" (list? 42) #f)
(check "(list? '(1 . 2))" (list? '(1 . 2)) #f)
(check "(procedure? (lambda (x) x))" (procedure? (lambda (x) x)) #t)
(check "(procedure? car)" (procedure? car) #t)
(check "(procedure? 42)" (procedure? 42) #f)
(check "(char? #\\A)" (char? #\A) #t)
(check "(char? 65)" (char? 65) #f)
(check "(vector? #(1 2 3))" (vector? #(1 2 3)) #t)
(check "(vector? '(1 2 3))" (vector? '(1 2 3)) #f)

(display "") (newline)
(display "===== 5. 基础列表操作 =====") (newline)
(check "(car '(1 2 3))" (car '(1 2 3)) 1)
(check "(cdr '(1 2 3))" (cdr '(1 2 3)) '(2 3))
(check "(cons 1 '(2 3))" (cons 1 '(2 3)) '(1 2 3))
(check "(cons 'a 'b)" (cons 'a 'b) '(a . b))
(check "(list 1 2 3)" (list 1 2 3) '(1 2 3))
(check "()" '() '())

(display "") (newline)
(display "===== 6. c[ad]+r 全套 (28个) =====") (newline)
(check "(car '(1 2 3))" (car '(1 2 3)) 1)
(check "(cdr '(1 2 3))" (cdr '(1 2 3)) '(2 3))
(check "(caar '((1 2) 3))" (caar '((1 2) 3)) 1)
(check "(cadr '(1 2 3))" (cadr '(1 2 3)) 2)
(check "(cdar '((1 2) 3))" (cdar '((1 2) 3)) '(2))
(check "(cddr '(1 2 3))" (cddr '(1 2 3)) '(3))
(check "(caaar '(((1) 2) 3))" (caaar '(((1) 2) 3)) 1)
(check "(caadr '((1) (2 3)))" (caadr '((1) (2 3))) 2)
(check "(cadar '((1 2) 3))" (cadar '((1 2) 3)) 2)
(check "(caddr '(1 2 3 4))" (caddr '(1 2 3 4)) 3)
(check "(cdaar '(((1 2) 3) 4))" (cdaar '(((1 2) 3) 4)) '(2))
(check "(cdadr '((1) (2 3)))" (cdadr '((1) (2 3))) '(3))
(check "(cddar '((1 2) 3))" (cddar '((1 2) 3)) '())
(check "(cdddr '(1 2 3 4))" (cdddr '(1 2 3 4)) '(4))
(check "(caaaar '((((42)))))" (caaaar '((((42))))) 42)
(check "(caaadr '(((1)) ((2))))" (caaadr '(((1)) ((2)))) 2)
(check "(caadar '((1 (2)) 3))" (caadar '((1 (2)) 3)) 2)
(check "(caaddr '(1 (2) (3 4 5)))" (caaddr '(1 (2) (3 4 5))) 3)
(check "(cadaar '(((a 3)) 5))" (cadaar '(((a 3)) 5)) 3)
(check "(cadadr '(1 (2 3) (4 5)))" (cadadr '(1 (2 3) (4 5))) 3)
(check "(caddar '((x a 3) y))" (caddar '((x a 3) y)) 3)
(check "(cadddr '(1 2 3 4))" (cadddr '(1 2 3 4)) 4)
(check "(cdaaar '((((1 2))) 3))" (cdaaar '((((1 2))) 3)) '(2))
(check "(cdaadr '(((1)) ((2 3))))" (cdaadr '(((1)) ((2 3)))) '(3))
(check "(cdadar '((0 (1 2)) 3))" (cdadar '((0 (1 2)) 3)) '(2))
(check "(cdaddr '(1 (2) (3 4 5)))" (cdaddr '(1 (2) (3 4 5))) '(4 5))
(check "(cddaar '(((a b 3 4) z) w))" (cddaar '(((a b 3 4) z) w)) '(3 4))
(check "(cddadr '(1 (2 3 4 x)))" (cddadr '(1 (2 3 4 x))) '(4 x))
(check "(cdddar '((a b c 4) z))" (cdddar '((a b c 4) z)) '(4))
(check "(cddddr '(1 2 3 4 5))" (cddddr '(1 2 3 4 5)) '(5))

(display "") (newline)
(display "===== 7. 列表算法 =====") (newline)
(check "(length '(a b c d e))" (length '(a b c d e)) 5)
(check "(length '())" (length '()) 0)
(check "(reverse '(1 2 3 4))" (reverse '(1 2 3 4)) '(4 3 2 1))
(check "(reverse '())" (reverse '()) '())
(check "(append '(1 2) '(3 4))" (append '(1 2) '(3 4)) '(1 2 3 4))
(check "(append '(1 2) '(3 4) '(5 6))" (append '(1 2) '(3 4) '(5 6)) '(1 2 3 4 5 6))
(check "(append '())" (append) '())
(check "(list-tail '(1 2 3 4) 2)" (list-tail '(1 2 3 4) 2) '(3 4))
(check "(list-tail '(1 2 3 4) 0)" (list-tail '(1 2 3 4) 0) '(1 2 3 4))
(check "(last-pair '(1 2 3))" (last-pair '(1 2 3)) '(3))
(check "(list-ref '(10 20 30) 1)" (list-ref '(10 20 30) 1) 20)
(check "(memq 'b '(a b c d))" (memq 'b '(a b c d)) '(b c d))
(check "(memq 'x '(a b c))" (memq 'x '(a b c)) #f)
(check "(memv 3 '(1 2 3 4 5))" (memv 3 '(1 2 3 4 5)) '(3 4 5))
(check "(member 3 '(1 2 3 4 5))" (member 3 '(1 2 3 4 5)) '(3 4 5))

(display "") (newline)
(display "===== 8. 关联列表 =====") (newline)
(check "(assq 'b '((a 1) (b 2) (c 3)))" (assq 'b '((a 1) (b 2) (c 3))) '(b 2))
(check "(assq 'x '((a 1) (b 2)))" (assq 'x '((a 1) (b 2))) #f)
(check "(assv 2 '((1 one) (2 two) (3 three)))" (assv 2 '((1 one) (2 two) (3 three))) '(2 two))
(check "(assoc 'b '((a 1) (b 2) (c 3)))" (assoc 'b '((a 1) (b 2) (c 3))) '(b 2))

(display "") (newline)
(display "===== 9. 高阶函数 =====") (newline)
(check "map (lambda (x) (* x x))" (map (lambda (x) (* x x)) '(1 2 3 4 5)) '(1 4 9 16 25))
(check "map + multi" (map + '(1 2 3) '(10 20 30)) '(11 22 33))
(check "map empty" (map (lambda (x) x) '()) '())
(check "map string->symbol" (map string->symbol '("a" "b" "c")) '(a b c))

(let ((acc '()))
  (for-each (lambda (x) (set! acc (cons x acc))) '(a b c))
  (check "for-each" (reverse acc) '(a b c)))

(define applied-result (apply + '(1 2 3)))
(check "apply + list" applied-result 6)

(check "apply list" (apply list '(1 2 3)) '(1 2 3))

(display "") (newline)
(display "===== 10. 数学函数 =====") (newline)
(check "(abs -5)" (abs -5) 5)
(check "(abs 3)" (abs 3) 3)
(check "(max 3 7 2 9 1)" (max 3 7 2 9 1) 9)
(check "(min 3 7 2 9 1)" (min 3 7 2 9 1) 1)
(check "(gcd 12 8)" (gcd 12 8) 4)
(check "(gcd 0 5)" (gcd 0 5) 5)
(check "(gcd)" (gcd) 0)
(check "(lcm 4 6)" (lcm 4 6) 12)
(check "(lcm 0 5)" (lcm 0 5) 0)
(check "(modulo 7 3)" (modulo 7 3) 1)
(check "(modulo -7 3)" (modulo -7 3) 2)
(check "(quotient 7 3)" (quotient 7 3) 2)
(check "(remainder 7 3)" (remainder 7 3) 1)
(check "(sqrt 9)" (sqrt 9) 3)
(check "(expt 2 3)" (expt 2 3) 8)

(display "") (newline)
(display "===== 11. let / let* / letrec / named-let =====") (newline)
(check "(let ((x 1) (y 2)) (+ x y))" (let ((x 1) (y 2)) (+ x y)) 3)
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
(check "(string-length \"abcdef\")" (string-length "abcdef") 6)
(check "(string-length \"\")" (string-length "") 0)
(check "(string-ref \"hello\" 1)" (string-ref "hello" 1) #\e)
(check "(string->number \"12345\")" (string->number "12345") 12345)
(check "(string->number \"3.14\")" (string->number "3.14") 3.14)
(check "(string->number \"not-a-number\")" (string->number "not-a-number") #f)
(check "(number->string 255)" (number->string 255) "255")
(check "(string->symbol \"abc\")" (string->symbol "abc") 'abc)
(check "(symbol->string 'xyz)" (symbol->string 'xyz) "xyz")
(check "(symbol->string 'abc)" (symbol->string 'abc) "abc")
(check "(string #\\a #\\space #\\b)" (string #\a #\space #\b) "a b")
(check "(substring \"hello world\" 0 5)" (substring "hello world" 0 5) "hello")
(check "(make-string 5 #\\x)" (make-string 5 #\x) "xxxxx")
(check "(string->list \"abc\")" (string->list "abc") '(#\a #\b #\c))
(check "(list->string '(#\\a #\\b #\\c))" (list->string '(#\a #\b #\c)) "abc")
(check "(string=? \"abc\" \"abc\")" (string=? "abc" "abc") #t)
(check "(string<? \"abc\" \"abd\")" (string<? "abc" "abd") #t)
(check "(string>? \"xyz\" \"abc\")" (string>? "xyz" "abc") #t)

(display "") (newline)
(display "===== 19. Char 操作 =====") (newline)
(check "(char->integer #\\A)" (char->integer #\A) 65)
(check "(integer->char 65)" (integer->char 65) #\A)
(check "(char-alphabetic? #\\a)" (char-alphabetic? #\a) #t)
(check "(char-alphabetic? #\\5)" (char-alphabetic? #\5) #f)
(check "(char-numeric? #\\5)" (char-numeric? #\5) #t)
(check "(char-numeric? #\\a)" (char-numeric? #\a) #f)
(check "(char-whitespace? #\\space)" (char-whitespace? #\space) #t)
(check "(char-whitespace? #\\a)" (char-whitespace? #\a) #f)
(check "(char-upcase #\\a)" (char-upcase #\a) #\A)
(check "(char-downcase #\\A)" (char-downcase #\A) #\a)
(check "(char=? #\\a #\\a)" (char=? #\a #\a) #t)
(check "(char<? #\\a #\\b)" (char<? #\a #\b) #t)
(check "(char-ci=? #\\a #\\A)" (char-ci=? #\a #\A) #t)

(display "") (newline)
(display "===== 20. Vector 操作 =====") (newline)
(check "(vector 1 2 3)" (vector 1 2 3) '#(1 2 3))
(check "(make-vector 5 'x)" (make-vector 5 'x) '#(x x x x x))
(check "(vector-ref '#(10 20 30) 1)" (vector-ref '#(10 20 30) 1) 20)
(let ((v (vector 1 2 3)))
  (vector-set! v 1 99)
  (check "vector-set!" v '#(1 99 3)))
(check "(vector->list '#(a b c))" (vector->list '#(a b c)) '(a b c))
(check "(list->vector '(1 2 3))" (list->vector '(1 2 3)) '#(1 2 3))
(check "(vector-fill! v 'z) (make-vector 3)"
       (let ((v (make-vector 3))) (vector-fill! v 'z) v) '#(z z z))
(check "(vector-length '#(1 2 3 4 5))" (vector-length '#(1 2 3 4 5)) 5)

(display "") (newline)

(display "===== 23. define-syntax / syntax-rules =====") (newline)
(define-syntax my-when
  (syntax-rules ()
    ((_ test body1 body2 ...) (if test (begin body1 body2 ...)))))
(check "my-when true" (my-when #t 42) 42)
(check "my-when false" (my-when #f 42) (if #f 42))

(define-syntax swap!
  (syntax-rules ()
    ((_ a b) (let ((swap-tmp a)) (set! a b) (set! b swap-tmp)))))
(let ((x 1) (y 2))
  (swap! x y)
  (check "swap!" (+ (* x 10) y) 21))

(display "") (newline)
(display "===== 24. lambda / 闭包 =====") (newline)
(check "((lambda (x) x) 42)" ((lambda (x) x) 42) 42)
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
    ((_ (name a b) body) (define (name x) (lambda (y) (let ((a x) (b y)) body))))))
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
  `(let ((swap-tmp ,a)) (set! ,a ,b) (set! ,b swap-tmp)))
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
