;; test-lang-julia.scm — isolated tests for lang-julia.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-julia.scm")

(t "jl #{n * (n + 1) / 2}" 15 ((lambda (n) #{n * (n + 1) / 2}) 5))
(t "jl #{x * 2}" 20 ((lambda (x) #{x * 2}) 10))
(t "jl #{i + 1}" 4 ((lambda (i) #{i + 1}) 3))

(display "\n=== test-lang-julia.scm ===\n")
;; test-lang-julia.scm — Test Julia-like language (simplified)

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(display "\n--- function...end ---\n")
(function fact-jl (n) (if (<= n 1) 1 (* n (fact-jl (- n 1)))) end)
(test "factorial 10" (fact-jl 10) 3628800)
(test "factorial 5" (fact-jl 5) 120)

(function add-jl (a b) (+ a b) end)
(test "add" (add-jl 10 20) 30)

(display "\n--- for...in...end ---\n")
(let ((sum 0))
  (for x in '(1 2 3 4 5)
    (set! sum (+ sum x))
  end)
  (test "for sum" sum 15))

(display "\n--- comprehension ---\n")
(test "comp" (comp (* x 2) for x in '(1 2 3)) '(2 4 6))

(display "\n--- typeof ---\n")
(test "typeof Int64" (typeof 42) 'Int64)
(test "typeof String" (typeof "hi") 'String)
(test "typeof Function" (typeof +) 'Function)

(display "\n--- println ---\n")
(println "hello from Julia" 42)

(display "\n=== All Julia demos done ===\n")
