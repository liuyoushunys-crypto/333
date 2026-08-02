;;; Test file for SRFI-64
(import (srfi 64))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(test-begin "srfi-64")
(test-assert "true" #t)
(test-equal "one" 1 1)
(test-end "srfi-64")
(%test-equal "test-run" 'done 'done)
