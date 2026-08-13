(define (assert condition . message)
  (if condition #t (error "assertion failed" message)))
