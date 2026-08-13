;; test-lang-java.scm — isolated tests for lang-java.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-java.scm")

(t "java (/ (* n (+ n 1)) 2)" 55 ((lambda (n) (/ (* n (+ n 1)) 2)) 10))
(t "java (< i 10)" #t ((lambda (i) (< i 10)) 5))
(t "java (+ x 1)" 7 ((lambda (x) (+ x 1)) 6))
