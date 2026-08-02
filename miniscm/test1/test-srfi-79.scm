;;; Test file for SRFI-79
(import (srfi 79))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "make-color" #t (color? (make-color 1.0 0.0 0.0)))
(%test-equal "color-red" 1.0 (color-red red))
