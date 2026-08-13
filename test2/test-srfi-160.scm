;;; Test file for SRFI-160
(import (srfi 160))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "f64vector" #(1.0 2.0) (f64vector 1.0 2.0))
