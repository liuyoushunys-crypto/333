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
