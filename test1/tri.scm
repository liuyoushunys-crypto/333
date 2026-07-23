;; tri.scm — triple-quoted string """ and ''' tests
;; Also tests #| ... |# block comment interaction

(define (t-eq label expected actual)
  (if (equal? actual expected)
      (begin (display "[PASS] ") (display label) (newline))
      (begin (display "[FAIL] ") (display label)
             (display "  expected: ") (write expected)
             (display "  actual: ") (write actual) (newline))))

(display "=== basic triple-quoted strings ===\n")
(t-eq "simple \"\"\"" "hello" """hello""")
(t-eq "simple '''" "world" '''world''')

(display "=== multi-line strings ===\n")
(t-eq "multi line" "abc\ndef"
  """abc
def""")
(t-eq "multi line '''" "abc\ndef"
  '''abc
def''')

(t-eq "three lines" "line1\nline2\nline3"
  """line1
line2
line3""")

(display "=== strings with quotes inside ===\n")
(t-eq "single quote inside" "he\"llo" """he"llo""")
(t-eq "two quotes inside" "a\"b\"c" """a"b"c""")
(t-eq "single quote in '''" "he'llo" '''he'llo''')

(display "=== strings with escaped chars inside ===\n")
(t-eq "tab" "a\tb" """a\tb""")
(t-eq "backslash" "a\\b" """a\\b""")
(t-eq "mixed escapes" "a\nb\tc" """a\nb\tc""")

(display "=== empty triple quotes ===\n")
(t-eq "empty \"\"\"" " " """ """)
(t-eq "empty '''" " " ''' ''')

(display "=== triple quotes in expressions ===\n")
(t-eq "string-length multi"
  5
  (string-length """abc
d"""))

(display "=== triple quotes after ; comment ===\n")
;; """ not parsed here
(t-eq "after ; comment" 42 42)

;; ;; """ still ignored
(t-eq "after ;; comment" 99 99)

(display "=== triple quotes inside #| block comment |# ===\n")
#| """ inside block comment should be ignored """ |#
(t-eq "after #| block |#" 1 1)

#| multi
line
block
""" should not break things """
end |#
(t-eq "after multi-line #| |#" 2 2)

(display "=== triple quotes with regular strings ===\n")
(t-eq "mixed" "ab" (string-append """a""" "b"))
(t-eq "reg + triple" "xyz" (string-append "x" """yz"""))

(display "=== triple quotes containing ; inside ===\n")
(t-eq "semicolon inside" "a;b" """a;b""")
(t-eq "hash inside" "a#b" """a#b""")

(display "=== real-world usage examples ===\n")
(t-eq "html snippet" "<div>\n  hello\n</div>"
  """<div>
  hello
</div>""")

(t-eq "sql query" "SELECT *\nFROM users\nWHERE id = 1"
  """SELECT *
FROM users
WHERE id = 1""")

(display "=== edge: single char per line ===\n")
(t-eq "single char / line" "a\nb\nc"
  """a
b
c""")

(t-eq "trailing newline" "a\n"
  """a
""")

(display "=== edge: standalone #| without |# ==="\n)
#|
This is a block comment
spanning multiple lines
no closing needed if it's the last construct
|#
(t-eq "after #| only" 3 3)

(display "\n;; === All triple-quote tests complete ===\n")
