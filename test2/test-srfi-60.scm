;;; Test file for SRFI-60
(import (srfi 60))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "logand" 2 (logand 6 3))
(%test-equal "logior" 7 (logior 6 3))
(%test-equal "logxor" 5 (logxor 6 3))
(%test-equal "arithmetic-shift" 8 (arithmetic-shift 2 2))
