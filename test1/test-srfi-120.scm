;;; Test file for SRFI-120
(import (srfi 120))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "make-timer" #t (timer? (make-timer "test" 1.0 (lambda () #t))))
