;;; Test file for SRFI-161
(import (srfi 161))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define b (make-unifiable-box 42))
(%test-equal "unifiable-box?" #t (unifiable-box? b))
