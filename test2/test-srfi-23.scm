;;; Test file for SRFI-23
(import (srfi 23))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "error raises" #t (call/cc (lambda (k) (with-exception-handler (lambda (e) (k #t)) (lambda () (error "test"))))))
