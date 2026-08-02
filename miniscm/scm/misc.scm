;; SRFI-60: 位操作别名与补充
;; ============================================================

(define bitwise-bit-field bit-field)
(define bitwise-copy-bit copy-bit)

(define (bitwise-if mask n0 n1)
  (bitwise-ior (bit-and mask n0)
               (bit-and (bit-not mask) n1)))

(define (bitwise-merge mask n0 n1)
  (bitwise-if mask n0 n1))

(define (bitwise-copy-bit-field n start end new)
  (let* ((width (- end start))
         (mask (bit-not (arithmetic-shift -1 width))))
    (bitwise-if (arithmetic-shift mask start)
                (arithmetic-shift (bit-and new mask) start)
                n)))

(define (bitwise-rotate-bit-field n count start end)
  (let* ((width (- end start))
         (mask (- (arithmetic-shift 1 width) 1))
         (field (bit-and (arithmetic-shift n (- start)) mask))
         (c (modulo count width))
         (rot (bit-and (bitwise-ior (arithmetic-shift field c)
                                    (arithmetic-shift field (- c width)))
                       mask)))
    (bitwise-if (arithmetic-shift mask start)
                (arithmetic-shift rot start)
                n)))


(define (integer->booleans n)
  (let loop ((n n) (r '()))
    (if (zero? n) (if (null? r) '(#f) (reverse r))
        (loop (quotient n 2) (cons (odd? n) r)))))

;; ===================== SRFI-13 补充 =====================
(define (string-concatenate strs)
  (apply string-append strs))

(define (string-take s n)   (substring s 0 n))
(define (string-drop s n)   (substring s n (string-length s)))
(define (string-take-right s n) (substring s (- (string-length s) n) (string-length s)))
(define (string-drop-right s n) (substring s 0 (- (string-length s) n)))

(define (string-skip s pred . args)
  (let ((start (if (null? args) 0 (car args)))
        (n (string-length s)))
    (let loop ((i start))
      (cond ((>= i n) #f)
            ((not (pred (string-ref s i))) i)
            (else (loop (+ i 1)))))))

;; ===================== SRFI-60 补充 =====================
(define (arithmetic-shift-right n count)
  (arithmetic-shift n (- count)))



(define (string-remove pred s)
  (let ((n (string-length s)))
    (let loop ((i 0) (chars '()))
      (if (>= i n) (list->string (reverse chars))
          (let ((c (string-ref s i)))
            (if (pred c) (loop (+ i 1) chars)
                (loop (+ i 1) (cons c chars))))))))

(define (string-filter pred s)
  (let ((n (string-length s)))
    (let loop ((i 0) (chars '()))
      (if (>= i n) (list->string (reverse chars))
          (let ((c (string-ref s i)))
            (if (pred c) (loop (+ i 1) (cons c chars))
                (loop (+ i 1) chars)))))))

(define (range start end . step)
  (let ((s (if (null? step) 1 (car step))))
    (let loop ((i start) (r '()))
      (if (>= i end) (reverse r) (loop (+ i s) (cons i r))))))

;; ============================================================
;; Round 1: SRFI 补充（尾递归）
;; ============================================================
(define (string->char-set s)
  (let ((v (make-vector 256 #f)))
    (do ((i 0 (+ i 1))) ((= i (string-length s)) v)
      (vector-set! v (char->integer (string-ref s i)) #t))))
;; ============================================================
;; Round 1: SRFI 补充（尾递归）
;; ============================================================

;; ---------- SRFI-1: 列表 ----------
(define (alist-cons key val alist)
  (cons (cons key val) alist))

(define (alist-delete key alist . compare)
  (let ((cmp (if (null? compare) equal? (car compare))))
    (filter (lambda (p) (not (cmp key (car p)))) alist)))

(define (filter-map f . lists)
  (let loop ((ls (apply map list lists)) (r '()))
    (if (null? ls) (reverse r)
        (let ((val (apply f (car ls))))
          (if val (loop (cdr ls) (cons val r))
              (loop (cdr ls) r))))))

(define (map-in-order f . lists)
  (apply map f lists))

(define (pair-for-each f lst)
  (let loop ((l lst))
    (unless (null? l)
      (f l)
      (loop (cdr l)))))

(define (reduce-right f ridentity lst)
  (fold-right f ridentity lst))

(define (unfold-right p f g seed . tail)
  (let loop ((s seed) (r (if (null? tail) '() (car tail))))
    (if (p s) r
        (loop (g s) (cons (f s) r)))))

;; ---------- SRFI-13: 字符串 ----------
(define (string-delete pred s) (string-remove pred s))
(define (string-replace s1 s2 start1 end1 . maybe)
  (let ((start2 (if (null? maybe) 0 (car maybe)))
        (end2 (if (or (null? maybe) (null? (cdr maybe))) (string-length s2) (cadr maybe))))
    (string-append (substring s1 0 start1)
                   (substring s2 start2 end2)
                   (substring s1 end1 (string-length s1)))))

(define (string-tokenize s . token-set)
  (let ((cs (if (null? token-set) char-set:whitespace (car token-set))))
    (let ((n (string-length s)))
      (let loop ((i 0) (r '()))
        (cond ((>= i n) (reverse r))
              ((char-set-contains? cs (string-ref s i))
               (loop (+ i 1) r))
              (else
               (let ((end (let scan ((j i))
                            (if (or (>= j n) (char-set-contains? cs (string-ref s j)))
                                j
                                (scan (+ j 1))))))
                 (loop end (cons (substring s i end) r)))))))))

(define (string-fold kons knil s . maybe)
  (let ((n (string-length s)))
    (let loop ((i 0) (acc knil))
      (if (= i n) acc
          (loop (+ i 1) (kons (string-ref s i) acc))))))

(define (string-fold-right kons knil s)
  (let loop ((i (- (string-length s) 1)) (acc knil))
    (if (< i 0) acc
        (loop (- i 1) (kons (string-ref s i) acc)))))

(define (string-for-each-index proc s)
  (let ((n (string-length s)))
    (do ((i 0 (+ i 1))) ((= i n))
      (proc i))))

(define (string-xcopy! target tstart s . args)
  (let ((sstart (if (null? args) 0 (car args)))
        (send (if (or (null? args) (null? (cdr args))) (string-length s) (cadr args))))
    (do ((i sstart (+ i 1))) ((= i send))
      (string-set! target (+ tstart (- i sstart)) (string-ref s i)))))

(define (string-index-right s pred . args)
  (let ((start (if (null? args) (- (string-length s) 1) (car args))))
    (let loop ((i start))
      (cond ((< i 0) #f)
            ((pred (string-ref s i)) i)
            (else (loop (- i 1)))))))

(define (string-skip-right s pred . args)
  (let ((start (if (null? args) (- (string-length s) 1) (car args))))
    (let loop ((i start))
      (cond ((< i 0) #f)
            ((not (pred (string-ref s i))) i)
            (else (loop (- i 1)))))))

(define (string-prefix-length s1 s2)
  (let ((n (min (string-length s1) (string-length s2))))
    (let loop ((i 0))
      (if (or (= i n) (not (char=? (string-ref s1 i) (string-ref s2 i)))) i
          (loop (+ i 1))))))

(define (string-suffix-length s1 s2)
  (let ((n1 (string-length s1)) (n2 (string-length s2)))
    (let loop ((i 0))
      (if (or (>= i (min n1 n2))
              (not (char=? (string-ref s1 (- n1 i 1)) (string-ref s2 (- n2 i 1)))))
          i
          (loop (+ i 1))))))

;; ---------- SRFI-14: 字符集 ----------
(define (char-set=? . css)
  (or (null? css)
      (let loop ((a (car css)) (rest (cdr css)))
        (or (null? rest)
            (and (let iloop ((i 0))
                   (or (= i 256)
                       (and (eq? (vector-ref a i) (vector-ref (car rest) i))
                            (iloop (+ i 1)))))
                 (loop (car rest) (cdr rest)))))))

(define (char-set-hash cs . bound)
  (let ((b (if (null? bound) 65536 (car bound)))
        (h 0))
    (do ((i 0 (+ i 1))) ((= i 256) (modulo h b))
      (when (vector-ref cs i)
        (set! h (+ (* h 41) i))))))

(define char-set:empty (make-vector 256 #f))
(define char-set:full
  (let ((v (make-vector 256 #f)))
    (do ((i 0 (+ i 1))) ((= i 256) v) (vector-set! v i #t))))

(define char-set:graphic
  (char-set-union char-set:letter char-set:digit char-set:punctuation))

(define char-set:printing
  (let ((v (make-vector 256 #f)))
    (do ((i 32 (+ i 1))) ((> i 126) v) (vector-set! v i #t))))

(define char-set:symbol
  (let ((v (make-vector 256 #f)))
    (for-each (lambda (c) (vector-set! v (char->integer c) #t))
              (string->list "!$%&*+-./:<=>?@^_~"))
    v))

(define char-set:hex-digit
  (char-set-union char-set:digit
    (string->char-set "ABCDEFabcdef")))

(define char-set:blank
  (string->char-set " \t"))

(define char-set:iso-control
  (let ((v (make-vector 256 #f)))
    (do ((i 0 (+ i 1))) ((> i 31) v) (vector-set! v i #t))
    (vector-set! v 127 #t)
    v))

(define (string-unfold p f g seed . tail)
  (let ((base (if (null? tail) "" (car tail))))
    (let loop ((s seed) (chars '()))
      (if (p s) (string-append base (list->string (reverse chars)))
          (loop (g s) (cons (f s) chars))))))

;; ---------- SRFI-43: 向量 ----------
(define (reverse-list->vector lst)
  (list->vector (reverse lst)))

(define (vector= elt=? . vecs)
  (or (null? vecs)
      (let loop ((a (car vecs)) (b (cdr vecs)))
        (or (null? b)
            (let ((n (vector-length a)))
              (and (= n (vector-length (car b)))
                   (let iloop ((i 0))
                     (or (= i n)
                         (and (elt=? (vector-ref a i) (vector-ref (car b) i))
                              (iloop (+ i 1)))))
                   (loop (car b) (cdr b))))))))

(define (vector-cumulate f knil v)
  (let* ((n (vector-length v))
         (r (make-vector n)))
    (let loop ((i 0) (acc knil))
      (if (= i n) r
          (let ((new (f acc (vector-ref v i))))
            (vector-set! r i new)
            (loop (+ i 1) new))))))

(define (vector-index-right pred v . args)
  (let ((start (if (null? args) (- (vector-length v) 1) (car args))))
    (let loop ((i start))
      (cond ((< i 0) #f)
            ((pred (vector-ref v i)) i)
            (else (loop (- i 1)))))))

(define (vector-skip-right pred v . args)
  (let ((start (if (null? args) (- (vector-length v) 1) (car args))))
    (let loop ((i start))
      (if (< i 0) #f
          (let ((x (vector-ref v i)))
            (if (pred x) (loop (- i 1)) i))))))

(define (vector-append-subvectors . args)
  (if (null? args) (vector)
      (let* ((pairs (let lp ((a args) (r '()))
                       (if (null? a) (reverse r)
                           (let ((v (car a)) (start (cadr a)) (end (caddr a)))
                             (lp (cdddr a) (cons (list v start end) r))))))
             (total (fold-left (lambda (s p) (+ s (- (caddr p) (cadr p)))) 0 pairs))
             (r (make-vector total))
             (pos 0))
        (for-each (lambda (p)
                    (let ((v (car p)) (start (cadr p)) (end (caddr p)))
                      (do ((i 0 (+ i 1))
                           (j start (+ j 1)))
                          ((>= j end))
                        (vector-set! r (+ pos i) (vector-ref v j)))
                      (set! pos (+ pos (- end start)))))
                  pairs)
        r)))

;; ---------- SRFI-1: 集合 ----------
(define (lset-union = . lists)
  (if (null? lists) '()
      (let loop ((acc (car lists)) (more (cdr lists)))
        (if (null? more) acc
            (loop (fold-left (lambda (a x) (if (any (lambda (y) (= x y)) a) a (append a (list x))))
                             acc (car more))
                  (cdr more))))))

(define (lset-intersection = list1 . lists)
  (if (null? lists) list1
      (fold-left (lambda (acc lst)
                   (filter (lambda (x) (any (lambda (y) (= x y)) lst)) acc))
                 list1 lists)))

(define (lset-difference = list1 . lists)
  (if (null? lists) list1
      (filter (lambda (x)
                (not (any (lambda (lst) (any (lambda (y) (= x y)) lst)) lists)))
              list1)))

(define (lset-xor = . lists)
  (if (null? lists) '()
      (let loop ((acc (car lists)) (more (cdr lists)))
        (if (null? more) acc
            (loop (append (filter (lambda (x) (not (any (lambda (y) (= x y)) (car more)))) acc)
                          (filter (lambda (x) (not (any (lambda (y) (= x y)) acc))) (car more)))
                  (cdr more))))))

(define (lset-=? = . lists)
  (or (null? lists) (null? (cdr lists))
      (and (lset-union = (car lists) (cadr lists))
           (= (length (car lists)) (length (cadr lists)))
           (= (length (lset-union = (car lists) (cadr lists))) (length (car lists)))
           (apply lset-=? = (cdr lists)))))

;; ============================================================
;; Round 2: SRFI 补充续（尾递归）
;; ============================================================

;; ---------- SRFI-14 字符集：操作 ----------
(define (char-set-filter pred cs . basis)
  (let ((src (if (null? basis) cs (car basis))))
    (let ((r (make-vector 256 #f)))
      (do ((i 0 (+ i 1))) ((= i 256) r)
        (when (and (vector-ref src i) (pred (integer->char i)))
          (vector-set! r i #t))))))

(define (char-set-fold kons knil cs)
  (let loop ((i 0) (acc knil))
    (if (= i 256) acc
        (loop (+ i 1)
              (if (vector-ref cs i)
                  (kons acc (integer->char i))
                  acc)))))

(define (char-set-for-each proc cs)
  (do ((i 0 (+ i 1))) ((= i 256))
    (when (vector-ref cs i) (proc (integer->char i)))))

(define (char-set-map proc cs)
  (let ((r (make-vector 256 #f)))
    (do ((i 0 (+ i 1))) ((= i 256) r)
      (vector-set! r i (and (proc (integer->char i)) #t)))))

(define (ucs-range->char-set lower upper . rest)
  (let ((error? (if (null? rest) #f (car rest))))
    (let ((r (make-vector 256 #f)))
      (do ((i lower (+ i 1))) ((>= i (min upper 256)) r)
        (when (or (not error?) (integer->char i))
          (vector-set! r i #t))))))

;; ---------- SRFI-13：大小写不敏感 ----------
(define (string-prefix-length-ci s1 s2)
  (let ((n (min (string-length s1) (string-length s2))))
    (let loop ((i 0))
      (if (or (= i n)
              (not (char-ci=? (string-ref s1 i) (string-ref s2 i))))
          i
          (loop (+ i 1))))))

(define (string-suffix-length-ci s1 s2)
  (let ((n1 (string-length s1)) (n2 (string-length s2)))
    (let loop ((i 0))
      (if (or (>= i (min n1 n2))
              (not (char-ci=? (string-ref s1 (- n1 i 1))
                               (string-ref s2 (- n2 i 1)))))
          i
          (loop (+ i 1))))))

;; ---------- 生成器补充 (SRFI-158) ----------
(define (generator-drop n gen)
  (do ((i 0 (+ i 1))) ((= i n)) (gen))
  gen)

(define (generator-map f gen)
  (lambda ()
    (let ((v (gen)))
      (if (eof-object? v) (eof-object)
          (f v)))))

(define (generator-fold f init gen)
  (let loop ((acc init))
    (let ((v (gen)))
      (if (eof-object? v) acc
          (loop (f v acc))))))

;; ---------- 列表：额外 ----------
(define (take! lst n)
  (if (= n 0) '()
      (let lp ((l lst) (k (- n 1)))
        (if (= k 0) (begin (set-cdr! l '()) lst)
            (lp (cdr l) (- k 1))))))

(define (drop! lst n)
  (let lp ((l lst) (k n))
    (if (= k 0) l (lp (cdr l) (- k 1)))))

(define (filter! pred lst)
  (filter pred lst))

;; ---------- 框架 ----------
(define (symbolic-append . args)
  (string->symbol (apply string-append (map symbol->string args))))

(define (interleave . lists)
  (let loop ((ls lists) (r '()))
    (if (or (null? ls) (null? (car ls))) (reverse r)
        (loop (map cdr ls)
              (append (reverse (map car ls)) r)))))



(define string-reverse  (compose list->string reverse string->list))
(define vector-empty?   (compose zero? vector-length))

(define (unzip4 lst)
  (values (map car lst) (map cadr lst) (map caddr lst) (map cadddr lst)))

(define (unzip5 lst)
  (values (map car lst) (map cadr lst) (map caddr lst) (map cadddr lst)
          (map (lambda (x) (car (cddddr x))) lst)))
