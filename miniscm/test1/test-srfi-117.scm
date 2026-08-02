;;; Test file for SRFI-117
(import (srfi 117))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define q (make-list-queue '(1 2 3)))
(%test-equal "list-queue-front" 1 (list-queue-front q))
