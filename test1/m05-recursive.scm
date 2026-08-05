;; 05-recursive.scm — 递归宏

(define-macro (my-list . args)
  (if (null? args)
    (quote ())
    `(cons ,(car args) (my-list ,@(cdr args)))))

(define-macro (my-map fn . lists)
  (let ((x (gensym)))
    `(let ((,x ,(car lists)))
       (if (null? ,x)
         (quote ())
         (cons (,fn (car ,x))
               (my-map ,fn ,@(map cdr lists)))))))

(define-macro (my-filter pred lst)
  (let ((x (gensym)))
    `(let ((,x ,lst))
       (if (null? ,x)
         (quote ())
         (if (,pred (car ,x))
           (cons (car ,x) (my-filter ,pred (cdr ,x)))
           (my-filter ,pred (cdr ,x)))))))

(define-macro (my-nth n lst)
  (if (= n 0)
    `(car ,lst)
    `(my-nth ,(- n 1) (cdr ,lst))))

(define-macro (my-take n lst)
  (if (= n 0)
    (quote ())
    `(cons (car ,lst) (my-take ,(- n 1) (cdr ,lst)))))

(display (my-list 1 2 3 4 5)) (newline)
(display (my-nth 2 (quote (a b c d)))) (newline)
(display (my-take 3 (quote (a b c d e)))) (newline)
