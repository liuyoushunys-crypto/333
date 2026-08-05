;;; Test file for SRFI-115
(import (srfi 115))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "regexp?" #t (regexp? (regexp "[0-9]+")))
(%test-equal "regexp-matches?" #t (regexp-matches? (regexp "[0-9]+") "123"))
