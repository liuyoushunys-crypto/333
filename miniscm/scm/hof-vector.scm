;; 第十一部分：高阶函数（函数组合与变换）
;; ============================================================

(define (curry f . args)
  (lambda xs
    (apply f (append args xs))))

;; ============================================================
;; 第十二部分：向量操作
;; ============================================================

(define (vector-for-each f . vecs)
  (let ((len (vector-length (car vecs))))
    (do ((i 0 (+ i 1)))
        ((>= i len))
      (apply f (map (lambda (v) (vector-ref v i)) vecs)))))

(define (vector-reverse vec)
  (let* ((n (vector-length vec))
         (r (make-vector n)))
    (do ((i 0 (+ i 1)))
        ((>= i n) r)
      (vector-set! r i (vector-ref vec (- n i 1))))))

(define (vector-concatenate vecs)
  (apply vector-append (vector->list vecs)))

;; ============================================================
;; 第十三部分：续延应用（call/cc 的妙用）
;; ============================================================

(define (product . nums)
  (let loop ((acc 1) (ns nums))
    (if (null? ns) acc
        (let ((n (car ns)))
          (if (= n 0) 0
              (loop (* acc n) (cdr ns)))))))

(define (tree->list tree)
  (let loop ((node tree) (acc '()))
    (cond ((null? node) acc)
          ((pair? node) (loop (car node) (loop (cdr node) acc)))
          (else (cons node acc)))))

;; ============================================================
