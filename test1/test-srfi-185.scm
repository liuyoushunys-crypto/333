;;; Test file for SRFI-185
(import (srfi 185))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "update" (1 2 4) (update (list 1 2 3) 2 (lambda (x) (+ x 1))))
