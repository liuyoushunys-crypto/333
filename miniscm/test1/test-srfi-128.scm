;;; Test file for SRFI-128
(import (srfi 128))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "comparator?" #t (comparator? integer-comparator))
(%test-equal "=?" #t (=? integer-comparator 3 3))
(%test-equal "<?" #t (<? integer-comparator 1 2))
