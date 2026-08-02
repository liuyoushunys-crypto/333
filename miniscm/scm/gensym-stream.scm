;; 第十六部分：gensym（纯 Scheme 符号生成器）
;; ============================================================


(define (->string obj)
  (with-output-to-string (lambda () (display obj))))

;; ============================================================

(define (quick-expt base exp)
  ;; O(log n) 的快速幂，纯 Scheme
  (if (= exp 0) 1
      (let loop ((b base) (e exp) (r 1))
        (cond ((= e 0) r)
              ((even? e)
               (loop (* b b) (quotient e 2) r))
              (else
               (loop b (- e 1) (* r b)))))))

;; ============================================================
