;;; Test file for SRFI-78
(import (srfi 78))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(check (even? 10) #t)
(%test-equal "check" 'done 'done)
