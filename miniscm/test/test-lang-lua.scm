;; test-lang-lua.scm — isolated tests for lang-lua.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-lua.scm")

(t "lua #{x + 1}" 6 ((lambda (x) #{x + 1}) 5))
(t "lua #{n <= 1}" #f ((lambda (n) #{n <= 1}) 3))
(t "lua #{i + 1}" 101 ((lambda (i) #{i + 1}) 100))

