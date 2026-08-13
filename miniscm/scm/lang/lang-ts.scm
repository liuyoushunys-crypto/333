(display "=== lang-ts loaded ===\n")
(define-syntax function (syntax-rules (=>) ((_ name (args ...) => ret body ...) (define name (lambda (args ...) body ...))) ((_ name (args ...) body ...) (define name (lambda (args ...) body ...)))))
(define-syntax const (syntax-rules (=) ((_ name = val) (define name val))))
(define-syntax newlet (syntax-rules (=) ((_ name = val) (define name val)) ((_ name) (define name #f))))
(define-syntax console.log (syntax-rules () ((_ x ...) (begin (display x) ... (newline)))))
(define-syntax arrow (syntax-rules (=>) ((_ args => body) (lambda args body))))
(define-macro (array T) `'list)
(define lang-map map)
(define-macro (newmap f lst) `(lang-map ,f ,lst))
(define-macro (newfilter lst pred) `(let loop ((xs ,lst) (acc '())) (if (null? xs) (reverse acc) (loop (cdr xs) (if (,pred (car xs)) (cons (car xs) acc) acc)))))
(define-macro (reduce f init lst) `(let loop ((acc ,init) (xs ,lst)) (if (null? xs) acc (loop (,f (car xs) acc) (cdr xs)))))
(define-macro (?. obj prop) `(let ((v ,obj)) (if v (v ,prop) #f)))
(define-macro (?? a b) `(let ((v ,a)) (if v v ,b)))
(define-syntax for-of (syntax-rules (of) ((_ var of arr body ...) (for-each (lambda (var) body ...) arr))))
(define-macro (template . parts) `(string-append ,@(map (lambda (p) (if (string? p) p `(format "~a" ,p))) parts)))
(define-macro (interface name . defs) `(quote ,name))
(define-macro (type name def) `(quote ,name))
(define-macro (?.) (syntax-rules () ((_ obj prop) (let ((v ,obj)) (if v (v prop) #f)))))
;; (function fact (n) => number (if #{n <= 1} 1 (* n (fact #{n - 1}))))
;; (const add = (arrow (x y) => #{x + y}))
;; (console.log (filter '(1 2 3) (lambda (x) #{x > 1})))
;; (console.log (template "sum = " #{2 + 3}))
;; (for-of x of '(10 20 30) (console.log x))

(display "--- demos ---\n")
(function fib (n)
  (define a 0)
  (define b 1)
  (for-of i of (iota n)
    (define t (+ a b))
    (set! a b)
    (set! b t))
  (console.log (string-append "fib(" (number->string n) ") = " (number->string a))))
(fib 10)

(function filter-demo (arr)
  (define evens (filter (lambda (x) (zero? (modulo x 2))) arr))
  (console.log "evens:" evens))
(filter-demo (list 1 2 3 4 5 6))

(function sum-demo (arr)
  (define total
    (let loop ((xs arr) (acc 0))
      (if (null? xs) acc (loop (cdr xs) (+ acc (car xs))))))
  (console.log "sum:" total))
(sum-demo (list 1 2 3 4 5))

(display "--- demos ---\n")

;; --- 1. fizzbuzz ---
;; (function fizzbuzz (n) => void
;;   (for-of i of (iota n)
;;     (let ((i #{i + 1}))
;;       (if (zero? #{i % 15})
;;         (console.log "FizzBuzz")
;;         (if (zero? #{i % 3})
;;           (console.log "Fizz")
;;           (if (zero? #{i % 5})
;;             (console.log "Buzz")
;;             (console.log i)))))))
;; (fizzbuzz 15)

;; --- 2. factorial with reduce ---
;; (function fact (n) => number
;;   (newlet nums = (iota n 1))
;;   (newlet result = (reduce (lambda (x acc) #{x * acc}) 1 nums))
;;   (console.log (template n "! = " result)))
;; (fact 6)

;; --- 3. chain filter-map-reduce ---
;; (function sum-even-squares (arr) => number
;;   (newlet evens = (filter arr (lambda (x) (zero? #{x % 2}))))
;;   (newlet squares = (map (lambda (x) #{x * x}) evens))
;;   (newlet total = (reduce (lambda (x acc) #{x + acc}) 0 squares))
;;   (console.log (template "sum even squares: " total)))
;; (sum-even-squares (list 1 2 3 4 5 6))

;; --- 4. nullish coalescing ---
;; (function greet (name) => void
;;   (newlet safe = (?? name "world"))
;;   (console.log (template "hello, " safe)))
;; (greet #f)
;; (greet "alice")
