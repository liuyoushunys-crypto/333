;;; Test file for SRFI-6
(import (srfi 6))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "open-input-string" #\a (read-char (open-input-string "abc")))
(%test-equal "get-output-string" "" (get-output-string (open-output-string)))
