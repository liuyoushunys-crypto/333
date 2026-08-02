;;; Test file for SRFI-171
(import (srfi 171))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "list-transduce tfilter" (1 3) (list-transduce (tfilter odd?) rcons '() '(1 2 3 4)))
(%test-equal "list-transduce tmap" (2 4 6) (list-transduce (tmap (lambda (x) (* x 2))) rcons '() '(1 2 3)))
