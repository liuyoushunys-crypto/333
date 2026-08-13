;;; Test file for SRFI-67
(import (srfi 67))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "integer-compare" -1 (integer-compare 1 2))
(%test-equal "integer-compare eq" 0 (integer-compare 3 3))
