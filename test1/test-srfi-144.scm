;;; Test file for SRFI-144
(import (srfi 144))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "flonum?" #t (flonum? 1.5))
(%test-equal "fl+" 3.5 (fl+ 1.5 2.0))
