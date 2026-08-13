;;; Test file for SRFI-28
(import (srfi 28))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define s (open-output-string))
(format s "hello ~a" "world")
(%test-equal "format" "hello world" (get-output-string s))
