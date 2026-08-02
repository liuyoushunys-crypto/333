(display "=== lang-swift loaded ===\n")
(define-syntax func (syntax-rules (->) ((_ name (args ...) -> ret body ...) (define name (lambda (args ...) body ...))) ((_ name (args ...) body ...) (define name (lambda (args ...) body ...)))))
(define-syntax let (syntax-rules (=) ((_ name = val) (define name val))))
(define-syntax var (syntax-rules (=) ((_ name = val) (define name val)) ((_ name) (define name #f))))
(define-syntax print (syntax-rules () ((_ x) (begin (display x) (newline)))))
(define-syntax for-in (syntax-rules (in) ((_ var in lst body ...) (for-each (lambda (var) body ...) lst))))
(define-syntax while (syntax-rules () ((_ cond body ...) (let loop () (if cond (begin body ... (loop)) #f)))))
(define-syntax repeat-while (syntax-rules () ((_ body ... cond) (let loop () (begin body ... (if cond (loop) #f))))))
(define-macro (if-let bindings . body) (if (and (pair? bindings) (eq? (car bindings) '=)) `(let ((,(cadr bindings) ,(caddr bindings))) (if ,(cadr bindings) (begin ,@body) #f)) `(if-let ,bindings ,@body)))
(define-macro (guard-let bindings . body) (if (and (pair? bindings) (eq? (car bindings) '=)) `(let ((,(cadr bindings) ,(caddr bindings))) (if (not ,(cadr bindings)) (begin ,@body) ,(cadr bindings))) `(guard-let ,bindings ,@body)))
(define-macro (?? a b) `(let ((v ,a)) (if v v ,b)))
(define lang-map map)
(define-macro (map f lst) `(lang-map ,f ,lst))
(define-macro (filter lst pred) `(let loop ((xs ,lst) (acc '())) (if (null? xs) (reverse acc) (loop (cdr xs) (if (,pred (car xs)) (cons (car xs) acc) acc)))))
;; (func fact (n) -> Int (if #{n <= 1} 1 (* n (fact #{n - 1}))))
;; (print (?? #f 42))
;; (for-in i in '(1 2 3) (print i))
;; (print (map (lambda (x) #{x * 2 + 1}) '(1 2 3)))
;; (func sum (n) (if #{n <= 0} 0 #{n * (n + 1) / 2}))

(display "--- demos ---\n")
(func fib (n) -> Int
  (var a = 0)
  (var b = 1)
  (for-in i in (iota n)
    (var t = #{a + b})
    (set! a b)
    (set! b t))
  (print (string-append "fib(" (number->string n) ") = " (number->string a))))
(fib 10)

(func sum-to (n)
  (var total = 0)
  (var i = 1)
  (repeat-while
    (set! total #{total + i})
    (set! i #{i + 1})
    #{i <= n})
  (print (string-append "sum 1.." (number->string n) " = " (number->string total))))
(sum-to 10)

(func chain-demo (lst)
  (var evens = (filter lst (lambda (x) (zero? #{x % 2}))))
  (var doubled = (map (lambda (x) #{x * 2}) evens))
  (print (format "evens doubled: ~a" doubled)))
(chain-demo (list 1 2 3 4 5 6))

(display "--- demos ---\n")

;; --- 1. fizzbuzz ---
;; (func fizzbuzz (n)
;;   (for-in i in (iota n)
;;     (let ((i #{i + 1}))
;;       (if (zero? #{i % 15})
;;         (print "FizzBuzz")
;;         (if (zero? #{i % 3})
;;           (print "Fizz")
;;           (if (zero? #{i % 5})
;;             (print "Buzz")
;;             (print i)))))))
;; (fizzbuzz 15)

;; --- 2. factorial ---
;; (func fact (n) -> Int
;;   (var result = 1)
;;   (var i = 1)
;;   (while #{i <= n}
;;     (set! result #{result * i})
;;     (set! i #{i + 1}))
;;   (print (string-append (number->string n) "! = " (number->string result))))
;; (fact 6)

;; --- 3. guard-let demo ---
;; (func safe-head (lst)
;;   (guard-let h = (and (pair? lst) (car lst))
;;     (print "empty list"))
;;   (?? h (print "nil")))
;; (safe-head (list 42))
;; (safe-head (list))

;; --- 4. repeat-while countdown ---
;; (func countdown (n)
;;   (var i = n)
;;   (repeat-while
;;     (print i)
;;     (set! i #{i - 1})
;;     #{i > 0})
;;   (print "go!"))
;; (countdown 5)
