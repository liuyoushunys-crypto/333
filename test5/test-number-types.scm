;; number-type-smoke-test — Fraction/complex correctness

;; Standalone assertions: use call-with-values for SRFI-141 two-value results.
(define (assert condition message)
  (if condition #t (error message)))

;; ── helpers ──
(define (approx= a b)
  (< (abs (- a b)) 1e-10))

;; ── SRFI-141 division with Fractions ──
(define (check-div thunk q r) (assert (call-with-values thunk (lambda (a b) (and (= a q) (= b r)))) "division"))
(check-div (lambda () (floor/ 7 3)) 2 1)
(check-div (lambda () (floor/ -7 3)) -3 2)
(check-div (lambda () (floor/ 7 -3)) -3 -2)
(check-div (lambda () (floor/ -7 -3)) 2 -1)

(check-div (lambda () (truncate/ 7 3)) 2 1)
(check-div (lambda () (truncate/ -7 3)) -2 -1)

(check-div (lambda () (ceiling/ 7 3)) 3 -2)
(check-div (lambda () (ceiling/ -7 3)) -2 -1)

(check-div (lambda () (round/ 5 3)) 2 -1)
(check-div (lambda () (round/ 7 3)) 2 1)

(check-div (lambda () (euclidean/ 7 3)) 2 1)
(check-div (lambda () (euclidean/ -7 3)) -3 2)

;; ── Fraction / Rational arithmetic ──
(assert (= (/ 1 3 2) 1/6)          "(/ 1 3 2)")

(assert (= (+ 1/2 1/3) 5/6)        "(+ 1/2 1/3)")
(assert (= (- 3/4 1/4) 1/2)        "(- 3/4 1/4)")
(assert (= (* 2/3 3/4) 1/2)        "(* 2/3 3/4)")
(assert (= (/ 1/2 3/4) 2/3)        "(/ 1/2 3/4)")

(assert (= (numerator 6/8) 3)      "(numerator 6/8)")
(assert (= (denominator 6/8) 4)    "(denominator 6/8)")

;; ── gcd / lcm with Fractions ──
(assert (= (gcd 1/2 1/3) 1/6)      "(gcd 1/2 1/3) — gcd of fractions")
(assert (= (lcm 1/2 1/3) 1)        "(lcm 1/2 1/3) — lcm of fractions")

;; ── integer? / rational? / real? for mixed types ──
(assert (integer? 5)               "(integer? 5)")
(assert (not (integer? 5/2))       "(not (integer? 5/2))")
(assert (rational? 5/2)            "(rational? 5/2)")
(assert (rational? 5)              "(rational? 5)")
(assert (real? 5+0i)               "(real? 5+0i)")
(assert (complex? 5+0i)            "(complex? 5+0i)")
(assert (real? 5)                  "(real? 5)")

;; ── comparisons with mixed types ──
(assert (< 1 3/2 2)               "(< 1 3/2 2)")
(assert (< 1/4 1/2)               "(< 1/4 1/2)")
(assert (> 3/2 1)                 "(> 3/2 1)")
(assert (= 1/2 2/4)               "(= 1/2 2/4)")

;; ── truncate / floor / ceiling / round with Fractions ──
(assert (= (truncate 7/3) 2)
        "(truncate 7/3)")
(assert (= (floor 7/3) 2)
        "(floor 7/3)")
(assert (= (ceiling 7/3) 3)
        "(ceiling 7/3)")
(assert (= (round 7/3) 2)
        "(round 7/3)")

(assert (= (truncate -7/3) -2)
        "(truncate -7/3)")
(assert (= (floor -7/3) -3)
        "(floor -7/3)")
(assert (= (ceiling -7/3) -2)
        "(ceiling -7/3)")
(assert (= (round -7/3) -2)
        "(round -7/3)")

;; ── expt with Fractions ──
(assert (= (expt 1/2 3) 1/8)       "(expt 1/2 3)")
(assert (= (expt 4 1/2) 2)         "(expt 4 1/2)")
(assert (= (expt 16 1/2) 4)        "(expt 16 1/2)")
(assert (= (expt 1/4 -1) 4)        "(expt 1/4 -1)")

;; ── Product aggregate ──
(assert (= (apply * (list 1/2 2/3 3/4)) 1/4)
        "(apply * (1/2 2/3 3/4))")
(assert (= (apply + (list 1/2 1/3 1/6)) 1)
        "(apply + (1/2 1/3 1/6))")

;; ── Mixed int/Fraction arithmetic ──
(assert (= (+ 1 1/2) 3/2)          "(+ 1 1/2)")
(assert (= (* 2 1/3) 2/3)          "(* 2 1/3)")
(assert (= (- 1 1/4) 3/4)          "(- 1 1/4)")
(assert (= (/ 2 1/3) 6)            "(/ 2 1/3)")

;; ── Flonum tests (should not raise) ──
(assert (flzero? 0.0)              "(flzero? 0.0)")
(assert (flpositive? 1.0)          "(flpositive? 1.0)")
(assert (flnegative? -1.0)         "(flnegative? -1.0)")
(assert (fleven? 4.0)              "(fleven? 4.0)")
(assert (flodd? 5.0)               "(flodd? 5.0)")
(assert (approx= (flexp 0.0) 1.0)  "(flexp 0.0)")
(assert (approx= (flsqrt 4.0) 2.0) "(flsqrt 4.0)")

;; ── Sin/cos pi/4 ──
(define sqrt2/2 (/ (sqrt 2) 2))
(assert (approx= (sin (/ pi 4)) sqrt2/2)
        "(sin pi/4)")
(assert (approx= (cos (/ pi 4)) sqrt2/2)
        "(cos pi/4)")

(display "\nall number-type tests passed\n")
