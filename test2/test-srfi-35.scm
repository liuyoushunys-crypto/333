;;; Test file for SRFI-35
(import (srfi 35))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define err-type (make-condition-type 'error #f (lambda (x) #t) '(msg)))
(define err (make-condition err-type 'msg "test"))
(%test-equal "condition?" #t (condition? err))
(%test-equal "condition-ref" "test" (condition-ref err 'msg))
