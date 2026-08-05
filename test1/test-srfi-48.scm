;;; Test file for SRFI-48
(import (srfi 48))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "format ~a" "42" (format #f "~a" 42))
(%test-equal "format ~s" "42" (format #f "~s" 42))
