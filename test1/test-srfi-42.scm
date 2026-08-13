;;; Test file for SRFI-42
(import (srfi 42))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "list-ec" '(0 1 4 9 16) (list-ec (* i i) (:range i 0 5)))
(%test-equal "sum-ec" 45 (sum-ec i (:range i 0 10)))
(%test-equal "any?-ec" #t (any?-ec (even? i) (:list i '(1 3 5 6))))
