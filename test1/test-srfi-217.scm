;;; Test file for SRFI-217
(import (srfi 217))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "integer-set?" #t (integer-set? (make-integer-set 1 2 3)))
