;;; Test file for SRFI-17
(import (srfi 17))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define x (list 1 2 3))
(set! (car x) 99)
(%test-equal "set! car" 99 (car x))
