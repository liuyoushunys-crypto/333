;;; Test file for SRFI-123
(import (srfi 123))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define x (list 1 2 3))
(%test-equal "generic-ref" 1 (generic-ref x 0))
