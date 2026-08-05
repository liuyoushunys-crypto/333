;;; Test file for SRFI-111
(import (srfi 111))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define b (box 42))
(%test-equal "box?" #t (box? b))
(%test-equal "unbox" 42 (unbox b))
(set-box! b 99)
(%test-equal "set-box!" 99 (unbox b))
