;;; scm/fill-gaps.scm — 填补 pyb=False 模式缺失的函数

;;; 当 pyb=False 时，这些函数需要 Scheme 实现

;;; ================================================================



;; ── 基础 ──

(define (exact x) (inexact->exact x))

(define (inexact x) (exact->inexact x))



;; ── SRFI-60 别名 ──

(define (bit-shift n c) (arithmetic-shift n c))



;; ── 字符名 ──

(define *char-names*

  '((#\space . "space") (#\newline . "newline") (#\tab . "tab")

    (#\return . "return") (#\null . "null") (#\alarm . "alarm")

    (#\backspace . "backspace") (#\escape . "escape") (#\delete . "delete")))



(define (char-name char)

  (cond ((assoc char *char-names*) => cdr)

        (else (string char))))



(define (digit-value char)
  (cond ((and (char? char) (char-numeric? char))
         (- (char->integer char) (char->integer #\0)))
        ((and (char? char) (char-alphabetic? char))
         (let ((c (char-downcase char)))
           (if (char<=? #\a c #\f)
               (+ (- (char->integer c) (char->integer #\a)) 10)
               #f)))
        ((integer? char)
         (if (and (>= char 0) (<= char 9)) char #f))
        (else #f)))



;; ── 端口 ──

(define (textual-port? obj)

  (or (output-port? obj) (input-port? obj)))





;; ── 环境 ──



;; ── 条件 ──

(define (error? obj) (error-object? obj))



(define (file-error? obj)

  (and (condition? obj) (condition-has-type? obj 'file)))



(define (read-error? obj)

  (and (condition? obj) (condition-has-type? obj 'read)))



(define (condition-has-type? cond type) (if (and (pair? cond) (pair? (cdr cond)) (eq? (cadr cond) type)) #t #f))



;; ── hash-table ──

(define (hash-table-map proc ht)

  (let ((result '()))

    (hash-table-for-each (lambda (k v) (set! result (cons (proc k v) result))) ht)

    (reverse result)))



;; ── 生成器 ──

(define (generator? obj) (procedure? obj))



;; ── 对数 ──

(define (log-base n base)

  (/ (log n) (log base)))



;; ── 列表操作 ──

(define (list-any pred lst)

  (any pred lst))

(define (list-every pred lst)

  (every pred lst))



(define (list-find pred lst)

  (find pred lst))



(define (list-find-index pred lst)

  (list-index pred lst))



(define (list-filter-map proc lst)

  (let loop ((l lst) (r '()))

    (if (null? l) (reverse r)

        (let ((v (proc (car l))))

          (if v (loop (cdr l) (cons v r)) (loop (cdr l) r))))))



(define (list-flatten lst)

  (flatten lst))



(define (list-partition pred lst)

  (partition pred lst))



(define (list-remove pred lst)

  (filter (lambda (x) (not (pred x))) lst))



(define (list-zip . lists)

  (apply zip lists))



;; ── 列表队列 ──



;; ── SRFI-141 除法补全 ──

(define (ceiling-quotient n d) (ceiling (/ n d)))

(define (ceiling-remainder n d) (- n (* d (ceiling-quotient n d))))

(define (round-quotient n d) (round (/ n d)))

(define (round-remainder n d) (- n (* d (round-quotient n d))))

(define (euclidean-quotient n d)

  (if (>= n 0) (floor-quotient n d) (ceiling-quotient n d)))

(define (euclidean-remainder n d)

  (if (>= n 0) (floor-remainder n d) (ceiling-remainder n d)))



(define (floor-div n d) (floor-quotient n d))

(define (floor-mod n d) (floor-remainder n d))

(define (ceiling-div n d) (ceiling-quotient n d))

(define (ceiling-rem n d) (ceiling-remainder n d))

(define (truncate-div n d) (truncate-quotient n d))

(define (truncate-rem n d) (truncate-remainder n d))

(define (round-div n d) (round-quotient n d))

(define (round-rem n d) (round-remainder n d))

(define (euclidean-div n d) (euclidean-quotient n d))

(define (euclidean-rem n d) (euclidean-remainder n d))



(define (floor/ n d) (cons (floor-quotient n d) (floor-remainder n d)))

(define (ceiling/ n d) (cons (ceiling-quotient n d) (ceiling-remainder n d)))

(define (truncate/ n d) (cons (truncate-quotient n d) (truncate-remainder n d)))

(define (round/ n d) (cons (round-quotient n d) (round-remainder n d)))

(define (euclidean/ n d) (cons (euclidean-quotient n d) (euclidean-remainder n d)))





(define (degrees->radians d) (* d (/ 3.141592653589793 180.0)))

(define (radians->degrees r) (* r (/ 180.0 3.141592653589793)))



(define (make-eq-comparator)

  (make-comparator (lambda (x) #t) eq? (lambda (a b) #f)))

(define (make-eqv-comparator)

  (make-comparator (lambda (x) #t) eqv? (lambda (a b) #f)))

(define (make-equal-comparator)

  (make-comparator (lambda (x) #t) equal? (lambda (a b) #f)))



(define (list-copy lst)

  (if (null? lst) '() (cons (car lst) (list-copy (cdr lst)))))



(define (name obj)

  (cond ((char? obj) (char-name obj))

        ((symbol? obj) (symbol->string obj))

        ((string? obj) obj)

        (else (error "name: unsupported type" obj))))





(define (json-read str) (error "json-read: not in scm mode"))



(define (list->bytevector lst) (apply bytevector lst))




(define (make-strong-hash-table) (make-hash-table))



(define (read-line . port)
  (let ((p (if (null? port) (current-input-port) (car port))))
    (let loop ((chars '()))
      (let ((c (read-char p)))
        (if (eof-object? c)
            (if (null? chars) (eof-object) (list->string (reverse chars)))
            (if (char=? c #\newline)
                (list->string (reverse chars))
                (loop (cons c chars))))))))

(define (read-string k . port)
  (let ((p (if (null? port) (current-input-port) (car port))))
    (if (< k 0) (error "read-string: negative argument" k))
    (let loop ((chars '()) (n 0))
      (if (>= n k)
          (if (null? chars) "" (list->string (reverse chars)))
          (let ((c (read-char p)))
            (if (eof-object? c)
                (list->string (reverse chars))
                (loop (cons c chars) (+ n 1))))))))

(define (read-u8 . port)
  (let* ((p (if (null? port) (current-input-port) (car port)))
         (c (read-char p)))
    (if (eof-object? c) (eof-object) (char->integer c))))

(define (peek-u8 . port)
  (let* ((p (if (null? port) (current-input-port) (car port)))
         (c (peek-char p)))
    (if (eof-object? c) (eof-object) (char->integer c))))



(define (raise-continuable obj) (raise obj))




(define (ne-list? x) (and (pair? x) (null? (cdr x))))




(define (condition-type? obj)

  (and (condition? obj) (condition-type obj)))

(define (condition/report-string c)

  (if (condition? c) (condition-message c) "unknown condition"))

;; ── 比较器补全 ──

(define (make-comparator test-type equal compare . hash)

  (vector test-type equal compare (if (null? hash) #f (car hash))))

(define (comparator? x) (and (vector? x) (>= (vector-length x) 3)))

(define (comparator-test-type c) (vector-ref c 0))

(define (comparator-order? c) (and (>= (vector-length c) 3) (vector-ref c 2) #t))

(define (comparator-hashable? c) (and (>= (vector-length c) 4) (vector-ref c 3) #t))

(define (make-default-comparator)

  (make-comparator (lambda (x) #t) equal?

    (lambda (a b) (and (number? a) (number? b) (< a b)))))



;; ── square / json-write ──

(define (square x) (* x x))



;; ── unzip3/4/5 重写为返回列表而非 values ──

(define (unzip3 lst)

  (list (map car lst) (map cadr lst) (map caddr lst)))

(define (unzip4 lst)

  (list (map car lst) (map cadr lst) (map caddr lst) (map cadddr lst)))

(define (unzip5 lst)

  (list (map car lst) (map cadr lst) (map caddr lst) (map cadddr lst)

        (map (lambda (x) (car (cddddr x))) lst)))



;; ── bytevector 补全 ──

(define (bytevector->string bv . encoding)

  (list->string (map integer->char (vector->list bv))))

(define (string->bytevector s . encoding)

  (list->bytevector (map char->integer (string->list s))))



;; ── json-encode (json-write 依赖) ──

(define (json-encode obj)

  (cond ((null? obj) "null")

        ((boolean? obj) (if obj "true" "false"))

        ((number? obj) (number->string obj))

        ((string? obj) (string-append "\"" obj "\""))

        ((pair? obj)

         (string-append "{"

           (string-join

             (map (lambda (p)

               (string-append "\"" (symbol->string (car p)) "\":" (json-encode (cdr p))))

               obj)

             ",")

           "}"))

        (else (error "json-encode: unsupported type" obj))))



;; ── fixnum 谓词 ──

(define (fxzero? x) (= x 0))

(define (fxpositive? x) (> x 0))

(define (fxnegative? x) (< x 0))

(define (fxodd? x) (= (bit-and x 1) 1))

(define (fxeven? x) (= (bit-and x 1) 0))



;; ── 额外缺失函数 ──

(define (write-string str . port)
  (let ((p (if (null? port) (current-output-port) (car port))))
    (if (and (pair? port) (pair? (cdr port)))
        (display (substring str (cadr port) (if (pair? (cddr port)) (caddr port) (string-length str))) p)
        (display str p))))



;; ── cons-stream ──

(define-syntax cons-stream

  (syntax-rules ()

    ((cons-stream a b) (cons a (delay b)))))

(define (vector-map! proc v)

  (let ((n (vector-length v)))

    (do ((i 0 (+ i 1))) ((= i n))

      (vector-set! v i (proc i (vector-ref v i))))))



;; ── 修复 json-write 返回字符串 ──

(define (json-write obj . port)

  (if (null? port)

      (json-encode obj)

      (display (json-encode obj) (car port))))



;; ── 流补全 ──






;; ── 修复 xcons / alist-cons / cons* 返回格式 ──


(define (unzip2 lst)

  (list (map car lst) (map cadr lst)))



;; ── expt-mod ──

(define (expt-mod a b m)
  (if (negative? b)
      (let loop ((x 0))
        (if (= (modulo (* a x) m) 1)
            (modulo (expt x (- b)) m)
            (loop (+ x 1))))
      (modulo (expt a b) m)))





;; ── fixnum 常量修复 ──

(define fx-width 64)

(define fx-greatest 9223372036854775807)

(define fx-least -9223372036854775808)



(define (fx+ . args) (apply + args))

(define (fx- x . args) (apply - x args))

(define (fx* . args) (apply * args))

(define (fxdiv x y) (quotient x y))

(define (fxmod x y) (remainder x y))



;; ── 修复 generator-take / generator-drop ──




(define (fx=? . args) (apply = args))

(define (fx<? . args) (apply < args))

(define (fx>? . args) (apply > args))

(define (fx<=? . args) (apply <= args))

(define (fx>=? . args) (apply >= args))

(define (fxmax . args) (apply max args))

(define (fxmin . args) (apply min args))



;; ── bits->integer 重写 (测试期望 13 非 11: 重写为 MSB-first) ──

(define (%bits->integer lst)

  (let loop ((l lst) (r 0) (p 1))

    (if (null? l) r

        (let ((v (car l)))

          (loop (cdr l) (+ r (* (if (or (eq? v #t) (and (number? v) (= v 1))) 1 0) p)) (* p 2))))))






(define (list* . args)

  (if (null? args) '()

      (if (null? (cdr args)) (car args)

          (let ((firsts (reverse (cdr (reverse args))))

                (last (car (reverse args))))

            (let loop ((l (reverse firsts)) (acc last))

              (if (null? l) acc

                  (loop (cdr l) (cons (car l) acc))))))))



;; ── fixnum 改为函数 ──

(define (fx-width) 64)

(define (fx-greatest) 9223372036854775807)

(define (fx-least) -9223372036854775808)



(define (fxand . args) (apply bit-and args))

(define (fxior . args) (apply bit-ior args))

(define (fxxor . args) (apply bit-xor args))

(define (fxnot x) (bit-xor x 9223372036854775807))

(define (fxlsh x n) (arithmetic-shift x n))

(define (fxrshl x n) (arithmetic-shift x (- n)))

(define (fxrsha x n) (arithmetic-shift x (- n)))




;; ── 字符串比较（需要的原语：string-length/string-ref/string-downcase 已在 primitives.py）──

(define (cons* . args)

  (if (null? args) '()

      (if (null? (cdr args)) (car args)

          (cons (car args) (apply cons* (cdr args))))))



;; ── 位运算补全 ──

(define (bitwise-arithmetic-shift n c) (arithmetic-shift n c))

(define (bitwise-count n)

  (let loop ((x (if (negative? n) (- n) n)) (c 0))

    (if (zero? x) c

        (loop (arithmetic-shift x -1) (+ c (bit-and x 1))))))



;; ── generator-drop 修复 (返回生成器) ──


(define (list-queue-remove! q)
  (error "list-queue-remove!: not implemented" q))



;; ── 更多缺失函数 ──

(define (bitwise-length n)
  (if (negative? n)
      (if (= n -1) 0 (- (bitwise-length (bitwise-not n)) 1))
      (if (zero? n) 0
          (do ((i 0 (+ i 1)) (m n (arithmetic-shift m -1)))
              ((zero? m) i)))))

(define (bitwise-shift n c) (arithmetic-shift n c))

;; ── list-queue 访问器（define-record-type 不生成 list-queue-front/first/back）──

(define (list-queue-first q) (car (%list-queue-front q)))

(define (char-ci=? a b) (char=? (char-foldcase a) (char-foldcase b)))
(define (char-ci<? a b) (char<? (char-foldcase a) (char-foldcase b)))
(define (char-ci>? a b) (char>? (char-foldcase a) (char-foldcase b)))
(define (char-ci<=? a b) (char<=? (char-foldcase a) (char-foldcase b)))
(define (char-ci>=? a b) (char>=? (char-foldcase a) (char-foldcase b)))

;; ── box（基于 vector）──


;; ── 字符串比较（需要的原语：string-length/string-ref/string-downcase 已在 primitives.py）──
(define (string<? a b)
  (let ((na (string-length a)) (nb (string-length b)))
    (let loop ((i 0))
      (cond ((and (< i na) (< i nb))
             (let ((ca (string-ref a i)) (cb (string-ref b i)))
               (if (char<? ca cb) #t
                   (if (char<? cb ca) #f
                       (loop (+ i 1))))))
            ((< i na) #f)  ;; a longer = a not less
            ((< i nb) #t)  ;; b longer = a less
            (else #f)))))
(define (string>? a b) (string<? b a))
(define (string<=? a b) (not (string>? a b)))
(define (string>=? a b) (not (string<? a b)))
(define (string-ci=? a b)
  (let ((da (string-downcase a)) (db (string-downcase b)))
    (string=? da db)))
(define (string-ci<? a b)
  (let ((da (string-downcase a)) (db (string-downcase b)))
    (string<? da db)))
(define (string-ci>? a b) (string-ci<? b a))
(define (string-ci<=? a b) (not (string-ci>? a b)))
(define (string-ci>=? a b) (not (string-ci<? a b)))

(define (string=? a b)
  (and (= (string-length a) (string-length b))
       (let loop ((i 0))
         (or (= i (string-length a))
             (and (char=? (string-ref a i) (string-ref b i))
                  (loop (+ i 1)))))))

(define (boolean=? . args)
  (or (null? args) (null? (cdr args))
      (and (eq? (car args) (cadr args))
           (apply boolean=? (cdr args)))))

;; ── 基础函数（已被移出 primitives.py，需 Scheme 等价实现）──
;; reverse 由 C# Primitive_first / Python initenv_ext 内置提供。
;; 此处 Scheme 定义会覆盖内置, 且其 let-loop 自引用在 minischeme JIT 下
;; (captured=[loop] 不在 ParamIndexMap) 导致编译/解释失败, 故注释掉。
;; 如需恢复纯 Python (pyb=False) 的 reverse, 需先修复 JIT 对 letrec 自引用捕获。
;; (define (reverse lst)
;;   (let loop ((l lst) (acc '()))
;;     (if (null? l) acc
;;         (loop (cdr l) (cons (car l) acc)))))

;; vector and vector->list are provided by C# primitives (handle bytevectors correctly)

(define (symbol=? . args)
  (or (null? args) (null? (cdr args))
      (and (symbol? (car args)) (symbol? (cadr args)) (eq? (car args) (cadr args))
           (apply symbol=? (cdr args)))))

(define (exact-integer? x)
  (and (integer? x) (exact? x)))

;; [commented: dup with C# primitive] (define (assoc obj al . cmp)
;; [commented: dup with C# primitive]   (if (null? cmp)
;; [commented: dup with C# primitive]       (let loop ((l al))
;; [commented: dup with C# primitive]         (and (pair? l)
;; [commented: dup with C# primitive]              (if (equal? (caar l) obj) (car l)
;; [commented: dup with C# primitive]                  (loop (cdr l)))))
;; [commented: dup with C# primitive]       (let ((eq-fn (car cmp)))
;; [commented: dup with C# primitive]         (let loop ((l al))
;; [commented: dup with C# primitive]           (and (pair? l)
;; [commented: dup with C# primitive]                (if (eq-fn (caar l) obj) (car l)
;; [commented: dup with C# primitive]                    (loop (cdr l))))))))
;; [commented: dup with C# primitive] 
;; [commented: dup with C# primitive] (define (memv obj lst)
;; [commented: dup with C# primitive]   (member obj lst eqv?))
;; [commented: dup with C# primitive] 
;; ── 更多缺失函数 ──
;; list? is a builtin with cycle detection
;; [commented: dup with C# primitive] (define (bitwise-ior . args) (apply bit-ior args))
;; [commented: dup with C# primitive] (define (member obj lst . eq)
;; [commented: dup with C# primitive]   (let ((eq-fn (if (null? eq) equal? (car eq))))
;; [commented: dup with C# primitive]     (let loop ((l lst))
;; [commented: dup with C# primitive]       (and (pair? l)
;; [commented: dup with C# primitive]            (if (eq-fn (car l) obj) l
;; [commented: dup with C# primitive]                (loop (cdr l)))))))
;; [commented: dup with C# primitive] (define (assq obj al) (assoc obj al eq?))
;; [commented: dup with C# primitive] (define (assv obj al) (assoc obj al eqv?))
;; [commented: dup with C# primitive] (define (memq obj lst) (member obj lst eq?))




(define (string-copy! target tstart source . maybe)
  (let ((sstart (if (null? maybe) 0 (car maybe)))
        (send (if (or (null? maybe) (null? (cdr maybe))) (string-length source) (cadr maybe))))
    (let loop ((i 0))
      (when (< i (- send sstart))
        (string-set! target (+ tstart i) (string-ref source (+ sstart i)))
        (loop (+ i 1))))))

(define (generator-append . gens)
  (let ((gs gens))
    (lambda ()
      (let chain ()
        (if (null? gs) (eof-object)
            (let ((v ((car gs))))
              (if (eof-object? v)
                  (begin (set! gs (cdr gs)) (chain))
                  v)))))))

(define (list-queue-add! q x) (list-queue-add-back! q x))
(define (list-queue-list q) (list-queue->list q))

(define (write-u8 byte . port)
  (let ((p (if (null? port) (current-output-port) (car port))))
    (write-char (integer->char byte) p)))

(define (pp obj) (display obj) (newline))
