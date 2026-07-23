;;; Test file for SRFI-247
(import (srfi 247))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "assoc-map?" #t (assoc-map? (assoc-map 'a 1)))
