;;; Test file for SRFI-226
(import (srfi 226))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "while" 3 (let ((x 0)) (while (< x 3) (set! x (+ x 1))) x))
