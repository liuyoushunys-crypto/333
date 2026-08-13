;; test-misc-edge.scm
;; Edge-case tests for untested functions in scm/misc.scm

(define (t-eq label expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(display "=== arithmetic-shift-right ===\n")
(t-eq "ashr simple" 4 (arithmetic-shift-right 16 2))
(t-eq "ashr by 0" 42 (arithmetic-shift-right 42 0))
(t-eq "ashr negative" -4 (arithmetic-shift-right -16 2))
(t-eq "ashr large" 32 (arithmetic-shift-right 1024 5))
(t-eq "ashr beyond zero" 0 (arithmetic-shift-right 1 10))

(display "=== char-set:empty ===\n")
(t-eq "empty contains nothing" #f (char-set-contains? char-set:empty #\a))
(t-eq "empty size" 0 (char-set-count char-set:empty))

(display "=== char-set:full ===\n")
(t-eq "full contains a" #t (char-set-contains? char-set:full #\a))
(t-eq "full contains nul" #t (char-set-contains? char-set:full #\nul))

(display "=== char-set:symbol ===\n")
(t-eq "symbol contains !" #t (char-set-contains? char-set:symbol #\!))
(t-eq "symbol not a" #f (char-set-contains? char-set:symbol #\a))

(display "=== char-set:hex-digit ===\n")
(t-eq "hex-digit contains A" #t (char-set-contains? char-set:hex-digit #\A))
(t-eq "hex-digit not G" #f (char-set-contains? char-set:hex-digit #\G))

(display "=== char-set:blank ===\n")
(t-eq "blank contains space" #t (char-set-contains? char-set:blank #\space))
(t-eq "blank not newline" #f (char-set-contains? char-set:blank #\newline))

(display "=== char-set:iso-control ===\n")
(t-eq "iso-control contains nul" #t (char-set-contains? char-set:iso-control #\nul))
(t-eq "iso-control not a" #f (char-set-contains? char-set:iso-control #\a))

(display "=== vector-cumulate ===\n")
(t-eq "vc sum" '#(1 3 6 10) (vector-cumulate + 0 '#(1 2 3 4)))
(t-eq "vc empty" '#() (vector-cumulate + 0 '#()))

(display "=== vector-index-right ===\n")
(t-eq "vir found" 3 (vector-index-right (λ (x) (> x 4)) '#(1 5 3 7 2)))
(t-eq "vir not found" #f (vector-index-right (λ (x) (> x 10)) '#(1 2 3)))

(display "=== vector-skip-right ===\n")
(t-eq "vsr found" 4 (vector-skip-right (λ (x) (< x 3)) '#(1 2 3 4 5)))
(t-eq "vsr skip all" #f (vector-skip-right (λ (x) #t) '#(1 2 3)))

(display "=== vector-append-subvectors ===\n")
(t-eq "vas simple" '#(1 2 3 10 20) (vector-append-subvectors '#(1 2 3 4 5) 0 3 '#(10 20) 0 2))
(t-eq "vas empty" '#() (vector-append-subvectors '#(a b) 0 0 '#(c d) 1 1))
(t-eq "vas three" '#(1 2 3) (vector-append-subvectors '#(1) 0 1 '#(2) 0 1 '#(3) 0 1))

(display "\n;; === All misc edge tests complete ===\n")
