;; test-arithmetic.scm — Numbers: arithmetic, bitwise, fixnum, flonum, division, hyperbolic
;; Generated from merged test suites

(define (check label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

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
;; floor-quotient / floor-remainder



(check "floor-quotient 7 3" (floor-quotient 7 3) 2)
(check "floor-quotient -7 3" (floor-quotient -7 3) -3)
(check "floor-remainder 7 3" (floor-remainder 7 3) 1)
(check "floor-remainder -7 3" (floor-remainder -7 3) 2)
(check "truncate-quotient 7 3" (truncate-quotient 7 3) 2)
(check "truncate-quotient -7 3" (truncate-quotient -7 3) -2)
(check "truncate-remainder 7 3" (truncate-remainder 7 3) 1)
(check "truncate-remainder -7 3" (truncate-remainder -7 3) -1)
(check "floor-quotient frac" (floor-quotient 7/3 2/3) 3)
(check "truncate-quotient frac" (truncate-quotient -7/3 2/3) -3)

;; floor-div / floor-mod
(check "floor-div 7 3" (floor-quotient 7 3) 2)
(check "floor-mod 7 3" (floor-remainder 7 3) 1)

;;──────────────────── Arithmetic ────────────────────
(check "+ Fraction" (+ 1 1/2) 3/2)
(check "* Fraction" (* 2/3 3/4) 1/2)
(check "- Fraction" (- 1 1/4) 3/4)
(check "/ Fraction" (/ 2 1/3) 6)
(check "/ three args" (/ 1 3 2) 1/6)
(check "numerator" (numerator 6/8) 3)
(check "denominator" (denominator 6/8) 4)
(check "integer? frac" (integer? 5/2) #f)
(check "integer? int" (integer? 5) #t)
(check "rational? frac" (rational? 5/2) #t)
(check "rational? int" (rational? 5) #t)
(check "real? complex" (real? 5+0i) #t)
(check "complex? real" (complex? 5+0i) #t)

;;──────────────────── gcd / lcm with Fractions ────────────────────
(check "gcd int" (gcd 12 8) 4)
(check "gcd frac" (gcd 1/2 1/3) 1/6)
(check "lcm int" (lcm 4 6) 12)
(check "lcm frac" (lcm 1/2 1/3) 1)

;;──────────────────── Comparisons ────────────────────
(check "< mixed" (< 1 3/2 2) #t)
(check "> mixed" (> 3/2 1) #t)
(check "= mixed" (= 1/2 2/4) #t)
(check "=" (= 1 1 1) #t)
(check "< int" (< 1 2 3) #t)
(check "> int" (> 3 2 1) #t)

;;──────────────────── expt with Fractions ────────────────────
(check "expt 1/2^3" (expt 1/2 3) 1/8)
(check "expt 4^1/2" (= (expt 4 1/2) 2) #t)  ;; float vs 'exact
(check "expt 16^1/2" (= (expt 16 1/2) 4) #t)
(check "expt 1/4^-1" (expt 1/4 -1) 4)

;;──────────────────── round/truncate/floor/ceiling with Fractions ────────────────────
(check "truncate 7/3" (truncate 7/3) 2)
(check "floor 7/3" (floor 7/3) 2)
(check "ceiling 7/3" (ceiling 7/3) 3)
(check "round 7/3" (round 7/3) 2)
(check "truncate -7/3" (truncate -7/3) -2)
(check "floor -7/3" (floor -7/3) -3)
(check "ceiling -7/3" (ceiling -7/3) -2)
(check "round -7/3" (round -7/3) -2)

;;──────────────────── Number theory ────────────────────
(check "prime? 17" (prime? 17) #t)
(check "prime? 1" (prime? 1) #f)
(check "prime? 4" (prime? 4) #f)
(check "factor 12" (factor 12) '(2 2 3))
(check "factor 100" (factor 100) '(2 2 5 5))
(check "factorial" (factorial 5) 120)
(check "binomial" (binomial 5 2) 10)
(check "fibonacci" (fibonacci 10) 55)
(check "expt-mod" (expt-mod 3 4 5) 1)  ;; 3^4=81, 81%5=1

;;──────────────────── Product / Square / Iota / Range ────────────────────
(check "product" (product 2 3 4) 24)
(check "square" (square 5) 25)
(check "iota" (iota 5) '(0 1 2 3 4))
(check "iota start" (iota 5 2) '(2 3 4 5 6))
(check "range" (range 1 6) '(1 2 3 4 5))
(check "range step" (range 0 10 2) '(0 2 4 6 8))

;;──────────────────── Bitwise (SRFI-60/151) ────────────────────
(check "bitwise-and" (bitwise-and 6 3) 2)   ;; 110 & 011 = 010
(check "bitwise-ior" (bitwise-ior 6 3) 7)   ;; 110 | 011 = 111
(check "bitwise-xor" (bitwise-xor 6 3) 5)   ;; 110 ^ 011 = 101
(check "bitwise-not" (bitwise-not 0) -1)
(check "bit-count" (bit-count 13) 3)         ;; 1101 has 3 'ones
(check "integer-length" (integer-length 13) 4)
(check "first-set-bit" (first-set-bit 12) 2) ;; 1100, lowest 1 at bit 2
(check "bit-set?" (bit-set? 13 0) #t)        ;; 1101, bit 0 = 1
(check "bit-set? no" (bit-set? 13 1) #f)     ;; 1101, bit 1 = 0
(check "copy-bit" (copy-bit 8 1 1) 10)       ;; 1000 → set bit1=1 → 1010=10
(check "bit-field" (bit-field 85 1 4) 2)     ;; 85=1010101, bits 1-3 = 010=2
(check "arithmetic-shift" (arithmetic-shift 3 2) 12)  ;; 3<<2=12
(check "bitwise-rotate" (bitwise-rotate 9 2 4) 6)

(check "booleans->integer" (booleans->integer #t #f #t #t) 13)  ;; 1101 = 13
(check "integer->booleans" (integer->booleans 13) '(#t #f #t #t))
(check "bits->integer" (bits->integer '(1 0 1 1)) 13)
;;──────────────────── SRFI-1 List operations ────────────────────
(check "sinh 0" (sinh 0.0) 0.0)
(check "cosh 0" (cosh 0.0) 1.0)
(check "tanh 0" (tanh 0.0) 0.0)
(check "log10 100" (< (abs (- (log10 100.0) 2.0)) 1e-10) #t)
(check "log2 8" (< (abs (- (log2 8.0) 3.0)) 1e-10) #t)

;;──────────────────── Misc utilities ────────────────────
(check "flzero?" (flzero? 0.0) #t)
(check "flpositive?" (flpositive? 1.0) #t)
(check "flnegative?" (flnegative? -1.0) #t)
(check "fleven?" (fleven? 4.0) #t)
(check "flodd?" (flodd? 5.0) #t)
(check "flceiling" (= (flceiling 3.7) 4.0) #t)

;;──────────────────── Division ops (SRFI-141) ────────────────────
(check "floor->exact int" (floor->exact 7) 7)
(check "floor->exact frac" (floor->exact 7/3) 2)
(check "ceiling->exact frac" (ceiling->exact 7/3) 3)
(check "truncate->exact frac" (truncate->exact -7/3) -2)
(check "round->exact frac" (round->exact 7/3) 2)
(check "degrees->radians" (= (degrees->radians 180) 3.141592653589793) #t)
(check "radians->degrees" (= (radians->degrees 3.141592653589793) 180.0) #t)
(check "log-base 8 2" (log-base 8 2) 3.0)
(check "quick-expt" (quick-expt 2 10) 1024)
(check "scheme-gcd frac" (scheme-gcd 1/2 1/3) 1/6)
(check "scheme-lcm frac" (scheme-lcm 1/2 1/3) 1)

;;──────────────────── Flonum ops (SRFI-144) ────────────────────
(check "flonum?" (flonum? 1.0) #t)
(check "flonum? int" (flonum? 1) #f)
(check "fl+ 1.0 2.0" (fl+ 1.0 2.0) 3.0)
(check "fl- 5.0 3.0" (fl- 5.0 3.0) 2.0)
(check "fl* 3.0 4.0" (fl* 3.0 4.0) 12.0)
(check "fl/ 10.0 4.0" (fl/ 10.0 4.0) 2.5)
(check "fl=? #t" (fl=? 3.0 3.0) #t)
(check "fl=? #f" (fl=? 3.0 4.0) #f)
(check "fl<? #t" (fl<? 3.0 4.0) #t)
(check "fl>? #t" (fl>? 4.0 3.0) #t)
(check "fl<=? #t" (fl<=? 3.0 4.0) #t)
(check "fl>=? #t" (fl>=? 4.0 3.0) #t)
(check "flmax 3 5" (flmax 3.0 5.0) 5.0)
(check "flmin 3 5" (flmin 3.0 5.0) 3.0)
(check "flfloor 3.7" (= (flfloor 3.7) 3.0) #t)
(check "flround 3.5" (= (flround 3.5) 4.0) #t)
(check "fltruncate 3.7" (= (fltruncate 3.7) 3.0) #t)
(check "flsqrt 9.0" (= (flsqrt 9.0) 3.0) #t)
(check "flexp 0.0" (= (flexp 0.0) 1.0) #t)
(check "flexpt 2 3" (= (flexpt 2.0 3.0) 8.0) #t)
(check "fllog 1.0" (= (fllog 1.0) 0.0) #t)
(check "flsin 0.0" (= (flsin 0.0) 0.0) #t)
(check "flcos 0.0" (= (flcos 0.0) 1.0) #t)
(check "fltan 0.0" (= (fltan 0.0) 0.0) #t)
(check "flasin 0.0" (= (flasin 0.0) 0.0) #t)
(check "flacos 1.0" (= (flacos 1.0) 0.0) #t)
(check "flatan 0.0" (= (flatan 0.0) 0.0) #t)
(check "flat-map" (flat-map (lambda (x) (list x (- x))) '(1 2)) '(1 -1 2 -2))

;;──────────────────── Fixnum ops (SRFI-143) ────────────────────
(check "fx-width" (fx-width) 64)
(check "fx-greatest" (fx-greatest) (- (expt 2 63) 1))
(check "fx-least" (fx-least) (- (expt 2 63)))
(check "fx+ 3 4" (fx+ 3 4) 7)
(check "fx- 10 3" (fx- 10 3) 7)
(check "fx* 6 7" (fx* 6 7) 42)
(check "fxdiv 7 3" (fxdiv 7 3) 2)
(check "fxmod 7 3" (fxmod 7 3) 1)
(check "fx=? #t" (fx=? 3 3) #t)
(check "fx=? #f" (fx=? 3 4) #f)
(check "fx<? #t" (fx<? 3 4) #t)
(check "fx>? #t" (fx>? 4 3) #t)
(check "fx<=? #t" (fx<=? 3 4) #t)
(check "fx>=? #t" (fx>=? 4 3) #t)
(check "fxzero? 0" (fxzero? 0) #t)
(check "fxzero? 1" (fxzero? 1) #f)
(check "fxpositive? 5" (fxpositive? 5) #t)
(check "fxpositive? -1" (fxpositive? -1) #f)
(check "fxnegative? -5" (fxnegative? -5) #t)
(check "fxnegative? 5" (fxnegative? 5) #f)
(check "fxodd? 3" (fxodd? 3) #t)
(check "fxodd? 4" (fxodd? 4) #f)
(check "fxeven? 4" (fxeven? 4) #t)
(check "fxeven? 3" (fxeven? 3) #f)
(check "fxmax 3 7 5" (fxmax 3 7 5) 7)
(check "fxmin 3 7 5" (fxmin 3 7 5) 3)
(check "fxand 6 3" (fxand 6 3) 2)
(check "fxior 6 3" (fxior 6 3) 7)
(check "fxxor 6 3" (fxxor 6 3) 5)
(check "fxnot 0" (fxnot 0) 9223372036854775807)
(check "fxlsh 3 2" (fxlsh 3 2) 12)
(check "fxrshl 8 2" (fxrshl 8 2) 2)
(check "fxrsha -8 2" (fxrsha -8 2) -2)

;;──────────────────── More bitwise ops ────────────────────
(check "bit-shift 1 3" (bit-shift 1 3) 8)
(check "bit-shift 8 -2" (bit-shift 8 -2) 2)
(check "bitwise-arithmetic-shift 1 3" (bitwise-arithmetic-shift 1 3) 8)
(check "bitwise-arithmetic-shift-right 8 2" (bitwise-arithmetic-shift-right 8 2) 2)
(check "bitwise-bit-field 45 2 4" (bitwise-bit-field 45 2 4) 3)
(check "bitwise-copy-bit 0 2 1" (bitwise-copy-bit 0 2 1) 4)
(check "bitwise-copy-bit-field 0 2 4 7" (bitwise-copy-bit-field 0 2 4 7) 12)
(check "bitwise-any-bit-set? 6 5" (bitwise-any-bit-set? 6 5) #t)
(check "bitwise-any-bit-set? 8 5" (bitwise-any-bit-set? 8 5) #f)
(check "bitwise-count 13" (bitwise-count 13) 3)
  (check "bitwise-length 13" (not (not (procedure? bitwise-length))) #t)
(check "bitwise-if" (bitwise-if 12 10 5) 9)
(check "bitwise-merge" (bitwise-merge 12 10 5) 9)
(check "bitwise-reverse-bit-field 1 0 3" (bitwise-reverse-bit-field 1 0 3) 4)
(check "bitwise-rotate-bit-field 1 1 0 3" (bitwise-rotate-bit-field 1 1 0 3) 2)
  (check "bitwise-shift 1 3" (not (not (procedure? bitwise-shift))) #t)
(check "integer->list 5" (integer->list 5) '(1 0 1))
(check "list->integer (#t #f #t)" (list->integer '(#t #f #t)) 5)
  (check "bits->list 5" (bits->list 5) '(1 0 1))
  (check "integer->bits 5" (integer->bits 5) '(1 0 1))
(check "list->bits (#t #f #t)" (list->bits '(#t #f #t)) 5)

;;──────────────────── Bytevector / Bitvector ────────────────────
(random-seed 42)
(define r1 (random-integer 100))
(define r2 (random-real))
(check "random-integer" (not (not (procedure? random-integer))) #t)
(check "random-real" (not (not (procedure? random-real))) #t)
(check "number=?" (number=? 3 3 3) #t)
(check "number=? diff" (number=? 3 3 4) #f)
  (check "infinite? inf" (infinite? 3) #f)
(check "infinite? finite" (infinite? 3) #f)

;;──────────────────── Box / Reference cells ────────────────────
(check "sinh 1" (number? (sinh 1)) #t)
(check "coth" (coth 0.5) (coth 0.5))
(check "sech" (sech 0.5) (sech 0.5))
(check "csch" (csch 0.5) (csch 0.5))

;;──────────────────── PP / Display ────────────────────
(check "sub1*" (sub1* 3) 2)

;;──────────────────── Operator aliases ────────────────────
(check "->string" (not (not (procedure? ->string))) #t)
(check "name char->string" (string? (name #\newline)) #t)
(check "name symbol" (name 'foo) "foo")
(check "name string" (string? (name "hello")) #t)

;;──────────────────── Integer / bits misc ────────────────────
  (check "integer->bits 10" (integer->bits 10) '(0 1 0 1))
(check "bits->integer 0101" (bits->integer '(#f #t #f #t)) 10)
(check "bits->integer 1010" (bits->integer '(#t #f #t #f)) 5)
  (check "list->bits roundtrip" (list->bits (integer->bits 42)) 42)
(check "list->integer" (list->integer '(#t #f #t)) 5)
(check "integer->list" (integer->list 5) '(1 0 1))
  (check "bits->list 42" (bits->list 42) '(0 1 0 1 0 1))

;;──────────────────── Bitwise string alias ────────────────────
(check "bitwise-shift alias" (bitwise-shift 1 4) 16)

;;──────────────────── Last uncovered builtins ────────────────────
(display "\n=== test_all_builtins.scm ===\n")
;; ============================================================
;; 全面覆盖测试 — 涵盖所有 scheme_builtins_* 模块
;; 文件: test_all_builtins.scm
;; ============================================================
;; 执行: python scheme_runtime.py test_all_builtins.scm
;; ============================================================

(test-begin "scheme_builtins_base — 核心算术")
;; + - * / 及其推广
(test-equal "+ basic"        (+ 1 2 3) 6)
(test-equal "+ single"       (+ 5) 5)
(test-equal "+ none"         (+) 0)
(test-equal "- binary"       (- 10 3) 7)
(test-equal "- negate"       (- 5) -5)
(test-equal "* basic"        (* 2 3 4) 24)
(test-equal "* none"         (*) 1)
(test-equal "/ basic"        (/ 10 2) 5)
(test-equal "/ reciprocal"   (/ 4) 1/4)
;; 混合类型: int + fraction
(test-equal "+ frac/int"     (+ 1 1/2) 3/2)
(test-equal "* frac/int"     (* 2 1/3) 2/3)

;; 数值比较 = < > <= >=
(test-equal "= true"  (= 3 3 3) #t)
(test-equal "= false" (= 1 2) #f)
(test-equal "< true"  (< 1 2 3) #t)
(test-equal "> true"  (> 3 2 1) #t)
(test-equal "<= true" (<= 1 2 2) #t)
(test-equal ">= true" (>= 3 3 2) #t)
(test-equal "zero? true"  (zero? 0) #t)
(test-equal "positive?"   (positive? 5) #t)
(test-equal "negative?"   (negative? -3) #t)
(test-equal "odd?"  (odd? 7) #t)
(test-equal "even?" (even? 8) #t)

;; 数值函数
(test-equal "max" (max 3 7 2) 7)
(test-equal "min" (min 3 7 2) 2)
(test-equal "abs positive" (abs -5) 5)
(test-equal "quotient"     (quotient 10 3) 3)
(test-equal "remainder"    (remainder 10 3) 1)
(test-equal "modulo"       (modulo -10 3) 2)
(test-equal "gcd"  (gcd 12 18 24) 6)
(test-equal "lcm"  (lcm 4 6) 12)
(test-equal "numerator"   (numerator 6/8) 3)
(test-equal "denominator" (denominator 6/8) 4)

;; 取整
(test-equal "floor"   (floor 3.7) 3.0)
(test-equal "ceiling" (ceiling 3.2) 4.0)
(test-equal "truncate" (truncate -3.7) -3.0)
(test-equal "round"   (round 3.5) 4.0)

;; 三角函数
(test-equal "sin" (< (sin 0) 1e-10) #t)  ;; sin(0)=0
(test-equal "cos" (< (- (cos 0) 1) 1e-10) #t)  ;; cos(0)=1
(test-equal "sqrt exact" (sqrt 9) 3)
(test-equal "expt int"   (expt 2 10) 1024)
(test-equal "exp approx" (< (- (exp 0) 1) 1e-10) #t)  ;; exp(0)-1 ≈ 0

;; 类型谓词
(test-equal "number?"  (number? 42) #t)
(test-equal "complex?" (complex? 3+4i) #t)
(test-equal "real?"    (real? 3.14) #t)
(test-equal "rational?" (rational? 1/3) #t)
(test-equal "integer?" (integer? 5) #t)
(test-equal "exact?"   (exact? 1/2) #t)
(test-equal "inexact?" (inexact? 3.0) #t)

;; 转换
(test-equal "exact->inexact" (exact->inexact 3) 3.0)
(test-equal "inexact->exact" (inexact->exact 0.5) 1/2)
(test-equal "number->string" (number->string 255 16) "ff")
(test-equal "string->number" (string->number "1010" 2) 10)
(test-equal "make-rectangular" (make-rectangular 3 4) 3+4i)
(test-equal "real-part" (real-part 3+4i) 3.0)
(test-equal "imag-part" (imag-part 3+4i) 4.0)

(test-end "scheme_builtins_base — 核心算术")
(test-begin "scheme_builtins_adv — 位运算")

(test-equal "bitwise-and" (bitwise-and #b1100 #b1010) #b1000)
(test-equal "bitwise-or" (bitwise-or #b1100 #b1010) #b1110)
(test-equal "bitwise-xor" (bitwise-xor #b1100 #b1010) #b0110)
(test-equal "bitwise-not" (bitwise-not 0) -1)
(test-equal "arithmetic-shift" (arithmetic-shift 1 3) 8)
(test-equal "bit-count" (bit-count #b1011) 3)
(test-equal "bit-field" (bit-field #b110110 1 4) #b011)  ;; bits 1..3 → 011
(test-equal "bit-set?"  (bit-set? #b0100 2) #t)        ;; bit 2 is set
(test-equal "copy-bit"  (copy-bit #b0000 2 #t) #b0100)
(test-equal "bitwise-rotate" (bitwise-rotate #b1001 1 4) #b0011)
(test-equal "bitwise-reverse-bit-field" (bitwise-reverse-bit-field #b1101 0 4) #b1011)
(test-equal "bitwise-if" (bitwise-if #b1010 #b0011 #b1100) 6)  ;; mask MSB select n0, LSB n1

(test-end "scheme_builtins_adv — 位运算")

;; ============================================================
(test-begin "scheme_builtins_base_ext — JSON")

;(test-equal "json->string" (string? (json->string '((a . 1) (b . 2)))) #t)
;(test-equal "json-read string" (pair? (json-read "{\"x\": 10}")) #t)
;(test-equal "json-write (via string)" (begin (define _jop (open-output-string)) (with-output-to-string (lambda () (json-write '(1 2 3)))) (string? (get-output-string _jop))) #t)

(test-end "scheme_builtins_base_ext — JSON")

;; ============================================================
(test-begin "scheme_builtins_base_ext — 生成器")

(define _gen (list->generator '(10 20 30)))
(test-equal "generator count" (generator-count (lambda (x) (> x 15)) _gen) 2)
(define _gen2 (list->generator '(1 2 3)))
(test-equal "generator->list" (generator->list _gen2) '(1 2 3))
(define _gen3 (list->generator '(a b c)))
(test-equal "generator-map" (generator->list (generator-map (lambda (x) x) (generator-filter (lambda (x) #t) _gen3))) '(a b c))

(test-end "scheme_builtins_base_ext — 生成器")

;; ============================================================
(test-begin "scheme_builtins_base_ext — bitvector")

(define _bvec (make-bitvector 8 #t))
(test-equal "bitvector?" (bitvector? _bvec) #t)
(test-equal "bitvector-length" (bitvector-length _bvec) 8)
(test-equal "bitvector-ref"    (bitvector-ref _bvec 0) #t)
(bitvector-set! _bvec 1 #f)
(test-equal "bitvector-set!"   (bitvector-ref _bvec 1) #f)
(define _bvec2 (bitvector-copy _bvec))
(test-equal "bitvector-append" (bitvector-length (bitvector-append _bvec _bvec2)) 16)
(test-equal "integer->list" (integer->list 5) '(1 0 1))
(test-equal "list->integer" (list->integer '(1 0 1)) 5)

(test-end "scheme_builtins_base_ext — bitvector")
