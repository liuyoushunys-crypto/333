;;; Test file for SRFI-182
(import (srfi 182))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "lambda*" 12 ((lambda* ((x 1) (y 2)) (+ x y)) 5 7))
