;;; Test file for SRFI-5
(import (srfi 5))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "let simple" 3 (let ((x 1) (y 2)) (+ x y)))
(%test-equal "let named" 120 (let loop ((n 5) (a 1)) (if (zero? n) a (loop (- n 1) (* a n)))))
