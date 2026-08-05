;;; Test file for SRFI-87
(import (srfi 87))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "case =>" 42 (case 3 ((1 2 3) => (lambda (x) (* x 14))) (else #f)))
