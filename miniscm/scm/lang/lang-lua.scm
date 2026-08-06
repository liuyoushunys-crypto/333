(display "=== lang-lua loaded ===\n")
(define-syntax function (syntax-rules (end) ((_ name (args ...) body ... end) (define name (lambda (args ...) body ...)))))
(define-syntax while (syntax-rules (end) ((_ cond body ... end) (let loop () (if cond (begin body ... (loop)))))))
(define-syntax repeat-until (syntax-rules () ((_ body ... cond) (let loop () (begin body ... (if cond #f (loop)))))))
(define-macro (for var = start end . rest) (let ((step (if (and (not (null? rest)) (eq? (car rest) 'step)) (cadr rest) 1)) (body (if (and (not (null? rest)) (eq? (car rest) 'step)) (cddr rest) rest))) `(let ((,var ,start) (limit ,end) (step ,step)) (let loop () (if (<= ,var limit) (begin ,@body (set! ,var (+ ,var step)) (loop)) #f)))))
(define-syntax for-in (syntax-rules (in) ((_ var in lst body ...) (for-each (lambda (var) body ...) lst))))
(define-syntax myif (syntax-rules (then elseif else end) ((_ cond then body ... end) (if cond (begin body ...) #f)) ((_ cond then body ... elseif clauses ... end) (if cond (begin body ...) (if ,@(cdr clauses) ...))) ((_ cond then body ... else body2 ... end) (if cond (begin body ...) (begin body2 ...)))))
(define-syntax print (syntax-rules () ((_ args ...) (begin (display args) ... (newline)))))
(define-macro (len x) `(length ,x))
(define-macro (table . pairs) `(list ,@(let loop ((p pairs) (acc '())) (if (null? p) (reverse acc) (loop (cddr p) (cons `(cons (quote ,(car p)) ,(cadr p)) acc))))))
(define-macro (pairs tbl) `(let loop ((xs ,tbl)) (if (null? xs) '() (cons (cons (car (car xs)) (cdr (car xs))) (loop (cdr xs))))))
;; (function fact (n) (if #{n <= 1} then 1 else (* n (fact #{n - 1})) end))
;; (for i = 1 10 (print i))
;; (repeat-until (set! i #{i + 1}) #{i >= 5})
;; (print (len '(1 2 3)))
;; (print (table a 1 b 2 c 3))

(display "--- demos ---\n")
(function fib (n)
  (let ((a 0) (b 1))
    (for i = 1 n
      (let ((t #{a + b}))
        (set! a b)
        (set! b t)))
    (print (string-append "fib(" (number->string n) ") = " (number->string a))))
end)
(fib 10)

(function sum-to (n)
  (let ((total 0) (i 1))
    (repeat-until
      (set! total #{total + i})
      (set! i #{i + 1})
      #{i > n})
    (print (string-append "sum 1.." (number->string n) " = " (number->string total))))
end)
(sum-to 10)

(function print-table (tbl)
  (for-in kv in (pairs tbl)
    (print (format "~a = ~a" (car kv) (cdr kv))))
end)
(print-table (table a 1 b 2 c 3))

(display "--- demos ---\n")

;; --- 1. fizzbuzz ---
;; (function fizzbuzz (n)
;;   (for i = 1 n
;;     (if #{i % 15 == 0} then (print "FizzBuzz")
;;       elseif #{i % 3 == 0} then (print "Fizz")
;;       elseif #{i % 5 == 0} then (print "Buzz")
;;       else (print i)
;;     end)
;;   )
;; end)
;; (fizzbuzz 15)

;; --- 2. factorial ---
;; (function fact (n)
;;   (let ((result 1) (i 1))
;;     (repeat-until
;;       (set! result #{result * i})
;;       (set! i #{i + 1})
;;       #{i > n})
;;     (print (string-append (number->string n) "! = " (number->string result))))
;; end)
;; (fact 6)

;; --- 3. while sum ---
;; (function sum-to (n)
;;   (let ((total 0) (i 1))
;;     (while #{i <= n}
;;       (set! total #{total + i})
;;       (set! i #{i + 1})
;;     end)
;;     (print (string-append "sum 1.." (number->string n) " = " (number->string total))))
;; end)
;; (sum-to 10)

;; --- 4. pairs demo ---
;; (function kv-count (tbl)
;;   (let ((n 0))
;;     (for-in kv in (pairs tbl)
;;       (set! n #{n + 1}))
;;     (print (string-append "entries: " (number->string n))))
;; end)
;; (kv-count (table a 1 b 2 c 3 d 4))
