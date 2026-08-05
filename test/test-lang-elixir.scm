;; test-lang-elixir.scm — isolated tests for lang-elixir.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-elixir.scm")

(t "ex #{n + 1}" 11 ((lambda (n) #{n + 1}) 10))
(t "ex #{x * 2}" 14 ((lambda (x) #{x * 2}) 7))
(t "ex #{5 + 3}" 8 #{5 + 3})

(display "\n=== test-lang-elixir.scm ===\n")
;; test-lang-elixir.scm — Test Elixir-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

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
(test "Enum.map" (Enum.map '(1 2 3) fn x -> (* x 2) end) '(2 4 6))
(test "Enum.filter" (Enum.filter '(1 2 3 4) fn x -> (even? x) end) '(2 4))

(display "\n--- IO.puts / inspect ---\n")
(IO.puts "hello from Elixir")
(test "IO.inspect" (IO.inspect 42) 42)

(display "\n--- is-nil / hd / tl ---\n")
(test "is-nil #f" (is-nil #f) #t)
(test "is-nil 42" (is-nil 42) #f)
(test "hd" (hd '(1 2 3)) 1)
(test "tl" (tl '(1 2 3)) '(2 3))

(display "\n--- for comprehension ---\n")
(let ((acc '()))
  (for-comp x <- '(a b c) do
    (set! acc (cons x acc))
  end)
  (test "for-comp" (reverse acc) '(a b c)))

(display "\n=== All Elixir demos done ===\n")
