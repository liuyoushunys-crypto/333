;;; Test file for SRFI-26
(import (srfi 26))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "cut 1" 8 ((cut + 5 <>) 3))
(%test-equal "cut 2" 7 ((cut + <> <>) 3 4))
(%test-equal "cute" 15 ((cute + (* 2 5) <>) 5))
