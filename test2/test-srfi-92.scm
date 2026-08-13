;;; Test file for SRFI-92
(import (srfi 92))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "exact-integer?" #t (exact-integer? 42))
(%test-equal "square" 25 (square 5))
