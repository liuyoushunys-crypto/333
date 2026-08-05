;; test-lang-ts.scm — isolated tests for lang-ts.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-ts.scm")

(t "ts #{x + 1}" 6 ((lambda (x) #{x + 1}) 5))
(t "ts #{n <= 1}" #t ((lambda (n) #{n <= 1}) 0))
(t "ts #{i < n}" #t ((lambda (i n) #{i < n}) 3 10))

