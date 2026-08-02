;;; Test file for SRFI-173
(import (srfi 173))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define h (make-hook))
(%test-equal "hook?" #t (hook? h))
