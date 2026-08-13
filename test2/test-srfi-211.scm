;;; Test file for SRFI-211
(import (srfi 211))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "set-at" '(1 99 3) (set-at (list 1 2 3) 1 99))
