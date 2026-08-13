;; test-lang-elixir.scm — Test Elixir-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-elixir.scm")

(display "\n--- def...do...end ---\n")
(def fact-ex (n) do
  (if (= n 0) 1 (* n (fact-ex (- n 1))))
end)
(test "factorial 5" (fact-ex 5) 120)
(test "factorial 0" (fact-ex 0) 1)
(test "factorial 8" (fact-ex 8) 40320)

(def add-ex (a b) do (+ a b) end)
(test "add" (add-ex 10 20) 30)

(display "\n--- defmodule ---\n")
(defmodule Math do
  (def mul (a b) do (* a b) end)
end)
(test "module mul" (mul 6 7) 42)

(display "\n--- pipe |> ---\n")
(test "pipe add1" (|> 5 (+ 1)) 6)
(test "pipe chain" (|> 5 (* 2) (+ 1)) 11)
(test "pipe identity" (|> 42) 42)

(display "\n--- Enum.map/filter ---\n")
(test "map" (map (lambda (x) (* x 2)) '(1 2 3)) '(2 4 6))
(test "filter" (filter even? '(1 2 3 4)) '(2 4))

(display "\n--- IO.puts / inspect ---\n")
(puts "hello from Elixir")
(test "inspect" (inspect 42) 42)

(display "\n--- is-nil / hd / tl ---\n")
(test "is-nil #f" (not #f) #t)
(test "is-nil 42" (not (null? 42)) #t)
(test "head" (head '(1 2 3)) 1)
(test "tail" (tail '(1 2 3)) '(2 3))

(display "\n--- for comprehension ---\n")
(let ((acc '()))
  (for-comp x <- '(a b c) do
    (set! acc (cons x acc))
  end)
  (test "for-comp" (reverse acc) '(a b c)))

(display "\n=== All Elixir demos done ===\n")
