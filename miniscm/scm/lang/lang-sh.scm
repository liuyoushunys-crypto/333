(display "=== lang-sh loaded ===\n")
(define-syntax sh-var (syntax-rules (=) ((_ name = val) (define name val))))
(define-syntax echo (syntax-rules () ((_ x ...) (begin (display x) ... (newline)))))
(define-syntax sh-for (syntax-rules (in do done) ((_ var in list do body ... done) (for-each (lambda (var) body ...) list))))
(define-syntax sh-while (syntax-rules (do done) ((_ cond do body ... done) (let loop () (if cond (begin body ... (loop)) #f)))))
(define-macro (sh-if . clauses) (let loop ((cs clauses)) (if (null? cs) #f (let ((kw (car cs))) (case kw ((then) (if (null? (cdr cs)) #f `(begin ,@(cadr cs)))) ((elif) (if (null? (cddr cs)) #f `(if ,(cadr cs) (begin ,@(caddr cs)) ,(loop (cdddr cs))))) ((else) (if (null? (cdr cs)) #f `(begin ,@(cdr cs)))) ((fi) #f) (else `(if ,cs ,(loop (cdr cs)) #f)))))))
(define-syntax test (syntax-rules (= > <) ((_ a = b) (equal? a b)) ((_ a > b) #{a > b}) ((_ a < b) #{a < b}) ((_ -z str) (= (string-length str) 0))))
(define-syntax seq (syntax-rules () ((_ n) (iota n))))
(define-syntax sh-pipe (syntax-rules (|) ((_ cmd1 | cmd2) (cmd2 cmd1))))
(define-syntax exit (syntax-rules () ((_ n) n)))
(define-syntax sh-true (syntax-rules () ((_) #t)))
(define-syntax sh-false (syntax-rules () ((_) #f)))
;; (sh-var x = 42) (echo "x=" x)
;; (sh-for i in (seq 3) do (echo #{i + 1}) done)
;; (sh-var n = 1) (sh-while #{n <= 3} do (echo n n) (set! n #{n + 1}) done)
;; (sh-if #{x > 0} then ("positive") else ("not") fi)
;; (echo (test 5 > 3))

(display "--- demos ---\n")
(define (sum-seq n)
  (sh-var total = 0)
  (sh-for i in (seq n) do
    (set! total (+ total #{i + 1}))
  done)
  (echo "sum 1.." n "=" total))
(sum-seq 5)

(define (countdown n)
  (sh-while #{n > 0} do
    (echo "countdown:" n)
    (set! n #{n - 1})
  done)
  (echo "blastoff!"))
(countdown 5)

(define (sign n)
  (if (> n 0) (echo "positive"))
  (if (< n 0) (echo "negative"))
  (if (zero? n) (echo "zero")))
(sign 5)
(sign -3)
(sign 0)

(display "--- demos ---\n")

;; --- 1. fizzbuzz ---
;; (define (fizzbuzz n)
;;   (sh-for i in (seq n) do
;;     (let ((i #{i + 1}))
;;       (sh-if (zero? #{i % 15}) then (echo "FizzBuzz")
;;         elif (zero? #{i % 3}) then (echo "Fizz")
;;         elif (zero? #{i % 5}) then (echo "Buzz")
;;         else (echo i)
;;       fi))
;;   done)
;; (fizzbuzz 15)

;; --- 2. factorial ---
;; (define (fact n)
;;   (sh-var result = 1)
;;   (sh-var i = 1)
;;   (sh-while (test i <= n) do
;;     (set! result #{result * i})
;;     (set! i #{i + 1})
;;   done)
;;   (echo n "! =" result))
;; (fact 6)

;; --- 3. sum evens ---
;; (define (sum-evens n)
;;   (sh-var total = 0)
;;   (sh-for i in (seq n) do
;;     (sh-if (zero? (modulo #{i + 1} 2)) then
;;     (set! total #{total + i + 1})
;;     fi)
;;   done)
;;   (echo "sum evens:" total))
;; (sum-evens 10)

;; --- 4. countdown ---
;; (define (countdown n)
;;   (sh-while (test n > 0) do
;;     (echo "T minus" n)
;;     (set! n #{n - 1})
;;   done)
;;   (echo "go!"))
;; (countdown 5)
