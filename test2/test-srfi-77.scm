;;; Test file for SRFI-77
(import (srfi 77))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "array2d?" #t (array2d? (make-array2d 2 3 0)))
(%test-equal "array2d-rows" 2 (array2d-rows (make-array2d 2 3 0)))
