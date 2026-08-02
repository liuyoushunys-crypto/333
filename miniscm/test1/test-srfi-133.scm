;;; Test file for SRFI-133
(import (srfi 133))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "vector-copy" #(1 2) (vector-copy '#(1 2)))
(%test-equal "vector-append" #(1 2 3 4) (vector-append '#(1 2) '#(3 4)))
