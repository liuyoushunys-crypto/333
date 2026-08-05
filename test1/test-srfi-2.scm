;;; Test file for SRFI-2
(import (srfi 2))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "and-let* empty" #t (and-let* ()))
(%test-equal "and-let* pass" 3 (and-let* ((x 1) (y 2)) (+ x y)))
(%test-equal "and-let* fail" #f (and-let* ((x #f) (y 2)) (+ x y)))
