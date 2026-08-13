;;; Test file for SRFI-189
(import (srfi 189))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "just" 42 (maybe 42))
(%test-equal "nothing?" #t (nothing? #f))
