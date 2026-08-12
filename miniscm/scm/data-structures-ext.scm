;; 第二十部分：count 通用版本（替换 Python builtin）
;; ============================================================

(define (count pred lst)
  (fold-left (lambda (acc x) (if (pred x) (+ acc 1) acc)) 0 lst))

(define (concatenate lsts)
  (apply append lsts))

(define (flatten lst)
  (reverse
    (let loop ((xs lst) (acc '()))
      (cond ((null? xs) acc)
            ((pair? xs) (loop (cdr xs) (loop (car xs) acc)))
            (else (cons xs acc))))))

;; ============================================================
;; Part 2: _PRELUDE — R7RS standard prelude + 扩展
;; (14 definitions removed due to overlap with Part 1)
;; ============================================================
(define (exact-nonnegative-integer? n)
  (and (exact-integer? n) (>= n 0)))

(define (exact-rational? n)
  (and (rational? n) (exact? n)))

(define (string . chars)
  (list->string chars))

(define number=? =)
(define (sub1* n) (- n 1))
(define (object->string obj)
  (let ((port (open-output-string)))
    (write obj port)
    (get-output-string port)))

(define (with-exception-handler/k handler thunk)
  (with-exception-handler handler thunk))

(define (loop-n n)
  (if (= n 0) 'done (loop-n (- n 1))))

(define (string-index str pred)
  (let loop ((i 0))
    (cond ((>= i (string-length str)) #f)
          ((pred (string-ref str i)) i)
          (else (loop (+ i 1))))))

(define real->exact inexact->exact)

(define (first  lst) (car lst))

(define second cadr)

(define third caddr)

(define fourth cadddr)

(define (fifth  lst) (car (cddddr lst)))

(define (sixth  lst) (cadr (cddddr lst)))

(define (seventh lst) (caddr (cddddr lst)))

(define (eighth  lst) (cadddr (cddddr lst)))

(define (ninth   lst) (car (cddddr (cddddr lst))))

(define (tenth   lst) (cadr (cddddr (cddddr lst))))

(define (list-index pred lst)
  (let loop ((lst lst) (i 0))
    (cond ((null? lst) #f)
          ((pred (car lst)) i)
          (else (loop (cdr lst) (+ i 1))))))

(define (with-output-to-string thunk)
  (let ((port (open-output-string)))
    (parameterize ((current-output-port port))
      (thunk))
    (get-output-string port)))

(define (integer->string/radix n radix)
  (number->string n radix))

(define (string-trim-left str . args)
  (let ((pred (if (null? args) char-whitespace? (if (char? (car args)) (lambda (c) (char=? c (car args))) (car args))))
        (n (string-length str)))
    (let loop ((i 0))
      (cond ((>= i n) "")
            ((pred (string-ref str i)) (loop (+ i 1)))
            (else (substring str i n))))))

(define (string-pad str len . args)
  (let ((chr (if (null? args) #\space (car args))))
    (let ((pad (- len (string-length str))))
      (if (<= pad 0) (substring str 0 len)
          (string-append (make-string pad chr) str)))))

(define (string-pad-right str len . args)
  (let ((chr (if (null? args) #\space (car args))))
    (let ((pad (- len (string-length str))))
      (if (<= pad 0) (substring str 0 len)
          (string-append str (make-string pad chr))))))

(define (string-count str pred)
  (let loop ((i 0) (n 0))
    (if (>= i (string-length str)) n
        (if (pred (string-ref str i))
            (loop (+ i 1) (+ n 1))
            (loop (+ i 1) n)))))

(define (char-set . chars)
  (let ((s (make-vector 256 #f)))
    (for-each (lambda (c) (vector-set! s (char->integer c) #t)) chars)
    s))

(define (char-set? x)
  (and (vector? x) (= (vector-length x) 256)))

(define (char-set-contains? cs c)
  (and (< (char->integer c) 256) (vector-ref cs (char->integer c))))

(define (char-set-empty? cs)
  (let loop ((i 0))
    (if (>= i 256) #t
        (and (not (vector-ref cs i)) (loop (+ i 1))))))

(define (char-set->list cs)
  (let loop ((i 0) (result '()))
    (if (>= i 256) (reverse result)
        (if (vector-ref cs i)
            (loop (+ i 1) (cons (integer->char i) result))
            (loop (+ i 1) result)))))

(define (char-set-union . css)
  (let ((result (make-vector 256 #f)))
    (for-each
      (lambda (cs)
        (let loop ((i 0))
          (when (< i 256)
            (if (vector-ref cs i) (vector-set! result i #t))
            (loop (+ i 1)))))
      css)
    result))

(define (char-set-intersection . css)
  (let ((result (make-vector 256 #t)))
    (for-each
      (lambda (cs)
        (let loop ((i 0))
          (when (< i 256)
            (unless (vector-ref cs i) (vector-set! result i #f))
            (loop (+ i 1)))))
      css)
    result))

(define (char-set-difference cs1 . css)
  (let ((result (char-set-copy cs1)))
    (for-each
      (lambda (cs)
        (let loop ((i 0))
          (when (< i 256)
            (if (vector-ref cs i) (vector-set! result i #f))
            (loop (+ i 1)))))
      css)
    result))

(define (char-set-copy cs)
  (let ((copy (make-vector 256)))
    (let loop ((i 0))
      (when (< i 256)
        (vector-set! copy i (vector-ref cs i))
        (loop (+ i 1))))
    copy))

(define (char-set-adjoin cs . chars)
  (let ((result (char-set-copy cs)))
    (for-each (lambda (c) (vector-set! result (char->integer c) #t)) chars)
    result))

(define (char-set-delete cs . chars)
  (let ((result (char-set-copy cs)))
    (for-each (lambda (c) (vector-set! result (char->integer c) #f)) chars)
    result))

(define (char-set-complement cs)
  (let ((result (make-vector 256)))
    (let loop ((i 0))
      (when (< i 256)
        (vector-set! result i (not (vector-ref cs i)))
        (loop (+ i 1))))
    result))

(define char-set:lower-case
  (let loop ((i (char->integer #\a)) (cs (make-vector 256 #f)))
    (if (> i (char->integer #\z)) cs
        (begin (vector-set! cs i #t)
               (loop (+ i 1) cs)))))
(define char-set:lower char-set:lower-case)

(define char-set:upper-case
  (let loop ((i (char->integer #\A)) (cs (make-vector 256 #f)))
    (if (> i (char->integer #\Z)) cs
        (begin (vector-set! cs i #t)
               (loop (+ i 1) cs)))))
(define char-set:upper char-set:upper-case)

(define char-set:digit
  (let loop ((i (char->integer #\0)) (cs (make-vector 256 #f)))
    (if (> i (char->integer #\9)) cs
        (begin (vector-set! cs i #t)
               (loop (+ i 1) cs)))))

(define char-set:letter
  (char-set-union char-set:lower-case char-set:upper-case))

(define char-set:whitespace
  (char-set #\space #\tab #\newline (integer->char 13)))

(define char-set:punctuation
  (char-set #\. #\, #\; #\: #\! #\? #\- #\' #\" #\( #\) #\[ #\]
            #\{ #\} #\/ #\\ #\@ #\# #\$ #\% #\^ #\& #\* #\+ #\= #\< #\> #\| #\~))

;; box/box?/unbox/set-box! provided by Python builtin in initenv.py

(define (generator . args)
  (let ((lst args))
    (lambda ()
      (if (null? lst) (eof-object)
          (let ((val (car lst)))
            (set! lst (cdr lst))
            val)))))

(define (make-generator proc)
  (lambda ()
    (let ((val (proc)))
      (if (eof-object? val) (eof-object) val))))

(define (list->generator lst)
  (lambda ()
    (if (null? lst) (eof-object)
        (let ((val (car lst)))
          (set! lst (cdr lst))
          val))))

(define (vector->generator vec)
  (let ((i 0) (n (vector-length vec)))
    (lambda ()
      (if (>= i n) (eof-object)
          (let ((val (vector-ref vec i)))
            (set! i (+ i 1))
            val)))))

(define (string->generator str)
  (let ((i 0) (n (string-length str)))
    (lambda ()
      (if (>= i n) (eof-object)
          (let ((val (string-ref str i)))
            (set! i (+ i 1))
            val)))))

(define (generator->list gen . limit)
  (let ((n (if (null? limit) #f (car limit)))
        (result '()))
    (let loop ((i 0))
      (let ((val (gen)))
        (if (or (eof-object? val) (and n (>= i n)))
            (reverse result)
            (begin (set! result (cons val result))
                   (loop (+ i 1))))))))

(define (generator->vector gen . limit)
  (list->vector (apply generator->list gen limit)))

(define (generator->string gen . limit)
  (list->string (apply generator->list gen limit)))

(define (generator-find pred gen)
  (let loop ()
    (let ((val (gen)))
      (if (eof-object? val) #f
          (if (pred val) val (loop))))))

(define (generator-count pred gen)
  (let loop ((n 0))
    (let ((val (gen)))
      (if (eof-object? val) n
          (if (pred val) (loop (+ n 1)) (loop n))))))

(define (make-iota-generator count . rest)
  (let ((start (if (null? rest) 0 (car rest)))
        (step (if (or (null? rest) (null? (cdr rest))) 1 (cadr rest)))
        (i 0))
    (lambda ()
      (if (>= i count) (eof-object)
          (let ((val (+ start (* i step))))
            (set! i (+ i 1))
            val)))))

(define (make-range-generator start end . step)
  (let ((s (if (null? step) 1 (car step)))
        (i start))
    (lambda ()
      (if (if (>= s 0) (>= i end) (<= i end)) (eof-object)
          (let ((val i))
            (set! i (+ i s))
            val)))))

(unless (defined? 'make-coroutine-generator)
  (define (make-coroutine-generator proc)
    (let ((result '())
          (running #t))
      (lambda ()
        (if running
            (let ((gen (lambda args
                         (set! result (if (null? args) #f (car args)))
                         (if (pair? result) (cdr result) '()))))
              (proc gen)
              (set! running #f)
              result)
            (eof-object))))))

(define (generator-filter pred gen)
  (lambda ()
    (let loop ()
      (let ((val (gen)))
        (if (or (eof-object? val) (pred val)) val
            (loop))))))

(define (generator-take n gen)
  (let ((i 0))
    (lambda ()
      (if (>= i n) (eof-object)
          (let ((val (gen)))
            (if (eof-object? val) val
                (begin (set! i (+ i 1)) val)))))))

(define (generator-for-each fn gen)
  (let loop ()
    (let ((val (gen)))
      (unless (eof-object? val)
        (fn val)
        (loop)))))

(define (tmap fn)
  (lambda (reducer)
    (case-lambda
      ((seed) (reducer seed))
      ((seed item) (reducer seed (fn item)))
      ((seed . rest) (apply reducer seed (map fn rest))))))

(define (tfilter pred)
  (lambda (reducer)
    (case-lambda
      ((seed) (reducer seed))
      ((seed item)
       (if (pred item) (reducer seed item) seed))
      ((seed . rest)
       (apply reducer seed (filter pred rest))))))

(define (ttake n)
  (lambda (reducer)
    (let ((remaining n))
      (case-lambda
        ((seed) (reducer seed))
        ((seed item)
         (if (<= remaining 0)
             seed
             (begin
               (set! remaining (- remaining 1))
               (reducer seed item))))
        ((seed . rest)
         (let ((taken (take rest remaining)))
           (set! remaining (- remaining (length taken)))
           (apply reducer seed taken)))))))

(define (tdrop n)
  (lambda (reducer)
    (let ((remaining n))
      (case-lambda
        ((seed) (reducer seed))
        ((seed item)
         (if (<= remaining 0)
             (reducer seed item)
             (begin (set! remaining (- remaining 1)) seed)))
        ((seed . rest)
         (let ((dropped (take rest remaining)))
           (set! remaining (- remaining (length dropped)))
           (if (null? dropped) seed
               (apply reducer seed (drop rest remaining)))))))))

(define (tconcatenate)
  (lambda (reducer)
    (case-lambda
      ((seed) (reducer seed))
      ((seed item)
       (if (list? item)
           (fold reducer seed item)
           (reducer seed item)))
      ((seed . rest)
       (fold (lambda (s item)
               (if (list? item)
                   (fold reducer s item)
                   (reducer s item)))
             seed rest)))))

(define (list-transduce xform reducer init lst)
  (let ((xf-reducer (xform reducer)))
    (let loop ((seed init) (items lst))
      (if (null? items)
          (xf-reducer seed)
          (loop (xf-reducer seed (car items)) (cdr items))))))

(define (vector-transduce xform reducer init vec)
  (let ((xf-reducer (xform reducer))
        (n (vector-length vec)))
    (let loop ((seed init) (i 0))
      (if (>= i n)
          (xf-reducer seed)
          (loop (xf-reducer seed (vector-ref vec i)) (+ i 1))))))

(define (string-transduce xform reducer init str)
  (let ((xf-reducer (xform reducer))
        (n (string-length str)))
    (let loop ((seed init) (i 0))
      (if (>= i n)
          (xf-reducer seed)
          (loop (xf-reducer seed (string-ref str i)) (+ i 1))))))

(define-record-type <hook>
  (make-hook-internal procedures)
  hook?
  (procedures hook-procedures set-hook-procedures!))

(define (make-hook . arity)
  (make-hook-internal '()))

(define (add-hook! hook proc . append?)
  (set-hook-procedures! hook
    (if (and (pair? append?) (car append?))
        (append (hook-procedures hook) (list proc))
        (cons proc (hook-procedures hook)))))

(define (remove-hook! hook proc)
  (set-hook-procedures! hook
    (filter (lambda (p) (not (eq? p proc))) (hook-procedures hook))))

(define (reset-hook! hook)
  (set-hook-procedures! hook '()))

(define (run-hook hook . args)
  (for-each (lambda (proc) (apply proc args)) (hook-procedures hook)))

(define-record-type <random-source>
  (%make-random-source state)
  random-source?
  (state random-source-state set-random-source-state!))

(define (make-random-source)
  (%make-random-source (current-second)))

(define (random-source->random-integer source n)
  (let ((state (random-source-state source)))
    (let* ((new-state (remainder (+ (* 1103515245 state) 12345) 2147483648))
           (val (modulo (inexact->exact (round (* (/ new-state 2147483648.0) n))) n)))
      (set-random-source-state! source new-state)
      val)))

(define (random-source->random-real source)
  (let ((state (random-source-state source)))
    (let* ((new-state (remainder (+ (* 1103515245 state) 12345) 2147483648))
           (val (/ new-state 2147483648.0)))
      (set-random-source-state! source new-state)
       val)))

(define (random-source-random-integer source n)
  (random-source->random-integer source n))
(define (random-source-random-real source)
  (random-source->random-real source))

(define (random-source-randomize! source)
  (set-random-source-state! source (current-second)))

(define (random-source-pseudo-randomize! source i j)
  (set-random-source-state! source (+ (* i 12345) j)))

(define (linear-update-list . args)
  (apply list args))

(define (random-integer n)
  (let ((state (random-source-state *default-random-source*)))
    (let* ((new-state (remainder (+ (* 1103515245 state) 12345) 2147483648))
           (val (modulo (inexact->exact (round (* (/ new-state 2147483648.0) n))) n)))
      (set-random-source-state! *default-random-source* new-state)
      val)))

(define (random-real)
  (let ((state (random-source-state *default-random-source*)))
    (let* ((new-state (remainder (+ (* 1103515245 state) 12345) 2147483648))
           (val (/ new-state 2147483648.0)))
      (set-random-source-state! *default-random-source* new-state)
      val)))

(define *default-random-source* (make-random-source))

(define-record-type <list-queue>
  (%make-list-queue front back)
  list-queue?
  (front %list-queue-front %set-list-queue-front!)
  (back  %list-queue-back  %set-list-queue-back!))

(define (make-list-queue front . rest)
  (%make-list-queue front
    (if (pair? rest) (car rest) (if (null? front) '() (last-pair front)))))

(define (list-queue . items)
  (let ((front (list-copy items)))
    (make-list-queue front (if (null? items) '() (last-pair front)))))

(define (list-queue-copy q)
  (let ((f (%list-queue-front q)))
    (make-list-queue (list-copy f) (if (null? f) '() (last-pair f)))))

(define (list-queue-add-front! q item)
  (%set-list-queue-front! q (cons item (%list-queue-front q)))
  (if (null? (%list-queue-back q))
      (%set-list-queue-back! q (%list-queue-front q))))

(define (list-queue-add-back! q item)
  (let ((new (list item)))
    (if (null? (%list-queue-back q))
        (begin
          (%set-list-queue-front! q new)
          (%set-list-queue-back! q new))
        (begin
          (set-cdr! (%list-queue-back q) new)
          (%set-list-queue-back! q new)))))

(define (list-queue-remove-front! q)
  (if (null? (%list-queue-front q))
      (error "list-queue-remove-front!: empty queue")
      (let ((val (car (%list-queue-front q))))
        (%set-list-queue-front! q (cdr (%list-queue-front q)))
        (if (null? (%list-queue-front q))
            (%set-list-queue-back! q '()))
        val)))

(define (list-queue-front q) (car (%list-queue-front q)))

(define (list-queue-back q) (car (%list-queue-back q)))

(define (list-queue-empty? q) (null? (%list-queue-front q)))

(define (list-queue->list q) (list-copy (%list-queue-front q)))

(define (list-queue-size q) (length (%list-queue-front q)))

(define (just value) (list value))

(define (just? x) (and (pair? x) (null? (cdr x))))

(define (nothing) '())

(define (nothing? x) (or (null? x) (eq? x #f)))

(define (maybe? x) (or (just? x) (nothing? x)))

(define (maybe-ref x . default)
  (if (null? x)
      (if (null? default) #f (car default))
      (car x)))

(define (permutations lst)
  (if (null? lst) '(())
      (apply append
        (map (lambda (x)
               (map (lambda (p) (cons x p))
                    (permutations (delete x lst))))
             lst))))

(define (combinations lst n)
  (cond ((= n 0) '(()))
        ((null? lst) '())
        (else
         (append
           (map (lambda (c) (cons (car lst) c))
                (combinations (cdr lst) (- n 1)))
           (combinations (cdr lst) n)))))

(define (cartesian-product . lists)
  (if (null? lists) '(())
      (let ((rest (apply cartesian-product (cdr lists))))
        (apply append
          (map (lambda (x)
                 (map (lambda (r) (cons x r)) rest))
               (car lists))))))

(define-record-type <binary-heap>
  (%make-binary-heap vec n cmp)
  binary-heap?
  (vec binary-heap-vec set-binary-heap-vec!)
  (n   binary-heap-n   set-binary-heap-n!)
  (cmp binary-heap-cmp))

(define (make-binary-heap . args)
  (let ((cmp (if (pair? args) (car args) <))
        (init (if (and (pair? args) (pair? (cdr args))) (cadr args) '())))
    (let ((vec (list->vector init))
          (len (length init)))
      (%make-binary-heap vec len cmp))))

(define (binary-heap-insert! heap val)
  (let ((vec (binary-heap-vec heap))
        (n (binary-heap-n heap)))
    (if (>= n (vector-length vec))
        ;; Grow vector
        (let ((new-vec (make-vector (* 2 (max 1 (vector-length vec))))))
          (vector-copy! new-vec 0 vec)
          (set-binary-heap-vec! heap new-vec)
          (set! vec new-vec)))
    (vector-set! vec n val)
    (set-binary-heap-n! heap (+ n 1))
    ;; Bubble up
    (let loop ((i n))
      (when (> i 0)
        (let ((parent (quotient (- i 1) 2)))
             (if ((binary-heap-cmp heap) val (vector-ref vec parent))
              (begin
                (vector-set! vec i (vector-ref vec parent))
                (vector-set! vec parent val)
                (loop parent)))))))
  heap)

(define (binary-heap-min heap)
  (if (= (binary-heap-n heap) 0)
      (error "binary-heap-min: empty heap")
      (vector-ref (binary-heap-vec heap) 0)))

(define (binary-heap-remove-min! heap)
  (let* ((vec (binary-heap-vec heap))
         (n (binary-heap-n heap)))
    (if (= n 0)
        (error "binary-heap-remove-min!: empty heap"))
    (let ((min-val (vector-ref vec 0)))
      (vector-set! vec 0 (vector-ref vec (- n 1)))
      (set-binary-heap-n! heap (- n 1))
      ;; Bubble down
      (let loop ((i 0))
        (let* ((left (+ (* i 2) 1))
               (right (+ (* i 2) 2))
                (smallest
                  (cond ((and (< left (- n 1))
                              ((binary-heap-cmp heap) (vector-ref vec left) (vector-ref vec i)))
                        left)
                       (else i)))
               (smallest
                  (if (and (< right (- n 1))
                           ((binary-heap-cmp heap) (vector-ref vec right) (vector-ref vec smallest)))
                     right
                     smallest)))
          (unless (= smallest i)
            (let ((tmp (vector-ref vec i)))
              (vector-set! vec i (vector-ref vec smallest))
              (vector-set! vec smallest tmp))
            (loop smallest))))
       min-val)))

(define (binary-heap-delete-min! heap)
  (binary-heap-remove-min! heap))

(define (reverse! lst)
  (let loop ((cur lst) (out '()))
    (if (null? cur) out
        (let ((next (cdr cur)))
          (set-cdr! cur out)
          (loop next cur)))))

(define (binary-heap-size heap)
  (binary-heap-n heap))

(define (binary-heap-empty? heap)
  (= (binary-heap-n heap) 0))


(define (vector-index pred vec . more)
  (let ((n (vector-length vec)))
    (let loop ((i 0))
      (if (>= i n) #f
          (if (pred (vector-ref vec i))
              i
              (loop (+ i 1)))))))

(define (vector-skip pred vec . more)
  (let ((n (vector-length vec)))
    (let loop ((i 0))
      (if (>= i n) #f
          (if (pred (vector-ref vec i)) (loop (+ i 1)) i)))))

(define (vector-any pred vec . more)
  (let ((n (vector-length vec)))
    (let loop ((i 0))
      (if (>= i n) #f
          (let ((r (pred (vector-ref vec i))))
            (if r r (loop (+ i 1))))))))

(define (vector-every pred vec . more)
  (let ((n (vector-length vec)))
    (let loop ((i 0))
      (if (>= i n) #t
          (if (pred (vector-ref vec i))
              (loop (+ i 1))
              #f)))))

(define (vector-fold kons knil vec . more)
  (let ((n (vector-length vec)))
    (let loop ((i 0) (acc knil))
      (if (>= i n) acc
          (loop (+ i 1) (kons i (vector-ref vec i) acc))))))

(define (vector-copy! to at from . args)
  (let ((start (if (null? args) 0 (car args)))
        (end (if (or (null? args) (null? (cdr args))) (vector-length from) (cadr args))))
    (do ((i start (+ i 1))
         (j at (+ j 1)))
        ((= i end))
      (vector-set! to j (vector-ref from i)))))

(define (vector-swap! vec i j)
  (let ((tmp (vector-ref vec i)))
    (vector-set! vec i (vector-ref vec j))
    (vector-set! vec j tmp)))

(define (vector-reverse! vec)
  (let ((n (vector-length vec)))
    (do ((i 0 (+ i 1))
         (j (- n 1) (- j 1)))
        ((>= i j))
      (vector-swap! vec i j))))

(define (xcons d a) (cons a d))

(define (append-reverse rev tail)
  (if (null? rev) tail
      (append-reverse (cdr rev) (cons (car rev) tail))))

(define (unfold p f g seed . args)
  (let ((tail-gen (if (null? args) (lambda (x) '()) (car args))))
    (let loop ((seed seed))
      (if (p seed) (tail-gen seed)
          (cons (f seed) (loop (g seed)))))))

(define (unzip1 lst) (map car lst))

(define (unzip2 lst)
  (values (map car lst) (map (lambda (x) (if (pair? (cdr x)) (cadr x) (cdr x))) lst)))

(define (unzip3 lst)
  (values (map car lst)
          (map (lambda (x) (if (pair? (cdr x)) (cadr x) (cdr x))) lst)
          (map (lambda (x) (caddr x)) lst)))

(define (unzip4 lst)
  (values (map car lst) (map cadr lst) (map caddr lst) (map cadddr lst)))

(define (test-begin . name)
  (display "===  Testing ")
  (if (null? name) (display "unnamed") (display (car name)))
  (display "  ===") (newline))

(define (test-end . name) (newline))

(define (merge! pred a b)
  (cond ((null? a) b)
        ((null? b) a)
        ((pred (car a) (car b))
         (set-cdr! a (merge! pred (cdr a) b))
         a)
        (else
         (set-cdr! b (merge! pred a (cdr b)))
         b)))

(define (hash-table-fold f init ht)
  (let ((result init))
    (hash-table-for-each
      (lambda (k v) (set! result (f k v result)))
      ht)
    result))

(define (hash-table-clear! ht)
  (for-each (lambda (k) (hash-table-delete! ht k)) (hash-table-keys ht)))

(define (vector-empty? v) (= (vector-length v) 0))

(define (vector-unfold f len . seeds)
  (let ((v (make-vector len))
        (n-seeds (length seeds)))
    (do ((i 0 (+ i 1)))
        ((>= i len) v)
      (call-with-values
        (lambda () (apply f i seeds))
        (lambda vals
          (vector-set! v i (car vals))
          (set! seeds (let ((new (cdr vals)))
                        (if (= (length new) n-seeds)
                            new
                            (map (lambda (s) (+ s 1)) seeds)))))))))

(define (vector-fold-right f init v)
  (let ((n (vector-length v)))
    (let loop ((i (- n 1)) (acc init))
      (if (< i 0) acc
          (loop (- i 1) (f i (vector-ref v i) acc))))))

(define (string-titlecase str)
  (let* ((len (string-length str))
         (result (make-string len)))
    (let loop ((i 0) (in-word #f))
      (if (>= i len) result
          (let ((ch (string-ref str i)))
            (cond
              ((char-alphabetic? ch)
               (string-set! result i (if in-word (char-downcase ch) (char-upcase ch)))
               (loop (+ i 1) #t))
              (else
               (string-set! result i ch)
               (loop (+ i 1) #f))))))))

(define (ascii? c) (and (char? c) (< (char->integer c) 128)))

(define (char-ascii? c) (ascii? c))

(define (char-iso-control? c) (char-control? c))

(define (scheme-implementation-name) "Hermes Scheme")

(define (scheme-implementation-version) "0.1 (R7RS-small + SRFIs)")

(define (version) (scheme-implementation-version))

(define (make-bitvector n . fill)
  (let ((init (if (null? fill) #f (car fill))))
    (let ((v (make-vector n init)))
      v)))

(define (bitvector? v)
  (and (vector? v) (or (= (vector-length v) 0)
                       (boolean? (vector-ref v 0)))))

(define (bitvector-length bv) (vector-length bv))

(define (bitvector-ref bv i) (vector-ref bv i))

(define (bitvector-set! bv i val) (vector-set! bv i val))

(define (bitvector->list bv) (vector->list bv))

(define (list->bitvector lst) (list->vector lst))

(define (bitvector-copy bv . args)
  (apply vector-copy bv args))

(define (bitvector-append . bvs)
  (let* ((total (fold-left (lambda (s v) (+ s (vector-length v))) 0 bvs))
         (r (make-vector total))
         (pos 0))
    (for-each (lambda (v)
                (let ((n (vector-length v)))
                  (do ((i 0 (+ i 1))) ((= i n))
                    (vector-set! r (+ pos i) (vector-ref v i)))
                  (set! pos (+ pos n))))
              bvs)
    r))

(define (ne-list? x)
  (and (pair? x) (not (pair? (cdr x)))))

(define (<> a b) (not (= a b)))

(define (procedure-rename proc name)
  proc)

(define-record-type <bimap>
  (%make-bimap forward reverse)
  bimap?
  (forward %bimap-forward %bimap-forward-set!)
  (rev %bimap-rev %bimap-rev-set!))

(define (make-bimap init)
  (let ((fwd (make-hash-table))
        (rev (make-hash-table)))
    (for-each (lambda (pair)
                (hash-table-set! fwd (car pair) (cdr pair))
                (hash-table-set! rev (cdr pair) (car pair)))
              init)
    (%make-bimap fwd rev)))

(define (bimap-forward bimap key)
  (hash-table-ref (%bimap-forward bimap) key))

(define (bimap-forward/default bimap key default)
  (hash-table-ref/default (%bimap-forward bimap) key default))

(define (bimap-reverse bimap val)
  (hash-table-ref (%bimap-rev bimap) val))

(define (bimap-set! bimap key val)
  (hash-table-set! (%bimap-forward bimap) key val)
  (hash-table-set! (%bimap-rev bimap) val key))

(define (bimap-contains? bimap key)
  (hash-table-contains? (%bimap-forward bimap) key))

(define-record-type <deque>
  (%make-deque front-len front back-len back)
  deque?
  (front-len %deque-fl %set-deque-fl!)
  (front    %deque-f %set-deque-f!)
  (back-len %deque-bl %set-deque-bl!)
  (back     %deque-b %set-deque-b!))

(define (make-deque . items)
  (%make-deque (length items) (list-copy items) 0 '()))

(define (deque-empty? dq)
  (and (= (%deque-fl dq) 0) (= (%deque-bl dq) 0)))

(define (deque-add-front dq item)
  (%set-deque-fl! dq (+ (%deque-fl dq) 1))
  (%set-deque-f! dq (cons item (%deque-f dq)))
  dq)

(define (deque-add-back dq item)
  (%set-deque-bl! dq (+ (%deque-bl dq) 1))
  (%set-deque-b! dq (cons item (%deque-b dq)))
  dq)

(define (deque-front dq)
  (if (deque-empty? dq) (error "deque-front: empty deque")
      (if (zero? (%deque-fl dq))
          (car (reverse (%deque-b dq)))
          (car (%deque-f dq)))))

(define (deque-back dq)
  (if (deque-empty? dq) (error "deque-back: empty deque")
      (if (zero? (%deque-bl dq))
          (car (reverse (%deque-f dq)))
          (car (%deque-b dq)))))

(define (deque-remove-front dq)
  (if (deque-empty? dq) (error "deque-remove-front: empty deque"))
  (let ((val (deque-front dq)))
    (if (zero? (%deque-fl dq))
        (let* ((b (reverse (%deque-b dq)))
               (n (length b)))
          (%set-deque-fl! dq (- n 1))
          (%set-deque-f! dq (cdr b))
          (%set-deque-bl! dq 0)
          (%set-deque-b! dq '()))
        (begin
          (%set-deque-fl! dq (- (%deque-fl dq) 1))
          (%set-deque-f! dq (cdr (%deque-f dq)))))
    val))

(define (deque-remove-back dq)
  (if (deque-empty? dq) (error "deque-remove-back: empty deque"))
  (let ((val (deque-back dq)))
    (if (zero? (%deque-bl dq))
        (let* ((f (reverse (%deque-f dq)))
               (n (length f)))
          (%set-deque-fl! dq 0)
          (%set-deque-f! dq '())
          (%set-deque-bl! dq (- n 1))
          (%set-deque-b! dq (cdr f)))
        (begin
          (%set-deque-bl! dq (- (%deque-bl dq) 1))
          (%set-deque-b! dq (cdr (%deque-b dq)))))
    val))

(define (deque-length dq)
  (+ (%deque-fl dq) (%deque-bl dq)))

(define (deque->list dq)
  (append (%deque-f dq) (reverse (%deque-b dq))))

(define (deque-push-front! dq item) (deque-add-front dq item))
(define (deque-push-back! dq item) (deque-add-back dq item))
(define (deque-pop-front! dq) (deque-remove-front dq))
(define (deque-pop-back! dq) (deque-remove-back dq))

(define fx-width 24)

(define fx-greatest (- (expt 2 (- fx-width 1)) 1))

(define fx-least (- -1 fx-greatest))

(define (fixnum? obj)
  (and (integer? obj) (exact? obj) (<= fx-least obj fx-greatest)))

(define (fixnum-width) fx-width)

(define (least-fixnum) fx-least)

(define (greatest-fixnum) fx-greatest)

(define (flonum? obj)
  (and (real? obj) (not (exact? obj))))

(define (fl=? a b) (= a b))

(define (fl<? a b) (< a b))

(define (fl>? a b) (> a b))

(define (fl<=? a b) (<= a b))

(define (fl>=? a b) (>= a b))

(define (fl+ . args) (apply + (map (lambda (x) (+ x 0.0)) args)))

(define (fl- . args) (apply - (map (lambda (x) (+ x 0.0)) args)))

(define (fl* . args) (apply * (map (lambda (x) (+ x 0.0)) args)))

(define (fl/ . args) (apply / (map (lambda (x) (+ x 0.0)) args)))

(define (flzero? x) (= x 0.0))

(define (flpositive? x) (> x 0.0))

(define (flnegative? x) (< x 0.0))

(define (flodd? x) (= (modulo (inexact->exact x) 2) 1))

(define (fleven? x) (= (modulo (inexact->exact x) 2) 0))

(define (flmin a . rest) (if (null? rest) a (apply min a rest)))

(define (flmax a . rest) (if (null? rest) a (apply max a rest)))

(define (flsqrt x) (sqrt x))

(define (flexpt a b) (expt a b))

(define (flexp x) (exp x))

(define (fllog x) (log x))

(define (flsin x) (sin x))

(define (flcos x) (cos x))

(define (fltan x) (tan x))

(define (flasin x) (asin x))

(define (flacos x) (acos x))

(define (flatan x) (atan x))

(define (flfloor x) (floor x))

(define (flceiling x) (ceiling x))

(define (fltruncate x) (truncate x))

(define (flround x) (round x))

(define (mapping . pairs)
  (let ((m (list->mapping pairs)))
    m))

(define (mapping? obj)
  (or (null? obj)
      (and (pair? obj) (pair? (car obj)) (list? obj))))

(define (list->mapping lst)
  (let loop ((l lst) (acc '()))
    (if (null? l) (reverse acc)
        (let ((k (car l)) (v (cadr l)))
          (loop (cddr l) (cons (cons k v) acc))))))

(define (mapping->list m)
  m)

(define (mapping-ref m key . default)
  (let ((pair (assoc key m)))
    (if pair (cdr pair)
        (if (null? default) #f
            (car default)))))

(define (mapping-contains? m key)
  (not (not (assoc key m))))

(define (mapping-set m key val)
  (let ((pair (assoc key m)))
    (if pair
        (cons (cons key val) (delete-assoc key m))
        (cons (cons key val) m))))

(define (mapping-delete m key)
  (delete-assoc key m))

(define (mapping-keys m)
  (map car m))

(define (mapping-values m)
  (map cdr m))

(define (mapping-size m)
  (length m))

(define (mapping-for-each f m)
  (for-each (lambda (p) (f (car p) (cdr p))) m))

(define (mapping-map f m)
  (map (lambda (p) (cons (car p) (f (car p) (cdr p)))) m))

(define (delete-assoc key lst)
  (cond ((null? lst) '())
        ((equal? (caar lst) key) (cdr lst))
        (else (cons (car lst) (delete-assoc key (cdr lst))))))

(define (make-array dimensions . init)
  ;; Create a nested vector structure
  (let ((dims (if (number? dimensions) (list dimensions) dimensions))
        (val (if (null? init) 0 (car init))))
    (letrec ((build
               (lambda (dims val)
                 (if (null? (cdr dims))
                     (make-vector (car dims) val)
                     (let ((v (make-vector (car dims))))
                       (do ((i 0 (+ i 1))) ((>= i (car dims)))
                         (vector-set! v i (build (cdr dims) val)))
                       v)))))
      (build dims val))))

(define (array? obj)
  (vector? obj))

(define (array-ref arr . indices)
  (let loop ((a arr) (idxs indices))
    (if (null? (cdr idxs))
        (vector-ref a (car idxs))
        (loop (vector-ref a (car idxs)) (cdr idxs)))))

(define (array-set! arr val . indices)
  (let loop ((a arr) (idxs indices))
    (if (null? (cdr idxs))
        (vector-set! a (car idxs) val)
        (loop (vector-ref a (car idxs)) (cdr idxs)))))

(define (array-dimensions arr)
  (let loop ((a arr) (dims '()))
    (if (vector? a)
        (loop (vector-ref a 0) (cons (vector-length a) dims))
        (reverse dims))))

(define (bitwise-reverse-bit-field n start end)
  (let ((field (bit-field n start end))
        (len (- end start)))
    (let loop ((i 0) (rev 0) (src field))
      (if (>= i len)
          (let ((mask (arithmetic-shift (- (expt 2 len) 1) start)))
            (bitwise-if mask (arithmetic-shift rev start) n))
          (loop (+ i 1) (+ (arithmetic-shift rev 1) (bit-and src 1))
                (arithmetic-shift src -1))))))

(define (first-set-bit n)
  (if (zero? n) -1
    (let loop ((i 0))
      (if (bit-set? n i) i (loop (+ i 1))))))

(define (bitwise-rotate n count len)
  (let ((field (bit-field n 0 len)))
    (let ((rotated (+ (arithmetic-shift (bit-field field 0 (- len count)) count)
                     (arithmetic-shift field (- count len)))))
      (bitwise-if (arithmetic-shift (- (expt 2 len) 1) 0)
                  rotated n))))
