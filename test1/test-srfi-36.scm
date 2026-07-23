;;; Test file for SRFI-36
(import (srfi 36))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "condition?" #t (condition? (make-io-error "test")))
(%test-equal "io-error?" #t (io-error? (make-io-error "test")))
