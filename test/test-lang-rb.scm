;; test-lang-rb.scm — isolated tests for lang-rb.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-rb.scm")

(t "rb #{x + 1}" 4 ((lambda (x) #{x + 1}) 3))
(t "rb #{n <= 1}" #f ((lambda (n) #{n <= 1}) 7))
(t "rb #{x * 2}" 84 ((lambda (x) #{x * 2}) 42))

