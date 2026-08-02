;;; Test file for SRFI-38
(import (srfi 38))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "write-with-shared-structure" #t (string? (call-with-output-string (lambda (p) (write-with-shared-structure 42 p)))))
