;;; Test file for SRFI-43
(import (srfi 43))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "vector-map" #(2 3 4) (vector-map (lambda (x) (+ x 1)) '#(1 2 3)))
 (%test-equal "vector->list" '(1 2 3) (vector->list '#(1 2 3)))
