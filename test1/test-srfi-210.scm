;;; Test file for SRFI-210
(import (srfi 210))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "clamp" 5 (clamp 3 5 10))
(%test-equal "clamp low" 5 (clamp 1 5 10))
