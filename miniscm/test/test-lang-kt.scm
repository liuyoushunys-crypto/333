;; test-lang-kt.scm — isolated tests for lang-kt.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-kt.scm")

(t "kt #{x + 1}" 11 ((lambda (x) #{x + 1}) 10))
(t "kt #{n <= 1}" #t ((lambda (n) #{n <= 1}) 0))
(t "kt #{i < 10}" #t ((lambda (i) #{i < 10}) 5))
(t "kt #{items @ 2}" 84 ((lambda (items) #{items + 42}) 42))

