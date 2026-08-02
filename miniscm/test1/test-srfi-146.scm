;;; Test file for SRFI-146
(import (srfi 146))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define m (mapping 'a 1 'b 2))
(%test-equal "mapping?" #t (mapping? m))
