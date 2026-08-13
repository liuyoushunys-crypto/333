;; test-install-ext.scm — Comprehensive test suite for install_ext (base.py:919-1366)
;; Run: python allinone.py test-install-ext.scm
;; NOTE: install_ext(env) must be called before these tests will work.
;; These tests cover every function registered in the install_ext function.

(import (scheme base))

(define-syntax test
  (syntax-rules ()
    ((_ expected expr)
     (test-equal 'expr expected expr))))
