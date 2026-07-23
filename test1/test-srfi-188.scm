;;; Test file for SRFI-188
(import (srfi 188))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "compose" 12 ((compose (lambda (x) (* x 2)) (lambda (x) (+ x 1))) 5))
(%test-equal "curry" 12 ((curry + 5) 7))
