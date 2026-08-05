;;; Test file for SRFI-190
(import (srfi 190))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "coroutine" (1 2 3) (generator->list (make-coroutine-generator (lambda (y) (y 1) (y 2) (y 3)))))
