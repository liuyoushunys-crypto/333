;;; Test file for SRFI-180
(import (srfi 180))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "json-read-string" 42 (json-read-string "42"))
(%test-equal "json-write-string" "42" (json-write-string 42))
