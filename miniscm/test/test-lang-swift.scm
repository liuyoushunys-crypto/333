;; test-lang-swift.scm — isolated tests for lang-swift.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-swift.scm")

(t "sw #{n * (n + 1) / 2}" 55 ((lambda (n) #{n * (n + 1) / 2}) 10))
(t "sw #{x + 1}" 43 ((lambda (x) #{x + 1}) 42))
(t "sw #{a * b + c}" 23 ((lambda (a b c) #{a * b + c}) 2 3 17))

