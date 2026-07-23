;;; Test file for SRFI-11
(import (srfi 11))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "let-values" 3 (let-values (((a b) (values 1 2))) (+ a b)))
(%test-equal "let*-values" 1 (let*-values (((a b) (values 1 2))) a))
