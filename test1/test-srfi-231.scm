;;; Test file for SRFI-231
(import (srfi 231))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "group-by" '((0 2) (1 3)) (group-by even? '(0 1 2 3)))
