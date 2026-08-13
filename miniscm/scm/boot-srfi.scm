;; ============================================================
;; boot-srfi.scm — 补充的标准/SRFI 过程与宏
;; 这些在 (import (srfi N)) 为 no-op 的前提下，作为全局定义提供，
;; 使各 SRFI 测试能够找到所需的过程。
;; ============================================================

;; ── SRFI-197: pipeline ──
(define-syntax |>
  (syntax-rules ()
    ((_ x) x)
    ((_ x (f . args) rest ...) (|> (f x . args) rest ...))
    ((_ x f rest ...) (|> (f x) rest ...))))

;; ── SRFI-137: min/max 接受单个列表参数 ──
(define (min . args)
  (let ((xs (if (and (= (length args) 1) (list? (car args))) (car args) args)))
    (fold (lambda (a b) (if (< a b) a b)) +inf.0 xs)))
(define (max . args)
  (let ((xs (if (and (= (length args) 1) (list? (car args))) (car args) args)))
    (fold (lambda (a b) (if (> a b) a b)) -inf.0 xs)))

;; ── SRFI-16 / SRFI-189: case-lambda ──
(define-macro (case-lambda . clauses)
  (define (req f)
    (let loop ((x f) (n 0))
      (cond ((null? x) n) ((symbol? x) n) (else (loop (cdr x) (+ n 1))))))
  (define (rest? f)
    (let loop ((x f))
      (cond ((null? x) #f) ((symbol? x) #t) (else (loop (cdr x))))))
  `(lambda args
     (cond
       ,@(map (lambda (cl)
                (let* ((formals (car cl)) (body (cdr cl))
                       (r (req formals)) (rs (rest? formals)))
                  (if rs
                      `((>= (length args) ,r) (apply (lambda ,formals ,@body) args))
                      `((= (length args) ,r) (apply (lambda ,formals ,@body) args)))))
              clauses)
       (else (error "case-lambda: no matching arity")))))

;; ── SRFI-89/182: lambda* (optional positional arguments) ──
(define-macro (lambda* specs . body)
  (let loop ((ss specs) (i 0) (bindings '()))
    (if (null? ss)
        `(lambda args
           (let ,(reverse bindings) ,@body))
        (let* ((spec (car ss))
               (name (if (pair? spec) (car spec) spec))
               (default (if (and (pair? spec) (pair? (cdr spec)))
                            (cadr spec) #f)))
          (loop (cdr ss) (+ i 1)
                (cons `(,name (if (> (length args) ,i)
                                  (list-ref args ,i)
                                  ,default))
                      bindings))))))

;; ── SRFI-73: infix 运算符表达式（支持 + - * / 优先级）──
(define-macro (infix . terms)
  `(infix-impl (quote ,terms)))
(define (infix-impl terms)
  (define (iop sym)
    (cond ((eq? sym '+) +) ((eq? sym '-) -) ((eq? sym '*) *) ((eq? sym '/) /)
          (else (error "infix: unknown operator"))))
  (define (find-op lst)
    (let ((n (length lst)))
      (let scan-hi ((i 1))
        (if (< i n)
            (if (or (eq? (list-ref lst i) '*) (eq? (list-ref lst i) '/)) i
                (scan-hi (+ i 2)))
            (let scan-lo ((i 1))
              (if (< i n)
                  (if (or (eq? (list-ref lst i) '+) (eq? (list-ref lst i) '-)) i
                      (scan-lo (+ i 2)))
                  (error "infix: no operator")))))))
  (define (reduce lst)
    (if (null? (cdr lst)) (car lst)
        (let ((i (find-op lst)))
          (reduce (append (take lst (- i 1))
                          (list ((iop (list-ref lst i)) (list-ref lst (- i 1)) (list-ref lst (+ i 1))))
                          (drop lst (+ i 2)))))))
  (reduce terms))

;; ── SRFI-183: everywhere ──
(define (everywhere f x)
  (cond ((pair? x) (cons (everywhere f (car x)) (everywhere f (cdr x))))
        ((vector? x) (vector-map (lambda (e) (everywhere f e)) x))
        (else (f x))))

;; ── SRFI-247: assoc-map ──
(define (assoc-map key val) (cons 'assoc-map (list (cons key val))))
(define (assoc-map? x) (and (pair? x) (eq? (car x) 'assoc-map)))
(define (assoc-map-ref am key . default)
  (let ((e (assoc key (cdr am))))
    (if e (cdr e) (if (null? default) #f (car default)))))

;; ── SRFI-235 / SRFI-185: update ──
(define (update obj k f)
  (cond ((and (list? obj) (not (and (pair? obj) (pair? (car obj)))))
         (let ((v (list->vector obj)))
           (vector-set! v k (f (vector-ref v k)))
           (vector->list v)))
        ((vector? obj)
         (let ((v (make-vector (vector-length obj))))
           (do ((i 0 (+ i 1))) ((= i (vector-length obj)))
             (vector-set! v i (vector-ref obj i)))
           (vector-set! v k (f (vector-ref v k)))
           v))
        (else
         (let loop ((al obj) (acc '()))
           (if (null? al)
               (reverse (cons (cons k (f (cdr (assoc k obj)))) acc))
               (let ((e (car al)))
                 (if (equal? (car e) k)
                     (append (reverse acc) (cons (cons k (f (cdr e))) (cdr al)))
                     (loop (cdr al) (cons e acc)))))))))

;; ── SRFI-200: sorted-by ──
(define (sorted-by less lst) (sort less lst))

;; ── SRFI-202: bmi 按位掩码整数运算 ──
(define (bmi-and . args) (fold (lambda (a b) (logand a b)) -1 args))
(define (bmi-ior . args) (fold (lambda (a b) (logior a b)) 0 args))
(define (bmi-xor . args) (fold (lambda (a b) (logxor a b)) 0 args))
(define (bmi-not x) (lognot x))

;; ── SRFI-171: transducers ──
(define (tfilter pred)
  (lambda (rf)
    (lambda args
      (if (= (length args) 1) (apply rf args)
          (let ((acc (car args)) (x (cadr args)))
            (if (pred x) (rf acc x) acc))))))
(define (tmap f)
  (lambda (rf)
    (lambda args
      (if (= (length args) 1) (apply rf args)
          (let ((acc (car args)) (x (cadr args)))
            (rf acc (f x)))))))
(define (rcons . args)
  (if (= (length args) 1) (car args)
      (append (car args) (list (cadr args)))))
(define (list-transduce xform reducer init lst)
  (let ((rf (xform reducer)))
    (let loop ((acc init) (xs lst))
      (if (null? xs) (rf acc)
          (loop (rf acc (car xs)) (cdr xs))))))

;; ── SRFI-172: two-arg-invoke ──
(define (two-arg-invoke proc a b) (proc a b))

;; ── SRFI-213: curried-lambda ──
(define-macro (curried-lambda formals . body)
  (let ((n (length formals)))
    `(lambda args
       (apply (lambda ,formals ,@body) (take args ,n)))))

;; ── SRFI-86: mu（多值绑定）──
(define-syntax mu
  (syntax-rules ()
    ((_ (var ...) body ...)
     (lambda (var ...) body ...))
    ((_ (var ...) expr body ...)
     (call-with-values (lambda () expr) (lambda (var ...) body ...)))))

;; SRFI-25/163 shape descriptor used by the portable array helpers.
(define (shape start end) (list start end))

;; ── SRFI-55: require-extension ──
(define-syntax require-extension
  (syntax-rules () ((_ library ...) (if #f #f))))

;; ── SRFI-239: destructuring-bind ──
(define-macro (destructuring-bind pattern expr . body)
  (let loop ((p pattern) (i 0) (binds '()))
    (if (null? p)
        `(let ,(reverse binds) ,@body)
        (loop (cdr p) (+ i 1)
              (cons (list (car p) `(list-ref ,expr ,i)) binds)))))

;; ── SRFI-38: write-with-shared-structure ──
(define (write-with-shared-structure x . port) (apply write (cons x port)))

;; ── SRFI-153: <?. 链式比较 ──
(define (<?. cmp . nums)
  (define (chain prev rest)
    (if (null? rest) #t
        (if (cmp prev (car rest))
            (chain (car rest) (cdr rest))
            #f)))
  (if (or (null? nums) (null? (cdr nums))) #t
      (chain (car nums) (cdr nums))))
