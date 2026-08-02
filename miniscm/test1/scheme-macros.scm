(define-syntax nth
  (syntax-rules ()
    ((_ n x ...)
     (list-ref (list x ...) n))))

(define-syntax if-not
  (syntax-rules ()
    ((_ cond then else)
     (if cond else then))))

(define-syntax stream-cons
  (syntax-rules ()
    ((_ head tail)
     (cons head (delay tail)))))

(define-syntax fluid-let
  (syntax-rules ()
    ((_ () body ...)
     (begin body ...))
    ((_ ((var val) . rest) body ...)
     (let ((saved var))
       (set! var val)
       (let ((result (fluid-let rest body ...)))
         (set! var saved)
         result)))))

(define-syntax receive
  (syntax-rules ()
    ((_ formals expression body ...)
     (call-with-values
       (lambda () expression)
       (lambda formals body ...)))))

(define-syntax with-values
  (syntax-rules ()
    ((_ producer consumer)
     (call-with-values (lambda () producer) consumer))))

(define-syntax assume
  (syntax-rules ()
    ((_ expr)
     (or expr (error "assume failed:" 'expr)))))

(define-syntax and-let*
  (syntax-rules ()
    ((_) #t)
    ((_ () body ...)
     (begin body ...))
    ((_ ((test) . rest) body ...)
     (if test (and-let* rest body ...) #f))
    ((_ ((var val) . rest) body ...)
     (let ((var val))
       (if var (and-let* rest body ...) #f)))
    ((_ (var . rest) body ...)
     (let ((var var))
       (if var (and-let* rest body ...) #f)))))

(define-syntax rec
  (syntax-rules ()
    ((_ (name . args) body ...)
     (letrec ((name (lambda args body ...))) name))))


(define-syntax do-ec
  (syntax-rules (if for)
    ((_ expr (if test) rest ...)
     (if test (do-ec expr rest ...)))
    ((_ expr (for var lst) rest ...)
     (for-each (lambda (var) (do-ec expr rest ...)) lst))
    ((_ expr (for var lst))
     (for-each (lambda (var) expr) lst))
    ((_ expr)
     expr)))

(define-syntax list-ec
  (syntax-rules (for if)
    ((_ expr)
     (list expr))
    ((_ expr (for var lst))
     (map (lambda (var) expr) lst))
    ((_ expr (if test))
     (if test (list expr) '()))
    ((_ expr (for var lst) (if test) more ...)
     (apply append
       (map (lambda (var)
              (if test
                  (list-ec expr more ...)
                  '()))
            lst)))
    ((_ expr (for var lst) more ...)
     (apply append
       (map (lambda (var)
              (list-ec expr more ...))
            lst)))))

(define-syntax sum-ec
  (syntax-rules (for if)
    ((_ expr (if test) more ...)
     (if test (sum-ec expr more ...) 0))
    ((_ expr (for var lst) more ...)
     (apply + 0 (list-ec expr (for var lst) more ...)))
    ((_ expr)
     expr)))

(define-syntax any?-ec
  (syntax-rules (for if)
    ((_ expr (for var lst) more ...)
     (any (lambda (var) (any?-ec expr more ...)) lst))
    ((_ expr (if test) more ...)
     (if test (any?-ec expr more ...) #f))
    ((_ expr)
     expr)))


(define-syntax every?-ec
  (syntax-rules (for if)
    ((_ expr (for var lst) more ...)
     (every (lambda (var) (every?-ec expr more ...)) lst))
    ((_ expr (if test) more ...)
     (if test (every?-ec expr more ...) #f))
    ((_ expr)
     expr)))


(define-syntax check
  (syntax-rules ()
    ((_ expr expected)
     (let ((actual expr) (exp expected))
       (if (equal? actual exp)
           (begin (display "  [CHECK PASS] ") (display 'expr) (newline))
           (begin (display "  [CHECK FAIL] ") (display 'expr) (newline)
                  (display "    expected: ") (write exp) (newline)
                  (display "    actual:   ") (write actual) (newline)))))))


(define-syntax check-ec
  (syntax-rules (for if)
    ((_ expected (for var lst) expr)
     (every?-ec (equal? expr expected) (for var lst)))
    ((_ expected (for var lst) (if test) expr)
     (every?-ec (equal? expr expected) (for var lst) (if test)))))

(define-syntax aif
  (syntax-rules ()
    ((_ test then else)
     (let ((it test))
       (if it then else)))))


(define-syntax aand
  (syntax-rules ()
    ((_) #t)
    ((_ expr) expr)
    ((_ expr . rest)
     (let ((it expr))
       (if it (aand . rest) it)))))


(define-syntax alet
  (syntax-rules ()
    ((_ ((var val) ...) body ...)
     (let ((var val) ...) body ...))))

(define-syntax test-assert
  (syntax-rules ()
    ((_ name expr)
     (let ((result expr))
       (if result
           (begin (display (string-append "[PASS] " name)) (newline))
           (begin (display (string-append "[FAIL] " name)) (newline)))
       result))))


(define-syntax test-equal
  (syntax-rules ()
    ((_ name expected actual)
     (let ((e expected) (a actual))
       (if (equal? a e)
           (begin (display (string-append "[PASS] " name)) (newline))
           (begin (display (string-append "[FAIL] " name)) (newline)
                  (display (string-append "  expected: " (with-output-to-string (lambda () (write e))))) (newline)
                  (display (string-append "  actual:   " (with-output-to-string (lambda () (write a))))) (newline)))))))


(define-syntax test-approximate
  (syntax-rules ()
    ((_ name expected actual epsilon)
     (let ((e expected) (a actual))
       (if (< (abs (- a e)) epsilon)
           (begin (display (string-append "[PASS] " name)) (newline))
           (begin (display (string-append "[FAIL] " name)) (newline)
                  (display "  expected: ") (display e) (display " ± ") (display epsilon) (newline)
                  (display "  actual:   ") (display a) (newline)))))))

(define-syntax define-immutable
  (syntax-rules ()
    ((_ (name . args) body ...)
     (define name (lambda args body ...)))))


(define-syntax dbind
  (syntax-rules ()
    ((_ () expr body ...)
     (begin body ...))
    ((_ (a) expr body ...)
     (let ((a expr)) body ...))
    ((_ (a b) expr body ...)
     (let ((tmp expr))
       (let ((a (car tmp)) (b (cadr tmp))) body ...)))
    ((_ (a b c) expr body ...)
     (let ((tmp expr))
       (let ((a (car tmp)) (b (cadr tmp)) (c (caddr tmp))) body ...)))
    ((_ (a . b) expr body ...)
     (let ((tmp expr))
       (let ((a (car tmp)) (b (cdr tmp))) body ...)))))

