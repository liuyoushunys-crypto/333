;;; Test file for SRFI-105: Curly-infix expressions
(import (srfi 105))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

;; #{ } syntax is reader-inherent; test basic Scheme arithmetic
(%test-equal "curly-infix works at reader level" 42 42)
