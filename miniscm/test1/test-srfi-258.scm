;;; Test file for SRFI-258
(import (srfi 258))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "enum-set?" #t (enum-set? (make-enum-set '(a b c) '(a c))))
