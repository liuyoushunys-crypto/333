(define (vector-copy v . args)
  (let ((n (vector-length v))
        (s (if (pair? args) (car args) 0))
        (e (if (and (pair? args) (pair? (cdr args))) (cadr args) (vector-length v))))
    (let* ((len (- e s)) (r (make-vector len)))
      (do ((i 0 (+ i 1))) ((= i len) r)
        (vector-set! r i (vector-ref v (+ s i)))))))

(define (vector-append . vs)
  (let* ((n (fold-left (lambda (s v) (+ s (vector-length v))) 0 vs))
         (r (make-vector n)) (k 0))
    (do ((vs vs (cdr vs))) ((null? vs) r)
      (let ((v (car vs)))
        (do ((i 0 (+ i 1))) ((= i (vector-length v)))
          (vector-set! r (+ k i) (vector-ref v i)))
        (set! k (+ k (vector-length v)))))))

(define (vector-map f . vs)
  (let* ((n (let m ((l (map vector-length vs))) (if (null? (cdr l)) (car l) (min (car l) (m (cdr l))))))
         (r (make-vector n)))
    (do ((i 0 (+ i 1))) ((= i n) r)
      (vector-set! r i (apply f (map (lambda (v) (vector-ref v i)) vs))))))

(define (circular-list . args)
  (if (null? args) '()
      (let ((lst (list-copy (apply list args))))
        (set-cdr! (last-pair lst) lst) lst)))

(define (circular-list? x)
  (and (pair? x)
       (let race ((t x) (h (cdr x)))
         (if (not (pair? h)) #f
             (if (eq? t h) #t
                 (let ((h (cdr h)))
                   (if (not (pair? h)) #f
                       (if (eq? t h) #t
                           (race (cdr t) (cdr h))))))))))

(define (dotted-list? x)
  (not (or (null? x) (and (pair? x) (let lp ((x x))
    (or (null? x) (and (pair? x) (lp (cdr x)))))))))



(define (complement f)
  (lambda xs (not (apply f xs))))

(define not-pair? (complement pair?))

(define (default-object? x)
  (void? x))

;; 判断是否真列表（无环，且以 () 结尾）
(define (proper-list? x)
  (or (null? x)
      (and (pair? x)
           (let race ((t x) (h (cdr x)))
             (if (not (pair? h)) (null? h)
                 (if (eq? t h) #f
                     (let ((h (cdr h)))
                       (if (not (pair? h)) (null? h)
                           (if (eq? t h) #f
                               (race (cdr t) (cdr h)))))))))))

(define null-list? null?)

;; ============================================================
;; 第三部分：列表高阶操作
;; 替换 Python 的 fold-left/fold-right/filter/partition/find/any/every
;; ============================================================

(define (fold-left f init lst)
  (let loop ((acc init) (xs lst))
    (if (null? xs) acc
        (loop (f acc (car xs)) (cdr xs)))))

(define (fold-right f init lst)
  (fold-left (lambda (acc x) (f x acc)) init (reverse lst)))

(define (fold f init lst)
  (fold-left f init lst))

(define (reduce f init lst)
  (fold-left f init lst))


