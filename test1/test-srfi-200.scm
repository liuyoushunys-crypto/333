;;; Test file for SRFI-200
(import (srfi 200))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "sorted-by" (1 2 3) (sorted-by < '(3 1 2)))
