;; test-char-set.scm — Char-sets: SRFI-14 ops, any, every, fold, map
;; Generated from merged test suites

(define cs (char-set #\a #\b #\c))
(check "char-set?" (char-set? cs) #t)
(check "char-set-contains?" (char-set-contains? cs #\b) #t)
(check "char-set?" (char-set? cs) #t)
(check "char-set-contains? no" (char-set-contains? cs #\z) #f)
(check "char-set->list" (length (char-set->list cs)) 3)
(check "char-set->string" (string-length (char-set->string cs)) 3)
(check "char-set-count" (char-set-count cs) 3)
(check "char-set-empty?" (char-set-empty? (char-set)) #t)
(check "char-set-adjoin" (char-set-contains? (char-set-adjoin cs #\d) #\d) #t)
(check "char-set-delete" (char-set-contains? (char-set-delete cs #\a) #\a) #f)
(define cs2 (char-set-union (char-set #\a #\b) (char-set #\b #\c)))
(check "char-set-union count" (char-set-count cs2) 3)
(check "char-set-intersection count" (char-set-count (char-set-intersection cs (char-set #\b #\c #\d))) 2)
(check "char-set-complement size" (char-set-count (char-set-complement (char-set))) 256)

;;──────────────────── Comparators (SRFI-128) ────────────────────
(define cs-abc (string->char-set "abc"))
(define cs-def (string->char-set "def"))
(check "char-set-copy" (char-set? (char-set-copy cs-abc)) #t)
(check "char-set-difference" (char-set->string (char-set-difference cs-abc cs-def)) "abc")
(check "char-set-xor size" (char-set-count (char-set-xor cs-abc cs-def)) 6)
(check "char-set-filter" (char-set->string (char-set-filter char-lower-case? (char-set-union cs-abc cs-def))) "abcdef")
(check "char-set-fold" (procedure? char-set-fold) #t)
(check "char-set-for-each" (let ((n 0)) (char-set-for-each (lambda (c) (set! n (+ n 1))) cs-abc) n) 3)
(check "char-set-map" (not (not (procedure? char-set-map))) #t)
(check "char-set-any" (not (not (procedure? char-set-any))) #t)
(check "char-set-any not" (char-set-any (lambda (c) (char=? c #\z)) cs-abc) #f)
(check "char-set-every" (char-set-every char-lower-case? cs-abc) #t)
(check "char-set-every not" (char-set-every char-lower-case? (string->char-set "aBc")) #f)
(check "char-set-=" (char-set=? cs-abc (string->char-set "cba")) #t)

;;──────────────────── Comparators (SRFI-128) ────────────────────
;; ============================================================
(test-begin "scheme_builtins_base_ext — 集合（char-set）")

(define _cs (char-set #\a #\b #\c))
(test-equal "char-set?"       (char-set? _cs) #t)
(test-equal "char-set-contains?" (char-set-contains? _cs #\b) #t)
(test-equal "char-set-contains? no" (char-set-contains? _cs #\z) #f)
(test-equal "char-set->list"  (length (char-set->list _cs)) 3)
(test-equal "char-set-adjoin" (char-set-contains? (char-set-adjoin _cs #\d) #\d) #t)
(test-equal "char-set-delete" (char-set-contains? (char-set-delete _cs #\b) #\b) #f)
(test-equal "char-set-empty?" (char-set-empty? (char-set)) #t)
(test-equal "char-set-union"  (char-set-contains? (char-set-union _cs (char-set #\d)) #\d) #t)
(test-equal "char-set-intersection" (char-set-contains? (char-set-intersection _cs (char-set #\a #\c)) #\b) #f)
(test-equal "char-set-difference" (char-set-contains? (char-set-difference _cs (char-set #\b)) #\b) #f)
(test-equal "char-set-complement" (char-set-contains? (char-set-complement _cs) #\z) #t)

(test-end "scheme_builtins_base_ext — 集合")

;; test-types.scm — merged test file
;; Char-set and number type tests


(display "\n=== test-char-set.scm ===\n")
;; test-char-set.scm — Scheme char-set test suite
;; Tests lines 1046-1081 of scheme/base.py (install_ext)
;; Run: python allinone.py test-char-set.scm
;; Note: install_ext must be enabled (normally dead code)

(import (scheme base))

(define-syntax test
  (syntax-rules ()
    ((_ expected expr)
     (test-equal 'expr expected expr))))

;; --- Type / creation ---

    (test-begin)
(test #t (char-set? (char-set #\a #\b #\c)))
(test #f (char-set? 42))
(test #t (char-set-empty? (char-set)))
(test #f (char-set-empty? (char-set #\a)))

;; --- Containment ---

(test #t (char-set-contains? (char-set #\a #\b) #\a))
(test #f (char-set-contains? (char-set #\a #\b) #\c))

;; --- Conversion ---

(test 3 (length (char-set->list (char-set #\a #\b #\c))))
(test 0 (length (char-set->list (char-set))))
(test 3 (string-length (char-set->string (char-set #\a #\b #\c))))

;; --- Count ---

(test 3 (char-set-count (char-set #\a #\b #\c)))
(test 0 (char-set-count (char-set)))

;; --- Mutation-like ---

(test #t (char-set-contains? (char-set-adjoin (char-set #\a) #\b) #\b))
(test #f (char-set-contains? (char-set-delete (char-set #\a #\b) #\b) #\b))

;; --- Set operations ---

(test #t (char-set-contains? (char-set-union (char-set #\a) (char-set #\b)) #\b))
(test #t (char-set-empty? (char-set-intersection (char-set #\a) (char-set #\b))))
(test #t (char-set-contains? (char-set-difference (char-set #\a #\b) (char-set #\b)) #\a))
(test #f (char-set-contains? (char-set-difference (char-set #\a #\b) (char-set #\b)) #\b))
(test #t (char-set-contains? (char-set-complement (char-set #\a)) #\b))

;; --- XOR ---

(test #t (char-set-contains? (char-set-xor (char-set #\a) (char-set #\b)) #\a))
(test 1 (char-set-count (char-set-xor (char-set #\a #\b) (char-set #\b))))

;; --- Equality ---

(test #t (char-set=? (char-set #\a #\b) (char-set #\b #\a)))
(test #f (char-set=? (char-set #\a) (char-set #\b)))

;; --- Copy ---

(test #t (char-set-contains? (char-set-copy (char-set #\x)) #\x))
(let ((cs (char-set #\a)))
  (char-set-delete (char-set-copy cs) #\a)
  (test #t (char-set-contains? cs #\a)))

;; --- String / range construction ---

(test #t (char-set-contains? (string->char-set "abc") #\a))
(test #f (char-set-contains? (string->char-set "abc") #\d))
(test 3 (char-set-count (string->char-set "abc")))
(test #t (char-set-empty? (string->char-set "")))

(test #t (char-set-contains? (ucs-range->char-set 65 67) #\A))
(test #f (char-set-contains? (ucs-range->char-set 65 67) #\C))
(test #t (char-set-empty? (ucs-range->char-set 0 0)))

;; --- Any / Every ---

(test #\a (char-set-any (lambda (c) (char=? c #\a)) (char-set #\a #\b)))
(test #f (char-set-any (lambda (c) (char=? c #\z)) (char-set #\a #\b)))
(test #t (char-set-every (lambda (c) #t) (char-set #\a #\b)))
(test #f (char-set-every (lambda (c) (char=? c #\a)) (char-set #\a #\b)))

;; --- Filter / Map ---

(test 2 (char-set-count (char-set-filter (lambda (c) #t) (char-set #\a #\b))))
(test 0 (char-set-count (char-set-filter (lambda (c) #f) (char-set #\a #\b))))
(test 256 (char-set-count (char-set-map (lambda (c) #t) (char-set #\a))))

;; --- Fold ---

(test 3 (char-set-fold (lambda (n c) (+ n 1)) 0 (char-set #\a #\b #\c)))
(test 0 (char-set-fold (lambda (n c) (+ n 1)) 0 (char-set)))

;; --- Hash ---

(test #t (integer? (char-set-hash (char-set #\a))))
(test #t (integer? (char-set-hash (char-set #\a #\b #\c))))

;; --- For-each ---

(test #t (begin (char-set-for-each (lambda (c) #f) (char-set #\a #\b)) #t))

    (test-end)


(display "\n=== test-number-types.scm ===\n")
;; number-type-smoke-test — Fraction/complex correctness

(load "test/assert.scm")

;; ── helpers ──
(define (approx= a b)
  (< (abs (- a b)) 1e-10))

;; ── SRFI-141 division with Fractions ──
(assert (= (floor/ 7 3) 2 1)       "(floor/ 7 3)")
(assert (= (floor/ -7 3) -3 2)     "(floor/ -7 3)")
(assert (= (floor/ 7 -3) -3 -2)    "(floor/ 7 -3)")
(assert (= (floor/ -7 -3) 2 -1)    "(floor/ -7 -3)")

(assert (= (truncate/ 7 3) 2 1)    "(truncate/ 7 3)")
(assert (= (truncate/ -7 3) -2 -1) "(truncate/ -7 3)")

(assert (= (ceiling/ 7 3) 3 -2)    "(ceiling/ 7 3)")
(assert (= (ceiling/ -7 3) -2 -1)  "(ceiling/ -7 3)")

(assert (= (round/ 5 3) 2 -1)      "(round/ 5 3)")
(assert (= (round/ 7 3) 2 1)       "(round/ 7 3)")

(assert (= (euclidean/ 7 3) 2 1)   "(euclidean/ 7 3)")
(assert (= (euclidean/ -7 3) -3 2)  "(euclidean/ -7 3)")

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



(display "\n=== test-unicode.scm ===\n")
;; test-unicode.scm — merged test file