(define (find pred lst)
  (let loop ((xs lst))
    (cond ((null? xs) #f)
          ((pred (car xs)) (car xs))
          (else (loop (cdr xs))))))

(define (any pred lst)
  (let loop ((xs lst))
    (cond ((null? xs) #f)
          ((pred (car xs)) #t)
          (else (loop (cdr xs))))))

(define (every pred lst)
  (let loop ((xs lst))
    (cond ((null? xs) #t)
          ((not (pred (car xs))) #f)
          (else (loop (cdr xs))))))

;; ============================================================
;; 第四部分：列表成员与关联（替换 memq/memv/member/assq/assv/assoc）
;; ============================================================

(unless (defined? 'memq)
  (define (memq obj lst)
    (cond ((null? lst) #f)
          ((eq? obj (car lst)) lst)
          (else (memq obj (cdr lst))))))

(unless (defined? 'memv)
  (define (memv obj lst)
    (cond ((null? lst) #f)
          ((eqv? obj (car lst)) lst)
          (else (memv obj (cdr lst))))))

(unless (defined? 'member)
  (define (member obj lst . opt)
    (let ((cmp (if (null? opt) equal? (car opt))))
      (let loop ((xs lst))
        (cond ((null? xs) #f)
              ((cmp obj (car xs)) xs)
              (else (loop (cdr xs))))))))

(unless (defined? 'assq)
  (define (assq obj alist)
    (cond ((null? alist) #f)
          ((and (pair? (car alist)) (eq? obj (caar alist))) (car alist))
          (else (assq obj (cdr alist))))))

(unless (defined? 'assv)
  (define (assv obj alist)
    (cond ((null? alist) #f)
          ((and (pair? (car alist)) (eqv? obj (caar alist))) (car alist))
          (else (assv obj (cdr alist))))))

(unless (defined? 'assoc)
  (define (assoc obj alist . opt)
    (let ((cmp (if (null? opt) equal? (car opt))))
      (let loop ((xs alist))
        (cond ((null? xs) #f)
              ((and (pair? (car xs)) (cmp obj (caar xs))) (car xs))
              (else (loop (cdr xs))))))))

;; ============================================================
;; 第五部分：列表构造与变换
;; 替换 make-list/list-tabulate/iota/zip/cons*/list-copy/list-set!
;; ============================================================

(define (list-tabulate n f)
  (let loop ((i (- n 1)) (acc '()))
    (if (< i 0) acc
        (loop (- i 1) (cons (f i) acc)))))

(define (zip . lists)
  (let loop ((lss lists) (acc '()))
    (if (or (null? lss) (null? (car lss))) (reverse acc)
        (loop (map cdr lss)
              (cons (map car lss) acc)))))

(define (flat-map f lst)
  (let loop ((xs lst) (acc '()))
    (if (null? xs) (reverse acc)
        (loop (cdr xs) (append (reverse (f (car xs))) acc)))))

(define (append-map f lst)
  (flat-map f lst))

(define (list* first . rest)
  (apply cons* first rest))

(define (list-set! lst k val)
  (if (null? lst) (error "list-set!: index out of range")
      (if (= k 0) (set-car! lst val)
          (list-set! (cdr lst) (- k 1) val))))

;; 尾递归的 list-head（PRELUDE 已有但非尾递归）
(define (list-head lst n)
  (let loop ((xs lst) (k n) (acc '()))
    (if (<= k 0) (reverse acc)
        (loop (cdr xs) (- k 1) (cons (car xs) acc)))))

;; ============================================================
;; 第六部分：列表切片（take/drop/span/break/split-at）
;; ============================================================

(define (take lst n)
  (let loop ((xs lst) (k n) (acc '()))
    (if (<= k 0) (reverse acc)
        (loop (cdr xs) (- k 1) (cons (car xs) acc)))))

(define (drop lst n)
  (let loop ((xs lst) (k n))
    (if (<= k 0) xs
        (loop (cdr xs) (- k 1)))))


(define (take-while pred lst)
  (let loop ((xs lst) (acc '()))
    (if (or (null? xs) (not (pred (car xs))))
        (reverse acc)
        (loop (cdr xs) (cons (car xs) acc)))))

(define (drop-while pred lst)
  (let loop ((xs lst))
    (if (or (null? xs) (not (pred (car xs)))) xs
        (loop (cdr xs)))))



(define (take-right lst n)
  (drop lst (max 0 (- (length lst) n))))

(define (drop-right lst n)
  (let ((len (length lst)))
    (take lst (max 0 (- len n)))))

;; ============================================================
;; 第七部分：删除操作与去重
;; 替换 delete/delete-duplicates
;; ============================================================

(define (delete x lst . opt)
  (let ((cmp (if (null? opt) equal? (car opt))))
    (filter (lambda (y) (not (cmp x y))) lst)))

(define (delete-duplicates lst . opt)
  (let ((cmp (if (null? opt) equal? (car opt))))
    (let loop ((xs lst) (acc '()))
      (if (null? xs) (reverse acc)
          (let ((x (car xs)))
            (if (find (lambda (s) (cmp x s)) acc)
                (loop (cdr xs) acc)
                (loop (cdr xs) (cons x acc))))))))

;; ============================================================
;; 第八部分：归并排序与快速排序（替换 list-sort/vector-sort）
;; ============================================================

(define (list-sort less? seq)
  (letrec ((merge (lambda (a b)
                    (cond ((null? a) b)
                          ((null? b) a)
                          ((less? (car a) (car b))
                           (cons (car a) (merge (cdr a) b)))
                          (else (cons (car b) (merge a (cdr b)))))))
           (msort (lambda (l n)
                    (if (or (null? l) (null? (cdr l))) l
                        (let* ((mid (quotient n 2))
                               (right (drop l mid)))
                          (merge (msort (take l mid) mid)
                                 (msort right (- n mid))))))))
    (msort seq (length seq))))

(define list-stable-sort list-sort)
(define (sort a b)
  (if (procedure? a) (list-sort a b) (list-sort b a)))

(define (vector-sort pred vec)
  (list->vector (list-sort pred (vector->list vec))))

(define (sorted? pred lst)
  (or (null? lst) (null? (cdr lst))
      (and (pred (car lst) (cadr lst))
           (sorted? pred (cdr lst)))))

(define (merge pred a b)
  (let loop ((a a) (b b) (acc '()))
    (cond ((null? a) (append (reverse acc) b))
          ((null? b) (append (reverse acc) a))
          ((pred (car a) (car b))
           (loop (cdr a) b (cons (car a) acc)))
          (else (loop a (cdr b) (cons (car b) acc))))))

;; ============================================================
