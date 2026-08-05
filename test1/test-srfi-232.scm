;;; Test file for SRFI-232
(import (srfi 232))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "flex-vector-ref" 42 (flex-vector-ref (flex-vector 10 42 30) 1))
