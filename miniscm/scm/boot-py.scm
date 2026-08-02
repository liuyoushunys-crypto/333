;; ~~ pipe / compose / curry / partial 高阶函数组合 ~~

;; pipe: 从左到右组合 (pipe f g)(x) = g(f(x))
(define-syntax pipe
  (syntax-rules ()
    ((_ f) f)
    ((_ f g) (lambda (x) (g (f x))))
    ((_ f g . rest) (pipe (pipe f g) . rest))))


;; curry: 将多参函数转为嵌套单参链
;; (curry f) → f 本身;  (curry f a) → (lambda (x) (f a x))
(define-macro (curry fn . args)
  (if (null? args) fn
      `(lambda (x) ((curry ,fn ,@(cdr args)) ,(car args) x))))

;; (curry/f f n) — f 是 Scheme 函数, n 是参数总数, 返回柯里化版
(define-macro (curry/s f n)
  (if (<= n 1) f
      `(lambda (x) ((curry/s (,f x) ,(- n 1))))))



;; ── Python 导入宏 ──
(define-macro (from mod import . names)
  (if (null? names) (error "from: missing names")
      (if (and (null? (cdr names)) (pair? (car names)))
          `(py-from ,mod ',(car names))
          `(py-from ,mod ',names))))

;; ── Python 级联接口 ──
;; (py. obj "attr1" "attr2" ...) — 级联属性访问
(define-macro (py. obj . attrs)
  (if (null? attrs) obj
      (let ((access `(py-get ,obj ,(car attrs))))
        (if (null? (cdr attrs)) access
            `(py. ,access ,@(cdr attrs))))))

;; (numpy) 一键导入 numpy
(define-macro (numpy) `(begin (import "numpy") (values)))
;; (np) = numpy 别名
(unless (defined? 'np)
  (define np numpy))

;; (.method args...) — Python 方法调用的简写 (在 -> 宏中自动转换)
;; 直接使用: (invoke obj "method" args...) 或 (-> obj (method args...))

;; (numpy) — 一键导入 numpy
(define-macro (numpy) `(begin (import "numpy") (values)))


;; (new Class args...) — 创建 Python 对象
(define-macro (new cls . args) `(py-new ,cls ,@args))

;; (props obj) — 列出公开属性
(unless (defined? 'props)
  (define (props obj) (py-dir obj)))

;; (invoke obj "method" args...) — 方法调用
(define-macro (invoke obj method . args)
  (let ((method-name (if (string? method) method (symbol->string method))))
    `(py-call ,obj ,method-name ,@args)))

;; (-> x form1 form2 ...) — 线程宏，前结果插入第一个参数位置
(define-macro (-> x . forms)
  (if (null? forms) x
      (let ((form (car forms)) (rest (cdr forms)))
        (if (pair? form)
            (let ((m (car form)))
              (cond ((eq? m 'py.)
                     `(-> (py. ,x ,@(cdr form)) ,@rest))
                    ((eq? m 'py:)
                     `(-> (py: ,x ,@(cdr form)) ,@rest))
                    ((eq? m 'invoke)
                     `(-> (invoke ,x ,@(cdr form)) ,@rest))
                    ((and (symbol? m) (string-prefix? ":" (symbol->string m)))
                     (let ((method-name (string-drop (symbol->string m) 1)))
                       `(-> (invoke ,x ,method-name ,@(cdr form)) ,@rest)))
                    (else
                     `(-> (,(car form) ,x ,@(cdr form)) ,@rest))))
            `(-> (,form ,x) ,@rest)))))

;; (->> x form1 form2 ...) — 前结果插入最后一个参数位置
(define-macro (->> x . forms)
  (if (null? forms) x
      (let ((form (car forms)) (rest (cdr forms)))
        (if (pair? form)
            `(->> (,(car form) ,@(cdr form) ,x) ,@rest)
            `(->> (,form ,x) ,@rest)))))

;; (py. obj attr) — 取属性
;; (py. obj attr val) — 设属性 (set!)
;; (py. obj "method" args...) — 调方法
(define-macro (py. obj . rest)
  (if (null? rest) obj
      (let ((first (car rest)) (more (cdr rest)))
        (cond ((null? more)
               `(py-get ,obj ,first))
              ((and (null? (cdr more)) (not (string? first)))
               `(py-set! ,obj ,first ,(car more)))
              (else
               `(py-call ,obj ,first ,@more))))))

;; (new Class args...) — 创建 Python 对象
(define-macro (new cls . args) `(py-new ,cls ,@args))

;; (dir obj) 已存在，加别名
(unless (defined? 'props)
  (define (props obj) (py-dir obj)))

