;;; Test file for SRFI-25
(import (srfi 25))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define a (array (shape 0 3) 1 2 3))
(%test-equal "array?" #t (array? a))
(%test-equal "array-rank" 1 (array-rank a))
(%test-equal "array-ref" 2 (array-ref a 1))
