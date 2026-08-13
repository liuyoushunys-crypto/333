;;; Test file for SRFI-143
(import (srfi 143))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "fixnum?" #t (fixnum? 42))
(%test-equal "fx+" 5 (fx+ 2 3))
