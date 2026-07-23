;;; Test file for SRFI-113
(import (srfi 113))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define s (set 1 2 3))
(%test-equal "set?" #t (set? s))
(%test-equal "set-contains?" #t (set-contains? s 2))
