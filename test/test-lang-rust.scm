;; test-lang-rust.scm — isolated tests for lang-rust.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-rust.scm")

(t "rs #{n * (n + 1) / 2}" 55 ((lambda (n) #{n * (n + 1) / 2}) 10))
(t "rs #{n <= 1}" #f ((lambda (n) #{n <= 1}) 3))
(t "rs #{x + 1}" 101 ((lambda (x) #{x + 1}) 100))

(display "\n=== test-lang-rust.scm ===\n")
;; test-lang-rust.scm — Test Rust-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(display "\n--- fn ---\n")
(fn fact-rs (n) (if (<= n 1) 1 (* n (fact-rs (- n 1)))))
(test "factorial 10" (fact-rs 10) 3628800)
(test "factorial 5" (fact-rs 5) 120)
(test "factorial 0" (fact-rs 0) 1)

(fn add-rs (a b) (+ a b))
(test "add" (add-rs 10 20) 30)

(display "\n--- let / let-mut ---\n")
(def x = 42)
(test "let x" x 42)

(let-mut y = 100)
(set y = (+ y 50))
(test "set y" y 150)

(display "\n--- match ---\n")
(define (test-match x)
  (match x
    (1 'one)
    (2 'two)
    (3 'three)
    (_ 'other)))
(test "match 1" (test-match 1) 'one)
(test "match 3" (test-match 3) 'three)
(test "match 42" (test-match 42) 'other)

(display "\n--- Option types ---\n")
(def val = (Some 42))
(test "Some" val 42)
(test "None" (None) #f)

(define (safe-div n d)
  (if (= d 0) (None) (Some (/ n d))))
(test "safe-div ok" (safe-div 10 2) 5)
(test "safe-div fail" (safe-div 10 0) #f)

(display "\n--- vec operations ---\n")
(def v = (vec 1 2 3 4 5))
(test "vec length" (len v) 5)
(push v 6)
(test "push length" (len v) 6)
(def popped = (pop v))
(test "pop value" popped 6)
(test "pop length" (len v) 5)

(display "\n--- while ---\n")
(let ((n 10) (sum 0))
  (while (> n 0)
    (set! sum (+ sum n))
    (set! n (- n 1)))
  (test "while sum 10..1" sum 55))

(display "\n--- for in ---\n")
(let ((acc 0))
  (for x in (vec 1 2 3 4 5)
    (set! acc (+ acc x)))
  (test "for sum" acc 15))

(display "\n--- println ---\n")
(println "hello from Rust")
(println "sum = ~a" 42)

(display "\n=== All Rust demos done ===\n")
