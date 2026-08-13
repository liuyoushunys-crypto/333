;; test-standards.scm — Standards: R7RS, SRFI compliance, Scheme equivalence
;; Generated from merged test suites

;; test-equivalence.scm — merged test file
;; Scheme vs Python builtin equivalence


(display "\n=== test-scm-all.scm ===\n")
;; ============================================================
;; 全面测试：哪些 Python builtins 可用纯 Scheme 等价实现
;; ============================================================

(define *pass* 0) (define *fail* 0)
(define (check label actual expected)
  (if (equal? actual expected)
      (begin (set! *pass* (+ *pass* 1)))
      (begin (set! *fail* (+ *fail* 1))
             (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(define (check-approx label actual expected)
  (if (< (abs (- actual expected)) 1e-10)
      (set! *pass* (+ *pass* 1))
      (begin (set! *fail* (+ *fail* 1))
             (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

;; ============== 算术 ==============
(display "\n--- 算术 ---\n")
(check "+" (+ 1 2) 3)
(check "-" (- 5 3) 2)
(check "*" (* 2 3) 6)
(check "/" (/ 10 3) 10/3)
(check "abs" (abs -5) 5)
(check "expt" (expt 2 10) 1024)
(check "square" (* 5 5) 25)
(check "quotient" (quotient 10 3) 3)
(check "remainder" (remainder 10 3) 1)
(check "modulo" (modulo -10 3) 2)
(check "floor-quotient" (floor-quotient 10 3) 3)
(check "floor-remainder" (floor-remainder 10 3) 1)
(check "truncate-quotient" (truncate-quotient 10 3) 3)
(check "truncate-remainder" (truncate-remainder 10 3) 1)
(check "gcd" (gcd 12 18 24) 6)
(check "lcm" (lcm 4 6 8) 24)
(check "numerator" (numerator 6/4) 3)
(check "denominator" (denominator 6/4) 2)
(check "integer-length" (integer-length 42) 6)
(check "exact-integer-sqrt" (call-with-values (lambda () (exact-integer-sqrt 25)) list) '(5 0))

;; ============== 比较器 ==============
(display "\n--- 比较器 ---\n")
(check "=" (= 1 1 1) #t)
(check "<" (< 1 2 3) #t)
(check ">" (> 3 2 1) #t)
(check "<=" (<= 1 2 2) #t)
(check ">=" (>= 3 2 2) #t)
(check "zero?" (zero? 0) #t)
(check "positive?" (positive? 5) #t)
(check "negative?" (negative? -3) #t)
(check "even?" (even? 4) #t)
(check "odd?" (odd? 3) #t)

;; ============== 三角函数 ==============
(display "\n--- 三角函数 ---\n")
(check-approx "sin" (sin 0) 0.0)
(check-approx "cos" (cos 0) 1.0)
(check-approx "tan" (tan 0) 0.0)
(check-approx "exp" (exp 0) 1.0)
(check-approx "log" (log 1) 0.0)
(check-approx "sqrt" (sqrt 4) 2.0)
(check-approx "asin" (asin 0) 0.0)
(check-approx "acos" (acos 1) 0.0)
(check-approx "atan" (atan 0) 0.0)
(check-approx "sinh" (sinh 0) 0.0)
(check-approx "cosh" (cosh 0) 1.0)
(check-approx "tanh" (tanh 0) 0.0)

;; ============== 类型谓词 ==============
(display "\n--- 类型谓词 ---\n")
(check "number? 42" (number? 42) #t)
(check "complex? 1+2i" (complex? 1+2i) #t)
(check "real? 3.14" (real? 3.14) #t)
(check "rational? 2/3" (rational? 2/3) #t)
(check "integer? 42" (integer? 42) #t)
(check "exact? 42" (exact? 42) #t)
(check "inexact? 3.14" (inexact? 3.14) #t)
(check "exact->inexact" (exact->inexact 3) 3.0)
(check "inexact->exact" (inexact->exact 3.0) 3)
(check "real-part" (real-part 1+2i) 1.0)
(check "imag-part" (imag-part 1+2i) 2.0)

;; ============== 取整 ==============
(display "\n--- 取整 ---\n")
(check "floor" (floor 3.7) 3.0)
(check "ceiling" (ceiling 3.1) 4.0)
(check "truncate" (truncate 3.7) 3.0)
(check "round" (round 3.5) 4.0)
(check "floor->exact" (floor->exact 3.7) 3)
(check "ceiling->exact" (ceiling->exact 3.1) 4)
(check "truncate->exact" (truncate->exact 3.7) 3)
(check "round->exact" (round->exact 3.5) 4)
(check "rationalize" (rationalize 0.333 0.001) 1/3)

;; ============== 列表基本操作 ==============
(display "\n--- 列表 ---\n")
(check "cons" (cons 1 '(2 3)) '(1 2 3))
(check "car" (car '(1 2 3)) 1)
(check "cdr" (cdr '(1 2 3)) '(2 3))
(check "null?" (null? '()) #t)
(check "pair?" (pair? '(1)) #t)
(check "list" (list 1 2 3) '(1 2 3))
(check "length" (length '(1 2 3 4 5)) 5)
(check "append" (append '(1 2) '(3 4)) '(1 2 3 4))
(check "reverse" (reverse '(1 2 3)) '(3 2 1))
(check "list-ref" (list-ref '(a b c) 1) 'b)
(check "list-tail" (list-tail '(a b c) 1) '(b c))
(check "last-pair" (last-pair '(1 2 3)) '(3))
(check "list-copy" (list-copy '(1 2 3)) '(1 2 3))

;; ============== 布尔 ==============
(display "\n--- 布尔 ---\n")
(check "not #f" (not #f) #t)
(check "not #t" (not #t) #f)
(check "not 0" (not 0) #f)
(check "boolean?" (boolean? #t) #t)
(check "boolean? 0" (boolean? 0) #f)

;; ============== IO ==============
(display "\n--- IO ---\n")
(check "eof-object?" (eof-object? (eof-object)) #t)
(check "port?" (port? (current-input-port)) #t)
(check "input-port?" (input-port? (current-input-port)) #t)
(check "output-port?" (output-port? (current-output-port)) #t)
(check "port-open?" (port-open? (current-input-port)) #t)

;; ============== 宏展开 ==============
(display "\n--- 宏展开 ---\n")
(check "bound-identifier=?" (bound-identifier=? (datum->syntax #t 'a) (datum->syntax #t 'a)) #t)
(check "free-identifier=?" (free-identifier=? (datum->syntax #t 'a) (datum->syntax #t 'a)) #t)

;; ============== hash-table ==============
(display "\n--- hash-table ---\n")
(define ht (make-hash-table))
(hash-table-set! ht 'a 1)
(check "hash-table-ref" (hash-table-ref ht 'a) 1)
(check "hash-table-contains?" (hash-table-contains? ht 'a) #t)
(check "hash-table-size" (hash-table-size ht) 1)
(check "hash-table-ref/default" (hash-table-ref/default ht 'b 42) 42)

;; ============== 符号 ==============
(display "\n--- 符号 ---\n")
(check "symbol?" (symbol? 'x) #t)
(check "symbol->string" (symbol->string 'hello) "hello")
(check "string->symbol" (string->symbol "hello") 'hello)
(check "symbol=?" (symbol=? 'a 'a) #t)

;; ============== 字符串 ==============
(display "\n--- 字符串 ---\n")
(check "string?" (string? "hello") #t)
(check "string-length" (string-length "hello") 5)
(check "string-ref" (string-ref "hello" 1) #\e)
(check "substring" (substring "hello" 1 3) "el")
(check "string-append" (string-append "a" "b" "c") "abc")

;; ============== 字符 ==============
(display "\n--- 字符 ---\n")
(check "char?" (char? #\a) #t)
(check "char->integer" (char->integer #\a) 97)
(check "integer->char" (integer->char 97) #\a)
(check "char=?" (char=? #\a #\a) #t)
(check "char<?" (char<? #\a #\b) #t)
(check "char-alphabetic?" (char-alphabetic? #\a) #t)
(check "char-numeric?" (char-numeric? #\9) #t)
(check "char-whitespace?" (char-whitespace? #\space) #t)
(check "char-upper-case?" (char-upper-case? #\A) #t)
(check "char-lower-case?" (char-lower-case? #\a) #t)
(check "char-upcase" (char-upcase #\a) #\A)
(check "char-downcase" (char-downcase #\A) #\a)

;; ============== 向量 ==============
(display "\n--- 向量 ---\n")
(check "vector?" (vector? #(1 2 3)) #t)
(check "make-vector" (make-vector 3 'x) #(x x x))
(check "vector" (vector 1 2 3) #(1 2 3))
(check "vector-ref" (vector-ref #(a b c) 1) 'b)
(check "vector-length" (vector-length #(1 2 3)) 3)
(check "vector->list" (vector->list #(a b c)) '(a b c))
(check "list->vector" (list->vector '(a b c)) #(a b c))

;; ============== bytevector ==============
(display "\n--- bytevector ---\n")
(define bv (make-bytevector 3 65))
(check "bytevector?" (bytevector? bv) #t)
(check "bytevector-u8-ref" (bytevector-u8-ref bv 0) 65)
(check "bytevector-length" (bytevector-length bv) 3)



;; ============== 过程类型 ==============
(display "\n--- 过程 ---\n")
(check "procedure?" (procedure? +) #t)
(check "apply" (apply + '(1 2 3)) 6)

;; ============== 控制 ==============
(display "\n--- 控制 ---\n")
(check "values" (call-with-values (lambda () (values 1 2)) list) '(1 2))
(check "promise?" (promise? (delay (+ 1 2))) #t)
(check "force" (force (delay (+ 1 2))) 3)

;; ============== Report ==============
(display "\n")
(display "=== 完成: ") (display *pass*) (display " PASS, ")
(display *fail*) (display " FAIL ===\n")

;; ============== 错误/条件 ==============
(display "\n--- 错误/条件 ---\n")
(check "error-object?" (call/cc (lambda (k) (with-exception-handler (lambda (e) (k (error-object? e))) (lambda () (error "test" "msg"))))) #t)
(check "error-object-message" (call/cc (lambda (k) (with-exception-handler (lambda (e) (k (error-object-message e))) (lambda () (error "test" "msg"))))) "test")
(check "condition?" (condition? (make-compound-condition)) #t)

;; ============== 环境 ==============
(display "\n--- 环境 ---\n")
(check "interaction-environment" (interaction-environment) (interaction-environment))
(check "scheme-report-environment" (scheme-report-environment 7) (scheme-report-environment 7))


(display "\n=== test-scm-equiv.scm ===\n")
;; ============================================================
;; 测试哪些 Python builtins 可以用纯 Scheme 等价实现
;; 不修改任何 Python 代码，只验证 Scheme 版本的正确性
;; ============================================================

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

;; ============================================================
;; 1. 数值谓词 — 全部可用 Scheme 表达
;; ============================================================

(display "\n=== 数值谓词 ===\n")

(define (my-zero? x) (= x 0))
(define (my-positive? x) (> x 0))
(define (my-negative? x) (< x 0))
(define (my-odd? x) (= (modulo x 2) 1))
(define (my-even? x) (= (modulo x 2) 0))
(define (my-finite? x) (not (or (infinite? x) (nan? x))))
(define (my-square x) (* x x))
(define (my-abs x) (if (< x 0) (- x) x))

(check "zero? 0"          (my-zero? 0) #t)
(check "zero? 1"          (my-zero? 1) #f)
(check "positive? 5"      (my-positive? 5) #t)
(check "positive? -1"     (my-positive? -1) #f)
(check "negative? -3"     (my-negative? -3) #t)
(check "negative? 0"      (my-negative? 0) #f)
(check "odd? 3"           (my-odd? 3) #t)
(check "odd? 4"           (my-odd? 4) #f)
(check "even? 2"          (my-even? 2) #t)
(check "even? 3"          (my-even? 3) #f)
(check "square 5"         (my-square 5) 25)
(check "abs 5"            (my-abs 5) 5)
(check "abs -5"           (my-abs -5) 5)

;; 与 Python 原生版对比
(check "zero? vs py"      (eqv? (my-zero? 0) (zero? 0)) #t)
(check "positive? vs py"  (eqv? (my-positive? 5) (positive? 5)) #t)

;; ============================================================
;; 2. 简单数学运算
;; ============================================================

(display "\n=== 简单数学运算 ===\n")

(define (my-min a b) (if (< a b) a b))
(define (my-max a b) (if (> a b) a b))
(define (my-clamp x lo hi) (my-max lo (my-min x hi)))

(check "min 3 7"          (my-min 3 7) 3)
(check "max 3 7"          (my-max 3 7) 7)
(check "clamp 5 1 10"    (my-clamp 5 1 10) 5)
(check "clamp 0 1 10"    (my-clamp 0 1 10) 1)
(check "clamp 20 1 10"   (my-clamp 20 1 10) 10)

;; ============================================================
;; 3. 列表操作
;; ============================================================

(display "\n=== 列表操作 ===\n")

(define (my-last-pair lst)
  (if (null? (cdr lst)) lst (my-last-pair (cdr lst))))

(define (my-list-ref lst k)
  (if (= k 0) (car lst) (my-list-ref (cdr lst) (- k 1))))

(define (my-list-tail lst k)
  (if (= k 0) lst (my-list-tail (cdr lst) (- k 1))))

(define (my-length lst)
  (let loop ((n 0) (xs lst))
    (if (null? xs) n (loop (+ n 1) (cdr xs)))))

(define (my-append . lists)
  (if (null? lists) '()
      (let ((first (car lists)))
        (if (null? first) (apply my-append (cdr lists))
            (cons (car first) (apply my-append (cons (cdr first) (cdr lists))))))))

(check "last-pair (1 2 3)" (my-last-pair '(1 2 3)) '(3))
(check "list-ref 0"        (my-list-ref '(a b c) 0) 'a)
(check "list-ref 2"        (my-list-ref '(a b c) 2) 'c)
(check "list-tail 1"       (my-list-tail '(a b c) 1) '(b c))
(check "length"            (my-length '(1 2 3 4 5)) 5)
(check "append"            (my-append '(1 2) '(3 4)) '(1 2 3 4))

;; ============================================================
;; 4. 函数组合器
;; ============================================================

(display "\n=== 函数组合器 ===\n")

(define (my-flip f) (lambda (a b) (f b a)))
(define (my-complement f) (lambda (x) (not (f x))))
(define (my-const x) (lambda _ x))
(define (my-compose . fns)
  (lambda (x)
    (let loop ((fns (reverse fns)) (result x))
      (if (null? fns) result
          (loop (cdr fns) ((car fns) result))))))

(check "flip"              ((my-flip -) 5 3) -2)
(check "complement"        ((my-complement even?) 3) #t)
(check "const"             ((my-const 42) 1 2 3) 42)
(check "compose"           ((my-compose (lambda (x) (* x 2))
                                         (lambda (x) (+ x 1))) 5) 12)

;; ============================================================
;; 5. 列表遍历高阶函数
;; ============================================================

(display "\n=== 列表遍历 ===\n")

(define (my-for-each fn lst)
  (if (not (null? lst))
    (begin (fn (car lst)) (my-for-each fn (cdr lst)))))

(define (my-map fn lst)
  (if (null? lst) '()
      (cons (fn (car lst)) (my-map fn (cdr lst)))))

(define (my-filter pred lst)
  (cond ((null? lst) '())
        ((pred (car lst)) (cons (car lst) (my-filter pred (cdr lst))))
        (else (my-filter pred (cdr lst)))))

(define (my-find pred lst)
  (cond ((null? lst) #f)
        ((pred (car lst)) (car lst))
        (else (my-find pred (cdr lst)))))

(check "map"              (my-map - '(1 2 3)) '(-1 -2 -3))
(check "filter"           (my-filter even? '(1 2 3 4 5 6)) '(2 4 6))
(check "find found"       (my-find even? '(1 2 3 4)) 2)
(check "find missing"     (my-find (lambda (x) (= x 10)) '(1 2 3)) #f)

;; for-each 副作用测试
(let ((acc '()))
  (my-for-each (lambda (x) (set! acc (cons x acc))) '(1 2 3))
  (check "for-each" (reverse acc) '(1 2 3)))

;; ============================================================
;; 6. 列表折叠
;; ============================================================

(display "\n=== 列表折叠 ===\n")

(define (my-fold-left f init lst)
  (let loop ((acc init) (xs lst))
    (if (null? xs) acc
        (loop (f acc (car xs)) (cdr xs)))))

(define (my-fold-right f init lst)
  (let loop ((xs lst) (acc init))
    (if (null? xs) acc
        (loop (cdr xs) (f (car xs) acc)))))

(define (my-reverse lst)
  (my-fold-left (lambda (acc x) (cons x acc)) '() lst))

(define (my-length-v2 lst)
  (my-fold-left (lambda (n _) (+ n 1)) 0 lst))

(check "fold-left +"      (my-fold-left + 0 '(1 2 3 4)) 10)
(check "fold-right -"     (my-fold-right - 0 '(1 2 3)) 2)
(check "reverse"          (my-reverse '(1 2 3)) '(3 2 1))
(check "length v2"        (my-length-v2 '(a b c d)) 4)

;; ============================================================
;; 7. 比较器
;; ============================================================

(display "\n=== 比较器 ===\n")

(define (my<? . args)
  (or (null? args) (null? (cdr args))
      (and (< (car args) (cadr args))
           (apply my<? (cdr args)))))

(define (my<=? . args)
  (or (null? args) (null? (cdr args))
      (and (<= (car args) (cadr args))
           (apply my<=? (cdr args)))))

(check "<? 1 2 3"         (my<? 1 2 3) #t)
(check "<? 1 3 2"         (my<? 1 3 2) #f)
(check "<=? 1 2 2"        (my<=? 1 2 2) #t)
(check "<=? 1 2 1"        (my<=? 1 2 1) #f)

;; ============================================================
;; 8. 类型转换
;; ============================================================

(display "\n=== 类型转换 ===\n")

(define (my-number->string n)
  (let ((s (number->string n)))
    (if (and (string? s) (> (string-length s) 1)
             (char=? (string-ref s 0) #\#))
        (substring s 2 (string-length s))
        s)))

(check "number->string 42" (my-number->string 42) "42")

(display "\n=== 全部测试完成 ===\n")



(display "\n=== test-types.scm ===\n")
;; R7RS and SRFI compliance tests


(display "\n=== r7rs-tests.scm ===\n")
;; ============================================================
;; 有理数/复数/R5RS/R6RS/R7RS 综合测试
;; ============================================================

(define (test-equal name expected actual)
  (if (equal? actual expected)
      (display (string-append "[PASS] " name))
      (begin
        (display (string-append "[FAIL] " name "  expected: "))
        (write expected)
        (display "  actual: ")
        (write actual)))
  (newline))

;; ==================== 有理数 ====================
(display "\n=== 有理数 ===\n")

(test-equal "1/2" 1/2 (+ 1/2))
(test-equal "1/2 + 1/3 = 5/6" 5/6 (+ 1/2 1/3))
(test-equal "2/3 * 3/4 = 1/2" 1/2 (* 2/3 3/4))
(test-equal "1/2 / 2 = 1/4" 1/4 (/ 1/2 2))
(test-equal "1 + 1/2 = 3/2" 3/2 (+ 1 1/2))
(test-equal "1/2 - 1/3 = 1/6" 1/6 (- 1/2 1/3))
(test-equal "-1/2" -1/2 (- 1/2))
(test-equal "numerator 3/4" 3 (numerator 3/4))
(test-equal "denominator 3/4" 4 (denominator 3/4))
(test-equal "numerator 6" 6 (numerator 6))

(test-equal "real? 3/4" #t (real? 3/4))
(test-equal "rational? 3/4" #t (rational? 3/4))
(test-equal "integer? 3/4" #f (integer? 3/4))
(test-equal "integer? 4" #t (integer? 4))
(test-equal "number? 3/4" #t (number? 3/4))
(test-equal "complex? 3/4" #t (complex? 3/4))
(test-equal "exact? 3/4" #t (exact? 3/4))
(test-equal "inexact? 3/4" #f (inexact? 3/4))

(test-equal "= 1/2 2/4" #t (= 1/2 2/4))
(test-equal "< 1/2 3/4" #t (< 1/2 3/4))
(test-equal "> 3/4 1/2" #t (> 3/4 1/2))
(test-equal "<= 1/2 1/2" #t (<= 1/2 1/2))
(test-equal ">= 1/2 1/2" #t (>= 1/2 1/2))

;; ==================== 复数 ====================
(display "\n=== 复数 ===\n")

(test-equal "3+4i" 3+4i (+ 3 4i))
(test-equal "3-4i" 3-4i (+ 3 -4i))
(test-equal "real-part 3+4i" 3.0 (real-part 3+4i))
(test-equal "imag-part 3+4i" 4.0 (imag-part 3+4i))
(test-equal "make-rectangular 3 4" 3+4i (make-rectangular 3 4))
(test-assert "make-polar" (< (abs (real-part (make-polar 2 (/ pi 2)))) 1e-10))
(test-equal "make-polar imag" 2.0 (imag-part (make-polar 2 (/ pi 2))))
(test-equal "complex? 3+4i" #t (complex? 3+4i))
(test-equal "complex? 3" #t (complex? 3))
(test-equal "number? 3+4i" #t (number? 3+4i))
(test-equal "real? 3+4i" #f (real? 3+4i))
(test-equal "real? 3" #t (real? 3))

(test-equal "magnitude 3+4i" 5.0 (magnitude 3+4i))
(test-equal "angle 1+0i" 0.0 (angle 1+0i))

(test-equal "exact? 3+4i" #f (exact? 3+4i))
(test-equal "inexact? 3+4i" #t (inexact? 3+4i))

(test-equal "(+ 1+2i 3+4i)" 4+6i (+ 1+2i 3+4i))
(test-equal "(* 1+2i 3+4i)" -5+10i (* 1+2i 3+4i))
(test-equal "(/ 1+2i)" .2-.4i (/ 1+2i))

;; ==================== R5RS/R6RS/R7RS 通用 ====================
(display "\n=== R5RS/R6RS/R7RS 通用 ===\n")

(test-equal "inexact->exact 3.0" 3 (inexact->exact 3.0))
(test-equal "exact->inexact 3" 3.0 (exact->inexact 3))
(test-equal "sqrt 9" 3 (sqrt 9))

;; expt with rational
(test-equal "expt 4 1/2" 2.0 (expt 4 1/2))
(test-equal "expt 8 1/3" 2.0 (expt 8 1/3))

(test-equal "abs -5" 5 (abs -5))
(test-equal "abs -3/4" 3/4 (abs -3/4))

(test-equal "quotient 7 3" 2 (quotient 7 3))
(test-equal "remainder 7 3" 1 (remainder 7 3))
(test-equal "modulo 7 3" 1 (modulo 7 3))
(test-equal "gcd 12 8" 4 (gcd 12 8))
(test-equal "lcm 4 6" 12 (lcm 4 6))

(test-equal "floor 3.7" 3.0 (floor 3.7))
(test-equal "ceiling 3.2" 4.0 (ceiling 3.2))
(test-equal "truncate 3.7" 3.0 (truncate 3.7))
(test-equal "round 3.5" 4.0 (round 3.5))

;; ==================== R5RS 额外 ====================
(display "\n=== R5RS 额外 ===\n")

(test-equal "string->number #xff" 255 (string->number "#xff"))
(test-equal "string->number #o377" 255 (string->number "#o377"))
(test-equal "string->number #b11111111" 255 (string->number "#b11111111"))
(test-equal "string->number \"3/4\"" 3/4 (string->number "3/4"))
(test-equal "number->string 42" "42" (number->string 42))
(test-equal "number->string 1/2" "1/2" (number->string 1/2))

(test-equal "char->integer A" 65 (char->integer #\A))
(test-equal "integer->char 65" #\A (integer->char 65))

;; ==================== R6RS 额外 ====================
(display "\n=== R6RS 额外 ===\n")

(test-equal "exact-integer? 5" #t (exact-integer? 5))
(test-equal "exact-integer? 3/4" #f (exact-integer? 3/4))

(test-equal "bitwise-and 6 3" 2 (bitwise-and 6 3))
(test-equal "bitwise-ior 6 3" 7 (bitwise-ior 6 3))
(test-equal "bitwise-xor 6 3" 5 (bitwise-xor 6 3))
(test-equal "bitwise-not 3" -4 (bitwise-not 3))
(test-equal "arithmetic-shift 2 2" 8 (arithmetic-shift 2 2))

;; ==================== R7RS 额外 ====================
(display "\n=== R7RS 额外 ===\n")

(test-equal "exact? 3" #t (exact? 3))
(test-equal "exact? 3.0" #f (exact? 3.0))
(test-equal "inexact? 3.0" #t (inexact? 3.0))
(test-equal "inexact? 3" #f (inexact? 3))

(test-equal "rational? 3" #t (rational? 3))
(test-equal "rational? 3.5" #f (rational? 3.5))

(display "\n=== 测试完成 ===\n")


(display "\n=== srfi-tests.scm ===\n")
;; ============================================================
;; SRFI 综合测试套件
;; 覆盖 SRFI-1/13/14/16/23/26/28/31/39/43/60/78/95/158
;; ============================================================

(define (test-equal name expected actual)
  (if (equal? actual expected)
      (display (string-append "[PASS] " name))
      (begin
        (display (string-append "[FAIL] " name "  expected: "))
        (write expected)
        (display "  actual: ")
        (write actual)))
  (newline))

;; ==================== SRFI-1: 列表库 ====================
(display "\n=== SRFI-1: 列表库 ===\n")

(test-equal "iota 5" '(0 1 2 3 4) (iota 5))
(test-equal "iota 3 10" '(10 11 12) (iota 3 10))
(test-equal "iota 4 1 2" '(1 3 5 7) (iota 4 1 2))

(test-equal "take 3" '(1 2 3) (take '(1 2 3 4 5) 3))
(test-equal "drop 3" '(4 5) (drop '(1 2 3 4 5) 3))
(test-equal "split-at 3" '((1 2 3) (4 5))
             (call-with-values (lambda () (split-at '(1 2 3 4 5) 3)) list))

(test-equal "take-while even" '(2 4 6) (take-while even? '(2 4 6 7 8)))
(test-equal "drop-while even" '(7 8) (drop-while even? '(2 4 6 7 8)))
(test-equal "span even" '((2 4 6) (7 8))
             (call-with-values (lambda () (span even? '(2 4 6 7 8))) list))
(test-equal "break even" '((1 3) (4 6 7))
             (call-with-values (lambda () (break even? '(1 3 4 6 7))) list))

(test-equal "any even?" #t (any even? '(1 3 5 7 8)))
(test-equal "any even? #f" #f (any even? '(1 3 5 7)))
(test-equal "every odd?" #t (every odd? '(1 3 5 7)))
(test-equal "every odd? #f" #f (every odd? '(1 3 5 8)))

(test-equal "filter odd" '(1 3 5) (filter odd? '(1 2 3 4 5)))
(test-equal "remove even" '(1 3 5) (remove even? '(1 2 3 4 5)))
(test-equal "partition odd" '((1 3 5) (2 4))
             (call-with-values (lambda () (partition odd? '(1 2 3 4 5))) list))

(test-equal "fold + 0" 15 (fold + 0 '(1 2 3 4 5)))
(test-equal "fold-right - 0" (- 1 (- 2 (- 3 0))) (fold-right - 0 '(1 2 3)))
(test-equal "fold-right cons '()" '(1 2 3 4) (fold-right cons '() '(1 2 3 4)))

(test-equal "filter-map square" '(1 9 25) (filter-map (lambda (x) (if (odd? x) (* x x) #f)) '(1 2 3 4 5)))
(test-equal "map-in-order" '(2 4 6) (map-in-order (lambda (x) (* x 2)) '(1 2 3)))

(test-equal "alist-cons" '((a . 1) (b . 2)) (alist-cons 'a 1 '((b . 2))))
(test-equal "length+" 5 (length+ '(1 2 3 4 5)))
(test-equal "length+ #f" #f (length+ (lambda (x) x)))
(test-equal "circular-list?" #t (circular-list? (circular-list 1 2 3)))
(test-equal "proper-list?" #t (proper-list? '(1 2 3)))
(test-equal "proper-list? #f" #f (proper-list? (circular-list 1 2)))

;; ==================== SRFI-13: 字符串库 ====================
(display "\n=== SRFI-13: 字符串库 ===\n")

(test-equal "string-join space" "a b c" (string-join '("a" "b" "c")))
(test-equal "string-join comma" "a,b,c" (string-join '("a" "b" "c") ","))
(test-equal "string-split space" '("a" "b" "c") (string-split "a b c"))
(test-equal "string-split comma" '("a" "b" "c") (string-split "a,b,c" ","))
(test-equal "string-split colon" '("192" "168" "1" "1") (string-split "192:168:1:1" ":"))

(test-equal "string-trim" "hello" (string-trim "  hello  "))
(test-equal "string-trim-left" "hello  " (string-trim-left "  hello  "))
(test-equal "string-trim-right" "  hello" (string-trim-right "  hello  "))

(test-equal "string-pad 10" "  hello" (string-pad "hello" 7))
(test-equal "string-pad-right 10" "hello  " (string-pad-right "hello" 7))
(test-equal "string-trim spaces" "x y" (string-trim " \t\nx y\r " char-whitespace?))

(test-equal "string-fold" 532 (string-fold (lambda (c sum) (+ (char->integer c) sum)) 0 "hello"))
(test-equal "string-fold-right" "olleh" (string-fold-right (lambda (c r) (string-append r (string c))) "" "hello"))

(test-equal "string-replace" "hxllo" (string-replace "hello" "x" 1 2))
(test-equal "string-replace full" "abc" (string-replace "hello" "abc" 0 5))

(test-equal "string-tokenize" '("hello" "world") (string-tokenize "hello world"))
(test-equal "string-prefix-length" 2 (string-prefix-length "abc" "abd"))
(test-equal "string-suffix-length" 3 (string-suffix-length "bcd" "abcd"))

(test-equal "string-prefix?" #t (string-prefix? "abc" "abcdef"))
(test-equal "string-suffix?" #f (string-suffix? "cde" "abcdef"))
(test-equal "string-prefix? #f" #f (string-prefix? "xyz" "abcdef"))

;; ==================== SRFI-14: 字符集 ====================
(display "\n=== SRFI-14: 字符集 ===\n")

(test-equal "char-set? #t" #t (char-set? (string->char-set "abc")))
(test-equal "char-set-contains? #t" #t (char-set-contains? (string->char-set "abc") #\a))
(test-equal "char-set-contains? #f" #f (char-set-contains? (string->char-set "abc") #\d))

(test-equal "char-set->list length" 52 (length (char-set->list char-set:letter)))
(test-equal "char-set:lower length" 26 (length (char-set->list char-set:lower-case)))
(test-equal "char-set:upper length" 26 (length (char-set->list char-set:upper-case)))

(test-equal "char-set:digit contains 5" #t (char-set-contains? char-set:digit #\5))
(test-equal "char-set:digit no X" #f (char-set-contains? char-set:digit #\X))
(test-equal "char-set:letter" #t (char-set-contains? char-set:letter #\A))
(test-equal "char-set:letter z" #t (char-set-contains? char-set:letter #\z))

(test-equal "char-set:whitespace space" #t (char-set-contains? char-set:whitespace #\space))
(test-equal "char-set:whitespace tab" #t (char-set-contains? char-set:whitespace #\tab))
(test-equal "char-set:whitespace newline" #t (char-set-contains? char-set:whitespace #\newline))

(test-equal "char-set:upper" #t (char-set-contains? char-set:upper #\A))
(test-equal "char-set:upper f" #f (char-set-contains? char-set:upper #\a))

(test-equal "char-set:lower" #t (char-set-contains? char-set:lower #\a))
(test-equal "char-set:lower A" #f (char-set-contains? char-set:lower #\A))

;; 字符集操作
(define cs1 (string->char-set "abc"))
(define cs2 (string->char-set "bcd"))
(test-equal "char-set-union" #t (char-set-contains? (char-set-union cs1 cs2) #\a))
(test-equal "char-set-union d" #t (char-set-contains? (char-set-union cs1 cs2) #\d))
(test-equal "char-set-intersection b" #t (char-set-contains? (char-set-intersection cs1 cs2) #\b))
(test-equal "char-set-intersection a" #f (char-set-contains? (char-set-intersection cs1 cs2) #\a))
(test-equal "char-set-difference a" #t (char-set-contains? (char-set-difference cs1 cs2) #\a))
(test-equal "char-set-difference d" #f (char-set-contains? (char-set-difference cs1 cs2) #\d))

;; ==================== SRFI-16: case-lambda ====================
(display "\n=== SRFI-16: case-lambda ===\n")

(define foo
  (case-lambda
    (() 0)
    ((x) x)
    ((x y) (+ x y))
    ((x y z) (+ x y z))
    (args (apply + args))))

(test-equal "case-lambda 0" 0 (foo))
(test-equal "case-lambda 1" 42 (foo 42))
(test-equal "case-lambda 2" 7 (foo 3 4))
(test-equal "case-lambda 3" 10 (foo 1 2 3 4))
(test-equal "case-lambda 4" 15 (foo 1 2 3 4 5))

;; ==================== SRFI-23: error ====================
(display "\n=== SRFI-23: error ===\n")

(test-equal "error raises"
  'error-caught
  (guard (exn (else 'error-caught))
    (error "test error")))

;; ==================== SRFI-26: cut/cute ====================
(display "\n=== SRFI-26: cut/cute ===\n")

(define add5 (cut + 5 <>))
(test-equal "cut add5 3" 8 (add5 3))

(define add (cut + <> <>))
(test-equal "cut add 3 4" 7 (add 3 4))

(define mul3 (cute * 3 4 5))
(test-equal "cute mul3" 60 (mul3))

(define div-by (cut / <> 2))
(test-equal "cut div-by 10" 5 (div-by 10))

;; ==================== SRFI-28: format ====================
(display "\n=== SRFI-28: format ===\n")

(test-equal "format ~a" "hello" (format "~a" "hello"))
(test-equal "format ~s" "\"hello\"" (format "~s" "hello"))
(test-equal "format ~d" "42" (format "~d" 42))
(test-equal "format ~%" (string #\a #\newline #\b) (format "a~%b"))
(test-equal "format multi" "x=5 y=10" (format "x=~a y=~a" 5 10))

;; ==================== SRFI-31: rec ====================
(display "\n=== SRFI-31: rec ===\n")

(define fact (rec (fact n)
               (if (zero? n) 1 (* n (fact (- n 1))))))
(test-equal "rec fact 5" 120 (fact 5))
(test-equal "rec fact 0" 1 (fact 0))

(define list-copy2 (rec (copy lst)
                   (if (null? lst) '()
                       (cons (car lst) (copy (cdr lst))))))
(test-equal "rec list-copy" '(1 2 3) (list-copy2 '(1 2 3)))

;; ==================== SRFI-39: parameters ====================
(display "\n=== SRFI-39: parameters ===\n")

(define my-param (make-parameter 10))
(test-equal "param init" 10 (my-param))
(my-param 20)
(test-equal "param set" 20 (my-param))
(parameterize ((my-param 30))
  (test-equal "param in parameterize" 30 (my-param)))
(test-equal "param restored" 20 (my-param))

(define greet (make-parameter "hello"))
(test-equal "greet default" "hello" (greet))
(parameterize ((greet "hi"))
  (test-equal "greet parameterized" "hi" (greet)))
(test-equal "greet restored" "hello" (greet))

;; ==================== SRFI-43: 向量库 ====================
(display "\n=== SRFI-43: 向量库 ===\n")

(test-equal "vector-map add1" #(2 3 4) (vector-map (lambda (x) (+ x 1)) #(1 2 3)))
(test-equal "vector-map +" #(5 7 9) (vector-map + #(1 2 3) #(4 5 6)))
;; vector-for-each returns unspecified; tested via side-effects below
(vector-for-each (lambda (x) #t) #(1 2 3))

(define vsum 0)
(test-equal "vector-for-each sum" 60 (begin (vector-for-each (lambda (x) (set! vsum (+ vsum x))) #(10 20 30)) vsum))

(test-equal "vector-length" 4 (vector-length #(a b c d)))
(test-equal "vector-ref" 'c (vector-ref '#(a b c d) 2))
(test-equal "vector->list" '(1 2 3) (vector->list #(1 2 3)))
(test-equal "list->vector" #(a b c) (list->vector '(a b c)))

(test-equal "vector-append" #(1 2 3 4) (vector-append #(1 2) #(3 4)))
(test-equal "make-vector fill" #(x x x) (make-vector 3 'x))

(define v (make-vector 3 0))
(vector-set! v 0 10)
(vector-set! v 2 30)
(test-equal "vector-set!" #(10 0 30) v)

;; ==================== SRFI-60: 位操作 ====================
(display "\n=== SRFI-60: 位操作 ===\n")

(test-equal "bitwise-and" 2 (bitwise-and 6 3))
(test-equal "bitwise-ior" 7 (bitwise-ior 6 3))
(test-equal "bitwise-xor" 5 (bitwise-xor 6 3))
(test-equal "bitwise-not" -4 (bitwise-not 3))
(test-equal "arithmetic-shift left" 8 (arithmetic-shift 2 2))
(test-equal "arithmetic-shift right" 2 (arithmetic-shift 8 -2))
(test-equal "logand" 2 (logand 6 3))
(test-equal "logior" 7 (logior 6 3))
