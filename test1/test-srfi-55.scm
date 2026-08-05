;;; Test file for SRFI-55
(import (srfi 55))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "require-extension" 'ok (begin (require-extension (srfi 1)) 'ok))
