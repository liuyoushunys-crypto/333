;;; Test file for SRFI-178
(import (srfi 178))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "bitvector?" #t (bitvector? (make-bitvector 8 #t)))
(%test-equal "bitvector-length" 8 (bitvector-length (make-bitvector 8 #t)))
