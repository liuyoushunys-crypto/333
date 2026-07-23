;;; Test file for SRFI-246
(import (srfi 246))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "int-vector?" #t (int-vector? (int-vector 1 2 3)))
