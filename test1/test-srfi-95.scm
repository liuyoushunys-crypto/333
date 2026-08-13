;;; Test file for SRFI-95
(import (srfi 95))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "sort" '(1 2 3) (sort '(3 1 2) <))
(%test-equal "sorted?" #t (sorted? '(1 2 3) <))
