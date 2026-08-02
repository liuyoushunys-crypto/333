;;; Test file for SRFI-66
(import (srfi 66))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "u8vector-length" 3 (u8vector-length (u8vector 1 2 3)))
