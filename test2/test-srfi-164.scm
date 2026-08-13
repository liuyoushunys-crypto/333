;;; Test file for SRFI-164
(import (srfi 164))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "call-with-values" '(1 2) (call-with-values (lambda () (values 1 2)) list))
