;;; Test file for SRFI-158
(import (srfi 158))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "generator->list" '(0 1 2 3 4) (generator->list (make-iota-generator 5)))
(%test-equal "generator->list range" '(10 11 12) (generator->list (make-range-generator 10 13)))
