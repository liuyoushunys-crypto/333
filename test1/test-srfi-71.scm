;;; Test file for SRFI-71
(import (srfi 71))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "let-values" 3 (let (((values a b) (values 1 2))) (+ a b)))
