;;; Test file for SRFI-221
(import (srfi 221))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "generator->list" '(0 1 2) (generator->list (make-iota-generator 3)))
