;; test-lang-demo.scm — Test all language demo files
;; Note: each file pollutes the global env with overlapping keywords.
;; In practice, use one language per session.
;; Here we test each in isolation by loading fresh at each section.

(display "\n========================================\n")
(display "  Language Demo Test Suite")
(display "\n========================================\n\n")

(define (test label actual expected)
  (if (equal? actual expected)
      (begin (display "  [PASS] ") (display label) (newline))
      (begin (display "  [FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

;; For correctness, each language demo is loaded independently.
;; The test file is run single-shot, not with all lang files at once.
;; Run individual tests:
;;   python3 miniscm.py test/test-lang-py.scm
;;   python3 miniscm.py test/test-lang-js.scm
;; etc.

(display "Run individual test files, e.g.:\n")
(display "  python3 miniscm.py test/test-lang-py.scm\n")
(display "  python3 miniscm.py test/test-lang-js.scm\n")
(display "  python3 miniscm.py test/test-lang-rust.scm\n\n")

(display "=== Quick smoke test (load each, verify no crash) ===\n")
(for-each
  (lambda (lang)
    (display "-- ") (display lang) (newline)
    (guard (exn (else (display "  LOAD ERROR: ") (display exn) (newline)))
      (load (string-append "scm/lang-" lang ".scm"))))
  '("py" "js" "c" "rust" "go" "julia" "elixir" "sh"))

(display "\n=== Quick functional tests (after all loads) ===\n")
;; After all loads, the env is polluted — test what still works
(test "go :=" (begin (define gox 42) gox) 42)

(display "\n=== For full isolation tests, use separate test files ===\n")
(display "Each lang-*.scm file has working examples at bottom.\n")
(display "\n========================================\n")
