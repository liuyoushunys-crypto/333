;;; Test file for SRFI-127
(import (srfi 127))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define ls (make-lseq 1 (make-lseq 2)))
(%test-equal "lseq?" #t (lseq? ls))
