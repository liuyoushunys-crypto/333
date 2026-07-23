;;; Test file for SRFI-135
(import (srfi 135))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "text?" #t (text? (make-text "hello")))
(%test-equal "text-length" 5 (text-length (make-text "hello")))
