;; test-lang-go.scm — isolated tests for lang-go.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-go.scm")

(t "go #{n + 1}" 11 ((lambda (n) #{n + 1}) 10))
(t "go #{n <= 1}" #t ((lambda (n) #{n <= 1}) 1))
(t "go #{i < 100}" #t ((lambda (i) #{i < 100}) 50))

(display "\n=== test-lang-go.scm ===\n")
;; test-lang-go.scm — Test Go-like language (simplified)

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(display "\n--- func ---\n")
(func fact-go (n) (if (<= n 1) 1 (* n (fact-go (- n 1)))))
(test "factorial 8" (fact-go 8) 40320)
(test "factorial 0" (fact-go 0) 1)
(func add-go (a b) (+ a b))
(test "add" (add-go 10 20) 30)

(display "\n--- := short var ---\n")
(:= go-x 42)
(test ":= x" go-x 42)

(display "\n--- switch ---\n")
(define (test-switch x)
  (switch x
    (case 1 'one)
    (case 2 'two)
    (default 'other)))
(test "switch 1" (test-switch 1) 'one)
(test "switch 99" (test-switch 99) 'other)

(display "\n--- fmt.Println ---\n")
(fmt.Println "hello from Go")

(display "\n=== All Go demos done ===\n")
