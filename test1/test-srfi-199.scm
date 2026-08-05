;;; Test file for SRFI-199
(import (srfi 199))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define ms (make-mutable-string "abc"))
(%test-equal "mutable-string?" #t (mutable-string? ms))
