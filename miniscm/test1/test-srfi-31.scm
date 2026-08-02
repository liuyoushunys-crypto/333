;;; Test file for SRFI-31
(import (srfi 31))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "rec factorial" 120 ((rec (fact n) (if (zero? n) 1 (* n (fact (- n 1))))) 5))
