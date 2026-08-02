;; 04-with-syntax.scm — with-syntax 临时绑定

(define-syntax define-struct
  (lambda (stx)
    (syntax-case stx ()
      ((_ name (field ...))
       (with-syntax
         (((make-name ...)
           (map (lambda (f)
                  (datum->syntax #'name
                    (string->symbol
                      (string-append "make-" (symbol->string f)))))
                #'(field ...)))
          ((name? ...)
           (map (lambda (f)
                  (datum->syntax #'name
                    (string->symbol
                      (string-append (symbol->string f) "?"))))
                #'(field ...))))
         (syntax
           (begin
             (define name (lambda (field ...) (list field ...)))
             (define (make-name x) (list 'name x)) ...
             (define (name? x) (and (pair? x) (eq? (car x) 'name))) ...)))))))

(define-syntax hash-let
  (lambda (stx)
    (syntax-case stx ()
      ((_ ht ((key var) ...) body ...)
       (syntax (let ((var (hash-table-ref ht 'key)) ...) body ...))))))

(define-syntax with-file-lines
  (lambda (stx)
    (syntax-case stx ()
      ((_ (var filename) body ...)
       (syntax
         (let ((var (call-with-input-file filename
                      (lambda (p)
                        (let loop ((line (read-line p)) (lines '()))
                          (if (eof-object? line)
                            (reverse lines)
                            (loop (read-line p) (cons line lines))))))))
           body ...))))))

(define-syntax define-accessors
  (lambda (stx)
    (syntax-case stx ()
      ((_ (getter ...) vec)
       (with-syntax
         (((idx ...) (iota (length #'(getter ...)))))
         (syntax
           (begin
             (define (getter vec) (vector-ref vec idx)) ...)))))))

(define-syntax time-it
  (lambda (stx)
    (syntax-case stx ()
      ((_ expr)
       (syntax
         (let ((start (current-second)))
           (let ((val expr))
             (display "elapsed: ")
             (display (- (current-second) start))
             (newline)
             val)))))))
