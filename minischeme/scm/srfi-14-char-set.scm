(define (char-set->string cs)
  (let loop ((i 0) (r '()))
    (if (>= i 256) (list->string (reverse r))
        (loop (+ i 1) (if (vector-ref cs i)
                          (cons (integer->char i) r)
                          r)))))

(define (char-set-count cs)
  (let loop ((i 0) (n 0))
    (if (= i 256) n
        (loop (+ i 1) (if (vector-ref cs i) (+ n 1) n)))))

(define (char-set-any pred cs)
  (let loop ((i 0))
    (and (< i 256)
         (let ((c (integer->char i)))
           (or (and (vector-ref cs i) (pred c) c)
               (loop (+ i 1)))))))

(define (char-set-every pred cs)
  (let loop ((i 0))
    (or (= i 256)
        (if (vector-ref cs i)
            (and (pred (integer->char i)) (loop (+ i 1)))
            (loop (+ i 1))))))

(define (char-set-xor . css)
  (let ((r (make-vector 256 #f)))
    (do ((css css (cdr css))) ((null? css) r)
      (let ((cs (car css)))
        (do ((i 0 (+ i 1))) ((= i 256))
          (when (vector-ref cs i)
            (vector-set! r i (not (vector-ref r i)))))))))

;; SRFI-1: 补充

(define (length+ lst)
  (if (null? lst) 0
      (if (pair? lst)
          (let loop ((lst lst) (n 0))
            (cond ((null? lst) n)
                  ((pair? lst) (loop (cdr lst) (+ n 1)))
                  (else #f)))
          #f)))

(define (list= elt=? . lists)
  (or (null? lists)
      (let loop ((a (car lists)) (b (cdr lists)))
        (or (null? b)
            (and (= (length a) (length (car b)))
                 (let iloop ((la a) (lb (car b)))
                   (or (null? la)
                       (and (elt=? (car la) (car lb))
                            (iloop (cdr la) (cdr lb)))))
                 (loop (car b) (cdr b)))))))

;; SRFI-14: 字符集常量与操作
