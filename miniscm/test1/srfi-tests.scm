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

(define map-add2 (cut map (cut + <> 2) <>))
(test-equal "cut map-add2" '(4 5 6) (map-add2 '(2 3 4)))

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
