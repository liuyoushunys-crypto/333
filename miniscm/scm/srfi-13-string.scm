;; 第九部分：字符串操作（替换 Python 的 string builtins）
;; ============================================================

(define (string-foldcase s)
  (let* ((len (string-length s))
         (r (make-string len)))
    (do ((i 0 (+ i 1)))
        ((>= i len) r)
      (string-set! r i (char-foldcase (string-ref s i))))))

(define (string-map f s . more)
  (let* ((len (string-length s))
         (r (make-string len)))
    (do ((i 0 (+ i 1)))
        ((>= i len) r)
      (string-set! r i (apply f (string-ref s i)
                               (map (lambda (str) (string-ref str i)) more))))))

(define (string-for-each f s . more)
  (let ((len (string-length s)))
    (do ((i 0 (+ i 1)))
        ((>= i len))
      (apply f (string-ref s i)
             (map (lambda (str) (string-ref str i)) more)))))

(define (string->vector s . args)
  (let* ((start (if (null? args) 0 (car args)))
         (end   (if (or (null? args) (null? (cdr args))) (string-length s) (cadr args)))
         (len   (- end start))
         (v     (make-vector len)))
    (do ((i 0 (+ i 1)))
        ((>= i len) v)
      (vector-set! v i (string-ref s (+ start i))))))

(define (vector->string v . args)
  (let* ((start (if (null? args) 0 (car args)))
         (end   (if (or (null? args) (null? (cdr args))) (vector-length v) (cadr args)))
         (len   (- end start))
         (r     (make-string len)))
    (do ((i 0 (+ i 1)))
        ((>= i len) r)
      (string-set! r i (vector-ref v (+ start i))))))

;; string-any / string-every：字符串中的量词
(define (string-any pred str . args)
  (let* ((start (if (null? args) 0 (car args)))
         (end   (if (or (null? args) (null? (cdr args))) (string-length str) (cadr args))))
    (let loop ((i start))
      (if (>= i end) #f
          (let ((r (pred (string-ref str i))))
            (if r r (loop (+ i 1))))))))

(define (string-every pred str . args)
  (let* ((start (if (null? args) 0 (car args)))
         (end   (if (or (null? args) (null? (cdr args))) (string-length str) (cadr args))))
    (let loop ((i start) (last #t))
      (if (>= i end) last
          (let ((r (pred (string-ref str i))))
            (if r (loop (+ i 1) r) #f))))))

(define (string-join lst . opt)
  (let ((sep (if (null? opt) " " (car opt))))
    (if (null? lst) ""
        (let loop ((xs (cdr lst)) (acc (car lst)))
          (if (null? xs) acc
              (loop (cdr xs) (string-append acc sep (car xs))))))))

(define (string-split s . opt)
  (let* ((sep (if (null? opt) " " (if (char? (car opt)) (string (car opt)) (car opt))))
         (len (string-length s))
         (sep-len (string-length sep)))
    (if (zero? sep-len)
        (error "string-split: empty separator")
        (let loop ((i 0) (acc '()))
          (let ((idx (string-contains s sep i)))
            (if idx
                (loop (+ idx sep-len)
                      (cons (substring s i idx) acc))
                (reverse (cons (substring s i len) acc))))))))

(define (string-trim s . opt)
  (let ((pred (if (null? opt) char-whitespace? (if (char? (car opt)) (lambda (c) (char=? c (car opt))) (car opt))))
        (n (string-length s)))
    (let ((start (let loop ((i 0))
                   (cond ((>= i n) n)
                         ((pred (string-ref s i)) (loop (+ i 1)))
                         (else i)))))
      (if (>= start n) ""
          (let ((end (let lp ((k (- n 1)))
                       (if (pred (string-ref s k))
                           (lp (- k 1))
                           k))))
            (substring s start (+ end 1)))))))

(define (string-trim-right s . opt)
  (let ((pred (if (null? opt) char-whitespace? (if (char? (car opt)) (lambda (c) (char=? c (car opt))) (car opt)))))
    (let ((n (string-length s)))
      (let loop ((i (- n 1)))
        (if (< i 0) ""
            (if (pred (string-ref s i))
                (loop (- i 1))
                (substring s 0 (+ i 1))))))))

(define (string-trim-both s . opt)
  (let ((pred (if (null? opt) char-whitespace? (if (char? (car opt)) (lambda (c) (char=? c (car opt))) (car opt))))
        (n (string-length s)))
    (let ((start (let loop ((i 0))
                   (cond ((>= i n) n)
                         ((pred (string-ref s i)) (loop (+ i 1)))
                         (else i)))))
      (if (>= start n) ""
          (let ((end (let loop ((k (- n 1)))
                       (if (pred (string-ref s k)) (loop (- k 1)) k))))
            (substring s start (+ end 1)))))))

(define (string-prefix? pre s)
  (let ((n (string-length pre))
        (m (string-length s)))
    (and (<= n m) (string=? (substring s 0 n) pre))))

(define (string-suffix? suf s)
  (let ((n (string-length suf))
        (m (string-length s)))
    (and (<= n m) (string=? (substring s (- m n) m) suf))))

(define (string-contains s sub . opt)
  (let ((start (if (null? opt) 0 (car opt)))
        (n (string-length s))
        (m (string-length sub)))
    (let loop ((i start))
      (if (> i (- n m)) #f
          (if (string=? (substring s i (+ i m)) sub) i
              (loop (+ i 1)))))))

;; bitwise 别名（指向 Python builtin，性能更高）
(define bitwise-ior bit-or)
(define bitwise-or  bit-or)
(define bitwise-and bit-and)
(define bitwise-xor bit-xor)
(define bitwise-not bit-not)
(define logand bitwise-and)
(define logior bitwise-ior)
(define logxor bitwise-xor)
(define lognot bitwise-not)
(define (bitwise-arithmetic-shift-right n count) (arithmetic-shift n (- count)))

;; 无需 Python builtin 的保留纯 Scheme 实现


