;;; Test file for SRFI-69
(import (srfi 69))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define ht (make-hash-table))
(hash-table-set! ht 'a 1)
(%test-equal "hash-table-ref" 1 (hash-table-ref ht 'a))
