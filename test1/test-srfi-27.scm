;;; Test file for SRFI-27
(import (srfi 27))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "random-integer" #t (>= (random-integer 100) 0))
(%test-equal "random-real" #t (>= (random-real) 0.0))
