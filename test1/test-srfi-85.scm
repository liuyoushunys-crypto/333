;;; Test file for SRFI-85
(import (srfi 85))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "recursive-equality?" #t (recursive-equality? (list 1 (list 2)) (list 1 (list 2))))
