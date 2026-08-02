;;; Test file for SRFI-19
(import (srfi 19))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "current-date" #t (date? (current-date)))
(%test-equal "current-time" #t (time? (current-time)))
