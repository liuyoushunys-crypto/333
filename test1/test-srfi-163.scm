;;; Test file for SRFI-163
(import (srfi 163))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "array-ref" 2 (array-ref (array (shape 0 3) 1 2 3) 1))
