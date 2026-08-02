;;; Test file for SRFI-192
(import (srfi 192))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(let ((p (open-input-string "abc"))) (%test-equal "port-position" 0 (port-position p)))
