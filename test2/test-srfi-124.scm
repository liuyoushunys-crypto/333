;;; Test file for SRFI-124
(import (srfi 124))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define ep (make-ephemeron 'key 'value))
(%test-equal "ephemeron?" #t (ephemeron? ep))
