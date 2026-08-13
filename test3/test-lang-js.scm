;; test-lang-js.scm — Test JavaScript-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-js.scm")

(display "\n--- function ---\n")
(function fact-js (n) (if (<= n 1) 1 (* n (fact-js (- n 1)))))
(test "factorial 6" (fact-js 6) 720)
(test "factorial 1" (fact-js 1) 1)

(function add-js (a b) (+ a b))
(test "function add" (add-js 10 20) 30)

(display "\n--- var / const ---\n")
(var x = 42)
(test "var x" x 42)
(var y)
(test "var undefined" (defined? 'y) #t)

(const pi = 3.14159)
(test "const pi" (> pi 3.14) #t)

(display "\n--- console.log ---\n")
(console.log "hello from JS")

(display "\n--- typeof ---\n")
(test "typeof number" (typeof 42) 'number)
(test "typeof string" (typeof "hi") 'string)
(test "typeof bool" (typeof #t) 'boolean)
(test "typeof function" (typeof +) 'function)
(test "typeof list" (typeof '(1 2)) 'object)

(display "\n--- === / !== ---\n")
(test "=== numbers" (=== 42 42) #t)
(test "=== strings" (=== "a" "a") #t)
(test "=== diff" (=== 1 2) #f)
(test "!== true" (!== 1 2) #t)
(test "!== false" (!== 1 1) #f)

(display "\n--- array/object ---\n")
(test "array literal" (length ($ 1 2 3 ])) 3)
(test "object" (length (object a 1 b 2)) 2)

(display "\n--- for loop ---\n")
(let ((sum 0))
  (for i = 0 (< i 5) (set! i (+ i 1))
    (set! sum (+ sum i)))
  (test "for loop sum" sum 10))

(display "\n=== All JavaScript demos done ===\n")
