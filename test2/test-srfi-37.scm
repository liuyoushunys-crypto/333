;;; Test file for SRFI-37
(import (srfi 37))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(define opts (list (option '(#\v "verbose") #f (lambda (arg name val seeds) (cons 'verbose seeds)))))
(%test-equal "option?" #t (option? (car opts)))
