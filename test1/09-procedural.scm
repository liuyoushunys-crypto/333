;; 09-procedural.scm — 过程式 syntax transformer

(define-syntax identity-macro
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr) (syntax expr)))))

(define-syntax debug-write
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr)
       (begin
         (display ";; expanding: ") (display (syntax->datum #'expr)) (newline)
         (syntax expr))))))

(define-syntax capture-raw
  (lambda (stx)
    (syntax-case stx ()
      ((_ . rest)
       (syntax (quote (syntax->datum #'(rest))))))))

(define-syntax wrap-in-list
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr ...)
       (syntax (quote (expr ...)))))))

(define-syntax eval-at-macro-time
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr)
       (let ((val (eval (syntax->datum #'expr))))
         (syntax (quote val)))))))

(define-syntax make-resolver
  (lambda (stx)
    (syntax-case stx ()
      ((_ name table)
       (let* ((tbl (syntax->datum #'table))
              (keys (map car tbl)))
         (with-syntax (((k ...) (map (lambda (k) (datum->syntax #'name k)) keys)))
           (syntax
             (lambda (key)
               (case key
                 ((k ...)
                  (apply (lambda (x) (error "not found")) '()))
                 (else #f))))))))))

(define-syntax trace-calls
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr)
       (let ((datum (syntax->datum #'expr)))
         (display ";; traced: ") (display datum) (newline)
         (syntax expr))))))

(identity-macro (+ 1 2))
(debug-write (* 3 4))
(capture-raw (+ 1 2 3))
(wrap-in-list a b c)
(eval-at-macro-time (+ 1 2 3))
(trace-calls (list 1 2 3))
