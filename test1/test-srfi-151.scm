;;; Test file for SRFI-151
(import (srfi 151))

(define (%test-equal name expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display name) (newline))
      (begin (display "[FAIL] ") (display name)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))
(%test-equal "bitwise-and" 2 (bitwise-and 6 3))
(%test-equal "bitwise-ior" 7 (bitwise-ior 6 3))
(%test-equal "bitwise-xor" 5 (bitwise-xor 6 3))
(%test-equal "arithmetic-shift" 8 (arithmetic-shift 2 2))
