;;; Test file for SRFI-16
(import (srfi 16))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define f (case-lambda ((x) (* x 2)) ((x y) (+ x y)) (else -1)))
(%test-equal "case-lambda 1" 6 (f 3))
(%test-equal "case-lambda 2" 5 (f 2 3))
