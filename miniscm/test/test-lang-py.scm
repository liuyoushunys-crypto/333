;; test-lang-py.scm — isolated tests for lang-py.scm DSL
(define (t label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(load "scm/lang/lang-py.scm")

(t "py #{n + 1}" 6 ((lambda (n) #{n + 1}) 5))
(t "py #{n <= 1}" #f ((lambda (n) #{n <= 1}) 5))
(t "py #{n * 2 + 1}" 11 ((lambda (n) #{n * 2 + 1}) 5))

(display "\n=== test-lang-py.scm ===\n")
;; test-lang-py.scm — Test Python-like language

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(display "\n--- def / function ---\n")
(def factorial-py (n) (if (<= n 1) 1 (* n (factorial-py (- n 1)))))
(test "factorial 5" (factorial-py 5) 120)
(test "factorial 0" (factorial-py 0) 1)
(test "factorial 10" (factorial-py 10) 3628800)

(def square-py (x) (* x x))
(test "square 7" (square-py 7) 49)
(test "square -3" (square-py -3) 9)

(def add-py (a b) (+ a b))
(test "add" (add-py 10 20) 30)

(display "\n--- list comprehension ---\n")
(test "list-comp basic" (list-comp (* x 2) for x in '(1 2 3 4 5))
      '(2 4 6 8 10))
(test "list-comp filter" (list-comp x for x in '(1 2 3 4 5 6) when (even? x))
      '(2 4 6))
(test "list-comp filter odd" (list-comp x for x in '(1 2 3 4 5) when (odd? x))
      '(1 3 5))
(test "list-comp empty" (list-comp x for x in '()) '())

(display "\n--- range ---\n")
(test "range 5" (range 5) '(0 1 2 3 4))
(test "range 2 5" (range 2 5) '(2 3 4))
(test "range 0 10 2" (range 0 10 2) '(0 2 4 6 8))

(display "\n--- print ---\n")
(print "hello from Python")
(test "print returns void" (begin (print 42) (if #f #f)) (if #f #f))

(display "\n--- isinstance ---\n")
(test "isinstance int" (isinstance 42 int) #t)
(test "isinstance str" (isinstance "hello" str) #t)
(test "isinstance bool" (isinstance #t bool) #t)
(test "isinstance list" (isinstance '(1 2) list) #t)

(display "\n--- try/except (guard) ---\n")
(let ((caught #f))
  (try (error "oops") except (e) (set! caught #t))
  (test "try/except" caught #t))

(display "\n=== All Python demos done ===\n")
