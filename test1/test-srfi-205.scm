;;; Test file for SRFI-205
(import (srfi 205))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "values" 3 (call-with-values (lambda () (values 1 2)) (lambda (a b) (+ a b))))
