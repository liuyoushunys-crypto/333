;;; Test file for SRFI-154
(import (srfi 154))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "dynamic-wind" 42 (dynamic-wind (lambda ()) (lambda () 42) (lambda ())))
