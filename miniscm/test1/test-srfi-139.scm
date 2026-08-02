;;; Test file for SRFI-139
(import (srfi 139))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define sc (make-syntax-closure '(a b) '(a)))
(%test-equal "syntax-closure?" #t (syntax-closure? sc))
