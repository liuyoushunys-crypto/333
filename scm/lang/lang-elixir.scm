(display "=== lang-elixir loaded ===\n")
(define-syntax defmodule (syntax-rules (do end) ((_ name do body ... end) (begin (display "mod: ") (display 'name) (newline) body ...))))
(define-syntax def (syntax-rules (do end) ((_ name (args ...) do body ... end) (define name (lambda (args ...) body ...)))))
(define-syntax elixir-set (syntax-rules (=) ((_ name = val) (define name val))))
(define-syntax thr (syntax-rules () ((_ x) x) ((_ x (f . a) . r) (thr (f x . a) . r)) ((_ x f . r) (thr (f x) . r))))
(define-syntax for-comp (syntax-rules (<- do end) ((_ x <- list do body ... end) (for-each (lambda (x) body ...) list))))
(define-syntax puts (syntax-rules () ((_ x) (begin (display x) (newline)))))
(define-syntax inspect (syntax-rules () ((_ x) (begin (display x) (newline) x))))
(define-syntax head (syntax-rules () ((_ lst) (car lst))))
(define-syntax tail (syntax-rules () ((_ lst) (cdr lst))))
(define-syntax len (syntax-rules () ((_ x) (length x))))
(define-macro (elixir-case expr . clauses) `(cond ,@(let loop ((cl clauses)) (if (null? cl) '() (cons `((equal? ,expr (quote ,(car cl))) ,@(caddr cl)) (loop (cdddr cl)))))))
(define-macro (cond-do . clauses) `(cond ,@(let loop ((cl clauses)) (if (null? cl) '() (cons `(,(car cl) ,@(caddr cl)) (loop (cdddr cl)))))))
(define-macro (Enum-map lst fn var -> . body) `(map (lambda (,var) ,@body) ,lst))
;; (def fact (n) do (if #{n <= 1} 1 (* n (fact #{n - 1}))) end)
;; (puts (thr 5 fact (lambda (x) #{x + 1})))
;; (puts (head '(1 2 3)))
;; (elixir-case 42 (1 "one") (42 "answer"))

(display "--- demos ---\n")
(def fib (n) do
  (elixir-set a = 0)
  (elixir-set b = 1)
  (for-comp i <- (iota n) do
    (elixir-set t = #{a + b})
    (set! a b)
    (set! b t)
  end)
  (puts (string-append "fib(" (number->string n) ") = " (number->string a)))
end)
(fib 10)

(def pipe-demo (n) do
  (thr n
    (lambda (x) #{x + 1})
    (lambda (x) #{x * 2})
    (lambda (x) (format "pipe: ~a" x))
    puts)
end)
(pipe-demo 5)

(def filter-pos (lst) do
  (elixir-set result = (list))
  (for-comp x <- lst do
    (if #{x > 0}
      (set! result (append result (list x))))
  end)
  (puts (format "positives: ~a" result))
end)
(filter-pos (list 3 -1 0 5 -2 7))

(display "--- demos ---\n")

;; --- 1. fizzbuzz ---
;; (def fizzbuzz (n) do
;;   (for-comp x <- (iota n) do
;;     (let ((x #{x + 1}))
;;       (if (zero? #{x % 15})
;;         (puts "FizzBuzz")
;;         (if (zero? #{x % 3})
;;           (puts "Fizz")
;;           (if (zero? #{x % 5})
;;             (puts "Buzz")
;;             (puts x)))))
;;   end)
;; end)
;; (fizzbuzz 15)

;; --- 2. thr pipe chain ---
;; (def result (n) do
;;   (thr n
;;     (lambda (x) #{x + 2})
;;     (lambda (x) #{x * 3})
;;     (lambda (x) #{x - 1})
;;     puts)
;; end)
;; (result 5)

;; --- 3. map with Enum-map ---
;; (def map-demo (lst) do
;;   (puts (format "mapped: ~a"
;;     (Enum-map lst (lambda (x) x -> #{x * 10}))))
;; end)
;; (map-demo (list 1 2 3))

;; --- 4. accumulator loop ---
;; (def sum-sq (n) do
;;   (elixir-set total = 0)
;;   (for-comp i <- (iota n) do
;;     (set! total (+ total (* #{i + 1} #{i + 1})))
;;   end)
;;   (puts (format "sum sq ~a = ~a" n total))
;; end)
;; (sum-sq 5)
