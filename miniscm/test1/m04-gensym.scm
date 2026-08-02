;; 04-gensym.scm — 符号生成（避免使用不存在的 gensym）

(define-macro (my-swap-hygienic a b)
  `(let ((tmp ,a))
     (set! ,a ,b)
     (set! ,b tmp)))

(define-macro (my-with-temp expr . body)
  `(let ((tmp ,expr))
     (display "temp: ") (display tmp) (newline)
     ,@body))

(define-macro (my-delay-once val . body)
  `(let ((done #f) (result (if #f #f)))
     (begin
       (set! result ,val)
       ,@body)))

(define-macro (my-valof expr)
  `(let ((v ,expr)) v))

(define x 1) (define y 2)
(my-swap-hygienic x y)
(display x) (display y) (newline)
(my-with-temp (* 2 3))
(display (my-valof (+ 2 3))) (newline)
