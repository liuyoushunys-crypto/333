;; test-lang-hs.scm — isolated tests for lang-hs.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-hs.scm")

(t "hs (+ x 1)" 43 ((lambda (x) (+ x 1)) 42))
(t "hs (* n 2)" 20 ((lambda (n) (* n 2)) 10))
(t "hs (+ (* x 2) 1)" 9 ((lambda (x) (+ (* x 2) 1)) 4))
