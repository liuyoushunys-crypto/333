(display "=== lang-py loaded ===\n")
(define-syntax def (syntax-rules () ((_ name (args ...) body ...) (define name (lambda (args ...) body ...)))))
(define-syntax print (syntax-rules () ((_ x) (begin (display x) (newline)))))
(define-syntax return (syntax-rules () ((_ x) x)))
(define-syntax for-in (syntax-rules (in) ((_ var in lst body ...) (for-each (lambda (var) body ...) lst))))
(define-syntax while (syntax-rules () ((_ cond body ...) (let loop () (if cond (begin body ... (loop)) #f)))))
(define-syntax range (syntax-rules () ((_ n) (iota n)) ((_ s e) (iota #{e - s} s)) ((_ s e step) (iota (quotient #{e - s} step) s step))))
(define-syntax len (syntax-rules () ((_ x) (length x))))
(define-syntax str (syntax-rules () ((_ x) (if (number? x) (number->string x) (if (symbol? x) (symbol->string x) x)))))
(define-macro (list-comp expr for var in lst . rest) (if (and (not (null? rest)) (eq? (car rest) 'when)) `(filter (lambda (,var) ,(cadr rest)) (map (lambda (,var) ,expr) ,lst)) `(map (lambda (,var) ,expr) ,lst)))
(define-macro (try body . clauses) (let loop ((cs clauses)) (if (null? cs) `(begin ,@body) (let ((c (car cs))) (case (car c) ((except) (let ((var (cadr c)) (handler (cddr c))) `(guard (,var (else ,@handler)) (begin ,@body)))) ((else) (let ((cb (cdr c))) `(let ((r (begin ,@body))) (if (not r) (begin ,@cb) r)))) ((finally) (let ((cl (cdr c))) `(dynamic-wind (lambda () #f) (lambda () (begin ,@body)) (lambda () ,@cl)))) (else `(begin ,@body)))))))
(define-macro (f-string . parts) `(string-append ,@(map (lambda (p) (if (string? p) p `(format "~a" ,p))) parts)))
;; (def fact (n) (if #{n <= 1} 1 (* n (fact #{n - 1}))))
;; (for-in x in '(1 2 3) (print x))
;; (print (list-comp #{x * 2} for x in (range 5) when #{x > 0}))
;; (print (f-string "sum=" #{2 + 3}))
;; (print (len (range 10)))

(display "--- demos ---\n")
(display "Fibonacci sequence (for-in + list):\n")
(def fib (n)
  (define result '(0 1))
  (for-in i in (range 2 n)
    (let ((a (car (reverse result)))
          (b (cadr (reverse result))))
      (set! result (append result (list #{a + b})))))
  (return result))
(print (fib 15))

(display "Filter even via list-comp:\n")
(print (list-comp x for x in (range 20) when (zero? #{x % 2})))

(display "Try/except safe-divide:\n")
(define (py-safe-div x y)
  (try (return #{x / y})
    except (e) (return "error")))
(print (py-safe-div 10 3))
(print (py-safe-div 10 0))

(display "Sum 0..100 with while:\n")
(def sum-to (n)
  (define i 0)
  (define s 0)
  (while #{i < n}
    (set! i #{i + 1})
    (set! s #{s + i}))
  (return s))
(print (sum-to 100))

(display "FizzBuzz via range + manual filter:\n")
(for-in x in (range 1 16)
  (print (cond ((zero? #{x % 15}) "FizzBuzz")
               ((zero? #{x % 3}) "Fizz")
               ((zero? #{x % 5}) "Buzz")
               (else x))))

;; ═══════════════════════════════════════════════════════════════
;; 复合示例 — 加载后在 REPL 中逐段运行
;; (python3 miniscm.py) → (load "scm/lang-py.scm") 后粘贴以下块
;; ═══════════════════════════════════════════════════════════════

;; --- 1. 斐波那契: for-in + 列表累加 ---
;; (def fib (n)
;;   (define result '(0 1))
;;   (for-in i in (range 2 n)
;;     (let ((a (car (reverse result)))
;;           (b (cadr (reverse result))))
;;       (set! result (append result (list #{a + b})))))
;;   (return result))
;; (print (fib 15))

;; --- 2. 列表推导式筛选偶数 ---
;; (print (list-comp x for x in (range 0 20 2) when #{x > 0}))

;; --- 3. try/except 安全除法 ---
;; (def safe-div (x y)
;;   (try (return #{x / y})
;;     except (e) (return "error")))
;; (print (safe-div 10 3))   → 10/3
;; (print (safe-div 10 0))   → "error"

;; --- 4. while 累加 1..n ---
;; (def sum-to (n)
;;   (define i 0)
;;   (define s 0)
;;   (while #{i < n}
;;     (set! i #{i + 1})
;;     (set! s #{s + i}))
;;   (return s))
;; (print (sum-to 100))       → 5050

;; --- 5. FizzBuzz (cond + for-in) ---
;; (for-in x in (range 1 16)
;;   (print (cond ((zero? #{x % 15}) "FizzBuzz")
;;                ((zero? #{x % 3}) "Fizz")
;;                ((zero? #{x % 5}) "Buzz")
;;                (else x))))
