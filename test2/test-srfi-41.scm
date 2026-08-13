;;; Test file for SRFI-41
(import (srfi 41))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define s (stream-cons 1 (stream-cons 2 stream-null)))
(%test-equal "stream-car" 1 (stream-car s))
(%test-equal "stream-null?" #t (stream-null? stream-null))
