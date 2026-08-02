(define (char->name c)
  (case c ((#\space) "space") ((#\newline) "newline") ((#\tab) "tab")
          ((#\return) "return") ((#\null) "null") ((#\alarm) "alarm")
          ((#\backspace) "backspace") ((#\escape) "escape") ((#\delete) "delete")
          (else (string c))))

(define (char-ready? . _) #t)
(define (u8-ready? . _) #t)

(define ascii?   (let ((l 128)) (lambda (c) (< (char->integer c) l))))
(define char-ascii? ascii?)

(define (char-control? c)
  (let ((n (char->integer c))) (or (< n 32) (= n 127))))
(define char-iso-control? char-control?)

;; ============= 布尔 =============
;; [commented: dup with boot-min] (define (atom? x) (not (pair? x)))
;; [commented: dup with boot-min] (define void-sentinel (void))
;; [commented: dup with boot-min] (define (void? x) (eq? x void-sentinel))

(unless (defined? 'symbol=?)
  (define (symbol=? . args)
    (or (null? args) (null? (cdr args))
        (and (eq? (car args) (cadr args)) (apply symbol=? (cdr args))))))

;; ============= 数值 =============
