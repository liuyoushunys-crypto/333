;; 07-hygiene.scm — syntax-case 卫生宏

(define-syntax let-it
  (lambda (stx)
    (syntax-case stx ()
      ((_ val body ...)
       (syntax (let ((it val)) body ...))))))

(define-syntax anaphoric-if
  (lambda (stx)
    (syntax-case stx ()
      ((_ test then else)
       (syntax (let ((it test)) (if it then else)))))))

(define-syntax define-unhygienic
  (lambda (stx)
    (syntax-case stx ()
      ((_ name val)
       (quasisyntax
         (define name (unsyntax (datum->syntax #'name (quote val)))))))))

(define-syntax with-temp
  (lambda (stx)
    (syntax-case stx ()
      ((_ body ...)
       (syntax
         (let ((temp (make-string 0)))
           (dynamic-wind
             (lambda () (set! temp (string-copy "tmp")))
             (lambda () body ...)
             (lambda () (set! temp #f)))))))))

(define-syntax define-private
  (lambda (stx)
    (syntax-case stx ()
      ((_ name val)
       (with-syntax ((hidden (datum->syntax #'name
                                (string->symbol
                                  (string-append "%" (symbol->string (syntax->datum #'name)))))))
         (syntax (define hidden val)))))))

(define-syntax rename
  (lambda (stx)
    (syntax-case stx ()
      ((_ (orig new) body ...)
       (syntax
         (let-syntax ((new (lambda (stx)
                             (syntax-case stx ()
                               ((_ args ...)
                                (syntax (orig args ...)))))))
           body ...))))))

(let-it (* 2 3) (display it) (newline))
(anaphoric-if (> 3 1) (display it) (display "no"))
(with-temp (display "in dynamic-wind"))
(define-private secret 42)
