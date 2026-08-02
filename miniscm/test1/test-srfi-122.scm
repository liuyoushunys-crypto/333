;;; Test file for SRFI-122
(import (srfi 122))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "make-nonempty-list" #t (nonempty-list? (cons 1 '(2 3))))
