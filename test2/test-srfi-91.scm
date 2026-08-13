;;; Test file for SRFI-91
(import (srfi 91))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "floating-point-pi" #t (> (floating-point-pi) 3.14))
(%test-equal "floating-point-e" #t (> (floating-point-e) 2.71))
