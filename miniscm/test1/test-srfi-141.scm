;;; Test file for SRFI-141
(import (srfi 141))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "floor-quotient" 2 (floor-quotient 7 3))
(%test-equal "floor-remainder" 1 (floor-remainder 7 3))
