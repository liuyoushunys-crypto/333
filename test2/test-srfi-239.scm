;;; Test file for SRFI-239
(import (srfi 239))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "destructuring-bind" 1 (destructuring-bind (a b c) (list 1 2 3) a))
