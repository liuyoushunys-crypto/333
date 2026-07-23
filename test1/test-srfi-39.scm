;;; Test file for SRFI-39
(import (srfi 39))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define p (make-parameter 42))
(%test-equal "param init" 42 (p))
(%test-equal "param parameterize" 99 (parameterize ((p 99)) (p)))
