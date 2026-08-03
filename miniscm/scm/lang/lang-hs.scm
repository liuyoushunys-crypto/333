(display "=== lang-hs loaded ===\n")
(define-syntax newdefine (syntax-rules () ((_ name (args ...) body ...) (define name (lambda (args ...) body ...)))))
(define-syntax let/in (syntax-rules (= in) ((_ name = val in body ...) (let ((name val)) body ...)) ((_ (name = val) ... in body ...) (let ((name val) ...) body ...))))
(define-macro (where expr . defs) (let loop ((ds defs) (binds '())) (if (null? ds) `(let ,(reverse binds) ,expr) (loop (cdr ds) (cons (list (car (car ds)) (cadr (cdr (car ds)))) binds)))))
(define-macro (lambda args . body) `(lambda ,args ,@body))
(define-macro ($ f x) `(,f ,x))
(define-macro (comp f g) `(lambda (x) (,f (,g x))))
(define-macro (++ a b) `(append ,a ,b))
(define lang-cons cons)
(define-macro (newcons x xs) `(lang-cons ,x ,xs))
(define-macro (!! xs n) `(list-ref ,xs ,n))
(define lang-map map)
(define-macro (newmap f lst) `(lang-map ,f ,lst))
(define-macro (newfilter lst pred) `(let loop ((xs ,lst) (acc '())) (if (null? xs) (reverse acc) (loop (cdr xs) (if (,pred (car xs)) (cons (car xs) acc) acc)))))
(define-macro (foldl f init lst) `(let loop ((acc ,init) (xs ,lst)) (if (null? xs) acc (loop (,f (car xs) acc) (cdr xs)))))
(define-macro (foldr f init lst) `(letrec ((fr (lambda (xs) (if (null? xs) ,init (,f (car xs) (fr (cdr xs))))))) (fr ,lst)))
(define-macro (guard name conds) `(letrec ((,name (lambda xs (cond ,@(map (lambda (c) `(,(car c) (begin ,@(cdr c)))) conds))))) ,name))
;; (define fact (n) (if #{n <= 0} 1 (* n (fact #{n - 1}))))
;; (let/in x = #{3 + 4} in ($ print x))
;; (print (map (lambda (x) #{x * 2}) '(1 2 3)))
;; (print (++ '(1 2) '(3 4)))
;; (print (!! '(10 20 30) 1))

(display "--- demos ---\n")
(define fib (n)
  (let loop ((a 0) (b 1) (i 0))
    (if #{i < n}
      (loop b #{a + b} #{i + 1})
      a)))
(display (string-append "fib(10) = " (number->string (fib 10))))
(newline)

(define fact (n)
  (if #{n <= 1}
    1
    (* n (fact #{n - 1}))))
(display (string-append "5! = " (number->string (fact 5))))
(newline)

(define sum-list (lst)
  (foldl (lambda (x acc) #{x + acc}) 0 lst))
(display (string-append "sum = " (number->string (sum-list (list 1 2 3 4 5)))))
(newline)

(define demo (lst)
  ($ display
    (format "evens doubled: ~a"
      (newmap (lambda (x) #{x * 2})
        (newfilter lst (lambda (x) (zero? #{x % 2}))))))
  (newline))
(demo (list 1 2 3 4 5 6))

(display "--- demos ---\n")

;; --- 1. fizzbuzz ---
;; (define fizzbuzz (n)
;;   (let loop ((i 1))
;;     (if #{i > n}
;;       #f
;;       (begin
;;         (if (zero? #{i % 15})
;;           (display "FizzBuzz")
;;           (if (zero? #{i % 3})
;;             (display "Fizz")
;;             (if (zero? #{i % 5})
;;               (display "Buzz")
;;               (display i))))
;;         (newline)
;;         (loop #{i + 1})))))
;; (fizzbuzz 15)

;; --- 2. factorial with foldl ---
;; (define fact (n)
;;   (foldl (lambda (x acc) #{x * acc}) 1 (iota n 1)))
;; ($ display (string-append "6! = " (number->string (fact 6))))
;; (newline)

;; --- 3. filter then map ---
;; (define evens-doubled (lst)
;;   ($ display
;;     (map (lambda (x) #{x * 2})
;;       (newfilter lst (lambda (x) (zero? #{x % 2})))))
;;   (newline))
;; (evens-doubled (list 1 2 3 4 5 6))

;; --- 4. quicksort ---
;; (define qsort (lst)
;;   (if (null? lst)
;;     (list)
;;     (let/in pivot = (car lst) in
;;       (++ (qsort (filter (cdr lst) (lambda (x) #{x < pivot})))
;;         (cons pivot (qsort (filter (cdr lst) (lambda (x) #{x >= pivot}))))))))
;; ($ display (qsort (list 3 1 4 1 5 9 2 6)))
;; (newline)
