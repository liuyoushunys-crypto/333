;;; Test file for SRFI-0
(import (srfi 0))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "cond-expand r5rs" #t (cond-expand (r5rs #t) (else #f)))
(%test-equal "cond-expand else" 42 (cond-expand (else 42)))
