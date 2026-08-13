;; test-lang-c.scm — Test C-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-c.scm")

(display "\n--- function definitions ---\n")
(int fact-c (n) (if (<= n 1) 1 (* n (fact-c (- n 1)))))
(test "factorial 7" (fact-c 7) 5040)
(test "factorial 0" (fact-c 0) 1)

(int add-c (a b) (+ a b))
(test "add" (add-c 10 20) 30)

(void hello-c () (display "hello from C"))
(test "hello" (begin (hello-c) (if #f #f)) (if #f #f))

(display "\n--- ++ / -- operators ---\n")
(c-def int counter 5)
(++ counter)
(test "++ counter" counter 6)
(++ counter)
(test "++ again" counter 7)
(-- counter)
(test "-- counter" counter 6)
(++ counter)
(++ counter)
(test "++ x3" counter 8)

(display "\n--- += *= operators ---\n")
(c-def int n 10)
(+= n 5)
(test "+= 5" n 15)
(*= n 2)
(test "*= 2" n 30)
(/= n 3)
(test "/= 3" n 10)

(display "\n--- for loop ---\n")
(let ((sum 0))
  (for (c-def int i 0) (< i 100) (++ i)
    (set! sum (+ sum i)))
  (test "for sum 0..99" sum 4950))

(display "\n--- switch ---\n")
(define (test-switch x)
  (switch x
    (case 1 'one)
    (case 2 'two)
    (default 'other)))
(test "switch 1" (test-switch 1) 'one)
(test "switch 2" (test-switch 2) 'two)
(test "switch default" (test-switch 42) 'other)

(display "\n--- ternary ---\n")
(test "c-ternary true" (c-ternary (< 1 2) ? 42 : 0) 42)
(test "c-ternary false" (c-ternary (> 1 2) ? 42 : 99) 99)

(display "\n=== All C demos done ===\n")
