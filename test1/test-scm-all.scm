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
